from typing import List

from fastapi import APIRouter, UploadFile, File
from app.services.workflow import process_generate_questions
from app.schemas import GenerateQuestionResponse

router = APIRouter()

@router.post("/generate-question", response_model=GenerateQuestionResponse)
async def generate_question_endpoint(
    session_id: str,
    audio_file: UploadFile = File(...),
    mode: str = "practice"
):
    """
    API Sinh câu hỏi phản biện.
    Đóng vai giám khảo/khán giả đặt câu hỏi dựa trên Context gốc và đoạn vừa trình bày.
    """
    return await process_generate_questions(session_id, audio_file, mode)