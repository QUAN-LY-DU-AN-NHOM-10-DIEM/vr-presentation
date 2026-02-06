from sqlalchemy.orm import Session
from app.database import SessionModel
import uuid

def create_session(db: Session, title: str, context: str):
    session_id = str(uuid.uuid4())
    db_session = SessionModel(
        session_id=session_id,
        title=title,
        context_text=context
    )
    db.add(db_session)
    db.commit()
    db.refresh(db_session)
    return db_session

def get_all_sessions(db: Session):
    """Lấy danh sách tất cả các session, sắp xếp mới nhất lên đầu"""
    return db.query(SessionModel).order_by(SessionModel.created_at.desc()).all()

def get_session_by_id(db: Session, session_id: str):
    """Lấy chi tiết 1 session (Dùng để kiểm tra trước khi xóa)"""
    return db.query(SessionModel).filter(SessionModel.session_id == session_id).first()

def delete_session(db: Session, session_id: str):
    """Xóa session khỏi database"""
    session_to_delete = get_session_by_id(db, session_id)
    if session_to_delete:
        db.delete(session_to_delete)
        db.commit()
        return True
    return False