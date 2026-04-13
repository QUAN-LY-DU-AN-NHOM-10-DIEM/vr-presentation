from typing import List

from fastapi import APIRouter, UploadFile, File, Query
from app.services.workflow import (
    process_generate_questions,
    process_batch_transcribe,
    process_evaluate,
)
from app.schemas import (
    GenerateQuestionResponse,
    BatchTranscriptResponse,
    EvaluationResponse,
)

router = APIRouter()


@router.post("/generate-question", response_model=GenerateQuestionResponse)
async def generate_question_endpoint(
    session_id: str, audio_file: UploadFile = File(...), mode: str = "practice"
):
    """
    API Sinh câu hỏi phản biện.
    Đóng vai giám khảo/khán giả đặt câu hỏi dựa trên Context gốc và đoạn vừa trình bày.
    """
    return await process_generate_questions(session_id, audio_file, mode)


@router.post("/submit", response_model=BatchTranscriptResponse)
async def batch_transcribe_endpoint(
    session_id: str = Query(..., description="Session ID"),
    audio_files: List[UploadFile] = File(
        ..., description="Danh sách file audio (Question_1.mp4, Question_2.mp4...)"
    ),
):
    """
    API nhận nhiều file audio cùng lúc, transcript và lưu vào session.
    - File phải đặt tên theo format: Question_[id].mp4 (VD: Question_1.mp4, Question_2.mp4)
    """
    import re

    audio_data = []
    for f in audio_files:
        filename = f.filename or ""
        match = re.match(r"Question_(\d+)", filename)
        if match:
            question_id = int(match.group(1))
            audio_data.append(
                {
                    "question_id": question_id,
                    "audio_file": f,
                    "file_name": filename,
                }
            )

    if not audio_data:
        from fastapi import HTTPException

        raise HTTPException(
            status_code=400,
            detail="Không tìm thấy file nào đúng format Question_[id].mp4",
        )

    return await process_batch_transcribe(session_id, audio_data)


@router.get("/evaluate", response_model=EvaluationResponse)
async def evaluate_endpoint(session_id: str = Query(..., description="Session ID")):
    """
    API đánh giá toàn diện theo 3 tiêu chí:
    - AC1: Tỷ lệ bám sát từ khóa
    - AC2: Cấu trúc bài thuyết trình (Mở bài, Kết bài)
    - AC3: Điểm xử lý Q&A
    """
    return await process_evaluate(session_id)
