from fastapi import APIRouter, HTTPException, UploadFile, File, Depends, status
from sqlalchemy.orm import Session
from typing import List, Optional

from app.database import get_db
from app.schemas import SessionResponse
from app.services.workflow import process_presentation_upload
from app import crud

router = APIRouter()

@router.post("/upload-context", response_model=SessionResponse)
async def upload_context_endpoint(
    slide_file: UploadFile = File(...),
    script_file: Optional[UploadFile] = File(None),
    db: Session = Depends(get_db)
):
    return await process_presentation_upload(db, slide_file, script_file)

# API Xóa
@router.delete("/sessions/{session_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_session_endpoint(session_id: str, db: Session = Depends(get_db)):
    """Xóa một bài theo ID"""
    is_deleted = crud.delete_session(db, session_id)
    
    if not is_deleted:
        raise HTTPException(
            status_code=404, 
            detail=f"Session {session_id} not found"
        )
    
    return None # 204 No Content thì không trả về body


# API Xem danh sách
@router.get("/sessions", response_model=List[SessionResponse])
def get_all_sessions_endpoint(db: Session = Depends(get_db)):
    """Trả về danh sách tất cả các bài đã tóm tắt"""
    return crud.get_all_sessions(db)