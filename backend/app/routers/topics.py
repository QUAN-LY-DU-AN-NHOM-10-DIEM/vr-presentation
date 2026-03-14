from fastapi import APIRouter, UploadFile, File
from typing import Optional

from app.schemas import TopicResponse
from app.services.workflow import process_presentation_upload

router = APIRouter()

@router.post("/upload-context", response_model=TopicResponse)
async def upload_context_endpoint(
    slide_file: UploadFile = File(...),
    script_file: Optional[UploadFile] = File(None)
):
    return await process_presentation_upload(slide_file, script_file)