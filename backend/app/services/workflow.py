import uuid
from fastapi import UploadFile, HTTPException
from sqlalchemy.orm import Session
from app.services.file_processor import extract_pdf, read_txt
from app.services.ai_service import clean_and_summarize
from app.crud import create_topic
from app.services.storage_service import save_upload_file

async def process_presentation_upload(
    db: Session, 
    title: str,
    description: str,
    slide_file: UploadFile, 
    script_file: UploadFile | None
):
    topic_id = str(uuid.uuid4())
    
    # Validate (Logic nghiệp vụ)
    if not slide_file.filename.endswith('.pdf'):
        raise HTTPException(status_code=400, detail="Slide file must be PDF")
    
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
    
    saved_slide_path = await save_upload_file(slide_file, topic_id, "pdf")
    
    saved_script_path = None
    if script_file:
        saved_script_path = await save_upload_file(script_file, topic_id, "txt")

    # Merge Logic
    full_text = f"---TITLE---{title}\n---DESCRIPTION---\n{description}---SLIDE---\n{slide_text}\n---SCRIPT---\n{script_text}"

    # AI Processing
    summary = await clean_and_summarize(full_text)
    
    # Persist Data (Gọi CRUD)
    new_session = create_topic(db, title=title, description=description, context=summary, slide_path=saved_slide_path,
        script_path=saved_script_path, topic_id=topic_id)
    
    return new_session