import uuid
from datetime import datetime

from fastapi import UploadFile, HTTPException
from app.services.file_processor import extract_pdf, read_txt
from app.services.ai_service import clean_and_summarize
from app.schemas import TopicResponse

active_sessions = {}

async def process_presentation_upload(
    slide_file: UploadFile, 
    script_file: UploadFile | None
):  
    # Validate (Logic nghiệp vụ)
    if not slide_file.filename.endswith('.pdf'):
        raise HTTPException(status_code=400, detail="Slide file must be PDF")
    
    if script_file:
        if not script_file.filename.endswith('.txt'):
            raise HTTPException(status_code=400, detail="Script file must be TXT")

    # Extract Data
    slide_text = await extract_pdf(slide_file)
    await slide_file.seek(0)
    script_text = ""
    if script_file:
        script_text = await read_txt(script_file)
        await script_file.seek(0)
    
    if not slide_text and not script_text:
        raise HTTPException(status_code=400, detail="No content extracted")

    # Merge Logic
    full_text = f"---SLIDE---\n{slide_text}\n---SCRIPT---\n{script_text}"

    # AI Processing
    ai_result = await clean_and_summarize(full_text)
    
    # Khởi tạo Session ID
    session_id = str(uuid.uuid4())
    
    # Lưu vào RAM
    active_sessions[session_id] = {
        "title": ai_result["title"],
        "description": ai_result["description"],
        "context": ai_result["context_text"],
        "history": [],
        "created_at": datetime.now() 
    }

    topic = TopicResponse(
        session_id=session_id, 
        title=ai_result["title"],
        description=ai_result["description"],
    )
    
    return topic

async def process_speech_evaluation(
    request: EvaluateSpeechRequest,
    db: Session
):
    """
    Xử lý toàn bộ logic chấm điểm bài nói của user.
    """
    # 1. Lấy context gốc từ bảng topics
    topic = get_topic_by_id(db, request.topic_id)
    if not topic or not topic.context_text:
        raise HTTPException(status_code=404, detail="Không tìm thấy chủ đề này.")

    # 2. Xử lý trường hợp nói quá ngắn (chống spam AI)
    if len(request.user_speech.split()) < 5:
        # Khởi tạo object Pydantic đúng chuẩn để không bị lỗi schema
        return EvaluateSpeechResponse(
            session_id="none",
            overall_score=1,
            criteria_scores=CriteriaScores(accuracy=1, fluency=1, repetition=1, structure=1),
            feedback="Bài nói quá ngắn, hãy trình bày rõ ràng hơn."
        )

    # 3. Gọi AI chấm điểm
    ai_result = await evaluate_speech_with_ai(topic.context_text, request.user_speech, request.mode)
    
    if not ai_result:
        raise HTTPException(status_code=500, detail="Lỗi khi chấm điểm. AI không phản hồi.")

    # 4. Lưu vào Database (sessions + evaluations)
    session_id = create_evaluation_record(
        db=db,
        topic_id=request.topic_id,
        mode=request.mode,
        user_speech=request.user_speech,
        ai_result=ai_result
    )

    # 5. Định dạng dữ liệu trả về cho Frontend
    return EvaluateSpeechResponse(
        session_id=session_id,
        overall_score=ai_result.get("overall_score", 0),
        criteria_scores=CriteriaScores(**ai_result.get("criteria_scores", {})),
        feedback=ai_result.get("feedback", "")
    )
    
async def process_generate_questions(db: Session, request: EvaluateSpeechRequest) -> GenerateQuestionResponse:
    # 1. Lấy context gốc
    topic = get_topic_by_id(db, request.topic_id)
    if not topic or not topic.context_text:
        raise HTTPException(status_code=404, detail="Không tìm thấy chủ đề tài liệu.")

    # 2. Xử lý input rỗng
    speech = request.user_speech.strip()
    if len(speech.split()) < 5:
        speech = "Thí sinh trình bày rất ngắn, chưa rõ ràng ý chính."

    # 3. Gọi AI
    ai_questions = await generate_questions_with_ai(topic.context_text, speech, request.mode)

    # 4. FALLBACK LOGIC (Bảo kê hệ thống)
    fallback_questions = [
        "Bạn có thể tóm tắt lại ý quan trọng nhất vừa trình bày không?",
        "Phần trình bày này mang lại giá trị thực tiễn gì?",
        "Xin hãy đưa ra một ví dụ cụ thể hơn cho luận điểm của bạn.",
        "Bạn gặp khó khăn lớn nhất là gì khi nghiên cứu phần này?",
        "Cơ sở nào để bạn đưa ra kết luận như vậy?"
    ]

    # Nếu AI lỗi hoặc trả về quá ít câu hỏi, đắp fallback vào cho đủ 10 câu
    final_questions = ai_questions.copy()
    if len(final_questions) < 10:
        needed = 10 - len(final_questions)
        # Bơm câu hỏi dự phòng vào
        final_questions.extend(fallback_questions[:needed])
        
    # Đảm bảo chỉ trả về tối đa 10 câu (cắt bớt nếu AI bị lố)
    final_questions = final_questions[:10]

    return GenerateQuestionResponse(questions=final_questions)
