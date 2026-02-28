from fastapi import APIRouter, Depends
from sqlalchemy.orm import Session

from app.database import get_db
from app.schemas import EvaluateSpeechRequest, EvaluateSpeechResponse
from app.services.workflow import process_speech_evaluation

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