from fastapi import UploadFile, HTTPException
from sqlalchemy.orm import Session
from app.services.file_processor import extract_pdf, read_txt
from app.services.ai_service import clean_and_summarize
from app.crud import create_session

async def process_presentation_upload(
    db: Session, 
    slide_file: UploadFile, 
    script_file: UploadFile | None
):
    # 1. Validate (Logic nghiệp vụ)
    if not slide_file.filename.endswith('.pdf'):
        raise HTTPException(status_code=400, detail="Slide file must be PDF")

    # 2. Extract Data
    slide_text = await extract_pdf(slide_file)
    script_text = ""
    if script_file:
        script_text = await read_txt(script_file)
    
    if not slide_text and not script_text:
        raise HTTPException(status_code=400, detail="No content extracted")

    # 3. Merge Logic
    full_text = f"---SLIDE---\n{slide_text}\n---SCRIPT---\n{script_text}"

    # 4. AI Processing
    topic, summary = await clean_and_summarize(full_text)
    
    if topic == "Error":
        raise HTTPException(status_code=500, detail="AI service failed to summarize content")

    # 5. Persist Data (Gọi CRUD)
    new_session = create_session(db, title=topic, context=summary)
    
    return new_session