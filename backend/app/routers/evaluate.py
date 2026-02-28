from typing import List

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session

from app.database import get_db
from app.schemas import EvaluateSpeechRequest, EvaluateSpeechResponse, GenerateQuestionResponse, SessionDetailResponse
from app.services.workflow import process_generate_questions, process_speech_evaluation
from app.crud import delete_session_record, get_all_session_with_evaluation, get_session_with_evaluation

router = APIRouter()

@router.post("/evaluate-speech", response_model=EvaluateSpeechResponse)
async def evaluate_speech_endpoint(
    request: EvaluateSpeechRequest,
    db: Session = Depends(get_db)
):
    """
    API Giám khảo AI chấm điểm bài thuyết trình.
    - Cần truyền vào: topic_id, user_speech, mode ('practice' hoặc 'exam')
    """
    return await process_speech_evaluation(request, db)

@router.get("/sessions/{session_id}", response_model=SessionDetailResponse)
def get_session_detail_endpoint(session_id: str, db: Session = Depends(get_db)):
    """
    API lấy chi tiết 1 phiên luyện tập, bao gồm cả điểm số đánh giá.
    """
    session_data = get_session_with_evaluation(db, session_id)
    if not session_data:
        raise HTTPException(status_code=404, detail=f"Không tìm thấy phiên luyện tập với ID {session_id}")
    
    return session_data

@router.delete("/sessions/{session_id}", status_code=204)
def delete_session_endpoint(session_id: str, db: Session = Depends(get_db)):
    """
    API xóa lịch sử luyện tập (Tự động xóa luôn bảng điểm).
    """
    is_deleted = delete_session_record(db, session_id)
    if not is_deleted:
        raise HTTPException(status_code=404, detail=f"Không tìm thấy phiên luyện tập với ID {session_id}")
    
    return None # Xóa thành công thì trả về 204 No Content

@router.get("/sessions", response_model=List[SessionDetailResponse])
def get_all_sessions_endpoint(db: Session = Depends(get_db)):
    """
    Lấy toàn bộ lịch sử luyện tập.
    """
    sessions = get_all_session_with_evaluation(db)
    return sessions

@router.post("/generate-question", response_model=GenerateQuestionResponse)
async def generate_question_endpoint(
    request: EvaluateSpeechRequest,
    db: Session = Depends(get_db)
):
    """
    API Sinh câu hỏi phản biện.
    Đóng vai giám khảo/khán giả đặt câu hỏi dựa trên Context gốc và đoạn vừa trình bày.
    """
    return await process_generate_questions(db, request)