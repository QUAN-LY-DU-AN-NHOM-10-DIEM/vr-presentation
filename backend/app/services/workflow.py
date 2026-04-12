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