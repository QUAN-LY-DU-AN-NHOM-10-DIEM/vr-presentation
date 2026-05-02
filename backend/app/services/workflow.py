from datetime import datetime
from typing import List, Optional, Dict

from fastapi import UploadFile, HTTPException

from app.services.file_processor import extract_pdf, read_txt
from app.services.ai_service import (
    clean_and_summarize,
    generate_questions_with_ai,
    evaluate_ac1,
    evaluate_ac2,
    evaluate_ac3,
)
from app.services.session_manager import get_session, set_session, create_session
from app.services.stt_service import transcribe_audio
from app.schemas import (
    GenerateQuestionResponse,
    TopicResponse,
    TranscriptResult,
    BatchTranscriptResponse,
    EvaluationResponse,
    KeywordStatus,
    QAItemEvaluation,
)

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
    if not slide_file.filename or not slide_file.filename.endswith(".pdf"):
        raise HTTPException(status_code=400, detail="Slide file must be PDF")

    if script_file and (
        not script_file.filename or not script_file.filename.endswith(".txt")
    ):
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
    print(f"Transcribed speech: {speech}")
    if len(speech.split()) < 5:
        speech = "Thí sinh trình bày rất ngắn, chưa rõ ràng ý chính."

    session_data["presentation_transcript"] = speech
    set_session(session_id, session_data)

    ai_questions = await generate_questions_with_ai(context_text, speech, mode)
    if not isinstance(ai_questions, list):
        ai_questions = []

    final_questions = ai_questions.copy()
    if len(final_questions) < CONFIG["max_questions"]:
        needed = CONFIG["max_questions"] - len(final_questions)
        final_questions.extend(FALLBACK_QUESTIONS[:needed])

    final_questions = final_questions[: CONFIG["max_questions"]]

    questions_map = {}
    for idx, question_text in enumerate(final_questions, start=1):
        key = str(idx)
        questions_map[key] = {
            "question": question_text,
            "answer": "",
            "file_name": "",
            "timestamp": datetime.now().isoformat(),
        }

    session_data["questions"] = questions_map
    set_session(session_id, session_data)

    return GenerateQuestionResponse(questions=final_questions)


async def process_batch_transcribe(
    session_id: str, audio_files: List[dict]
) -> BatchTranscriptResponse:
    session_data = get_session(session_id)
    if not session_data:
        raise HTTPException(
            status_code=404,
            detail="Không tìm thấy phiên làm việc hoặc Session đã hết hạn.",
        )

    context_text = session_data.get("context", "")
    results = []
    questions_map = session_data.get("questions", {})

    for item in audio_files:
        question_id = str(item["question_id"])
        audio_file = item["audio_file"]
        file_name = item.get("file_name", "")

        speech = (await transcribe_audio(audio_file)).strip()
        if len(speech.split()) < 5:
            speech = "Thí sinh trình bày rất ngắn, chưa rõ ràng ý chính."

        results.append(
            TranscriptResult(question_id=int(question_id), transcript=speech)
        )

        questions_map[question_id] = {
            "question": questions_map.get(question_id, {}).get("question", ""),
            "answer": speech,
            "file_name": file_name,
            "timestamp": datetime.now().isoformat(),
        }

    session_data["questions"] = questions_map
    set_session(session_id, session_data)

    return BatchTranscriptResponse(results=results, session_id=session_id)


async def process_evaluate(session_id: str) -> EvaluationResponse:
    session_data = get_session(session_id)
    if not session_data:
        raise HTTPException(status_code=404, detail="Session không tìm thấy")

    context = session_data.get("context", "")
    questions = session_data.get("questions", {})
    presentation_transcript = session_data.get("presentation_transcript", "")

    ac1_result = await evaluate_ac1(context, presentation_transcript)
    ac2_result = await evaluate_ac2(
        presentation_transcript, len(presentation_transcript.split())
    )
    ac3_result = await evaluate_ac3(questions)

    ac1_score = ac1_result["score"]
    ac2_score = ac2_result["score"]
    ac3_score = ac3_result["avg_score"]

    total_score = round((ac1_score + ac2_score + ac3_score) / 3, 1)

    return EvaluationResponse(
        total_score=total_score,
        ac1_score=ac1_score,
        ac1_feedback=ac1_result["feedback"],
        ac1_keywords=ac1_result["keywords"],
        ac2_score=ac2_score,
        ac2_feedback=ac2_result["feedback"],
        ac2_has_intro=ac2_result["has_intro"],
        ac2_has_closing=ac2_result["has_closing"],
        ac3_score=ac3_score,
        ac3_feedback=ac3_result["feedback"],
        detailed_qa=ac3_result["detailed"],
        session_id=session_id,
    )


async def process_evaluate_content(context: str, presentation_transcript: str):
    """Chấm điểm nội dung (AC1 - từ khóa)"""
    return await evaluate_ac1(context, presentation_transcript)
