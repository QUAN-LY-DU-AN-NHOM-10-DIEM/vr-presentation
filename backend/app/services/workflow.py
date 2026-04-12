from datetime import datetime
from typing import Optional

from fastapi import UploadFile, HTTPException

from app.services.file_processor import extract_pdf, read_txt
from app.services.ai_service import clean_and_summarize, generate_questions_with_ai
from app.services.session_manager import get_session, set_session, create_session
from app.services.stt_service import transcribe_audio
from app.schemas import GenerateQuestionResponse, TopicResponse

CONFIG = {
    "max_questions": 10,
}

FALLBACK_QUESTIONS = [
    "Bạn có thể tóm tắt lại ý quan trọng nhất vừa trình bày không?",
    "Phần trình bày này mang lại giá trị thực tiễn gì?",
    "Xin hãy đưa ra một ví dụ cụ thể hơn cho luận điểm của bạn.",
    "Bạn gặp khó khăn lớn nhất là gì khi nghiên cứu phần này?",
    "Cơ sở nào để bạn đưa ra kết luận như vậy?",
]


async def process_presentation_upload(
    slide_file: UploadFile, script_file: Optional[UploadFile] = None
) -> TopicResponse:
    if not slide_file.filename.endswith(".pdf"):
        raise HTTPException(status_code=400, detail="Slide file must be PDF")

    if script_file and not script_file.filename.endswith(".txt"):
        raise HTTPException(status_code=400, detail="Script file must be TXT")

    slide_text = await extract_pdf(slide_file)
    await slide_file.seek(0)

    script_text = ""
    if script_file:
        script_text = await read_txt(script_file)
        await script_file.seek(0)

    if not slide_text and not script_text:
        raise HTTPException(status_code=400, detail="No content extracted")

    full_text = f"---SLIDE---\n{slide_text}\n---SCRIPT---\n{script_text}"
    ai_result = await clean_and_summarize(full_text)

    session_id = create_session(
        title=ai_result["title"],
        description=ai_result["description"],
        context=ai_result["context_text"],
    )

    return TopicResponse(
        session_id=session_id,
        title=ai_result["title"],
        description=ai_result["description"],
    )


async def process_generate_questions(
    session_id: str, audio_file: UploadFile, mode: str
) -> GenerateQuestionResponse:
    session_data = get_session(session_id)
    if not session_data:
        raise HTTPException(
            status_code=404,
            detail="Không tìm thấy phiên làm việc hoặc Session đã hết hạn.",
        )

    context_text = session_data.get("context", "")
    if not context_text:
        raise HTTPException(status_code=400, detail="Session không có dữ liệu Context.")

    speech = (await transcribe_audio(audio_file)).strip()
    if len(speech.split()) < 5:
        speech = "Thí sinh trình bày rất ngắn, chưa rõ ràng ý chính."

    ai_questions = await generate_questions_with_ai(context_text, speech, mode)
    if not isinstance(ai_questions, list):
        ai_questions = []

    final_questions = ai_questions.copy()
    if len(final_questions) < CONFIG["max_questions"]:
        needed = CONFIG["max_questions"] - len(final_questions)
        final_questions.extend(FALLBACK_QUESTIONS[:needed])

    final_questions = final_questions[: CONFIG["max_questions"]]

    session_data["history"].append(
        {
            "mode": mode,
            "transcript": speech,
            "generated_questions": final_questions,
            "timestamp": datetime.now().isoformat(),
        }
    )
    set_session(session_id, session_data)

    return GenerateQuestionResponse(questions=final_questions)
