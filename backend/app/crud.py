import os
from sqlalchemy.orm import Session
from app.database import TopicModel
import uuid

def create_topic(db: Session, title: str, description: str, context: str, slide_path: str, script_path: str = None, topic_id: str = None):
    if not topic_id:
        topic_id = str(uuid.uuid4())
    db_session = TopicModel(
        topic_id=topic_id,
        title=title,
        description=description,
        context_text=context,
        slide_path=slide_path,
        script_path=script_path
    )
    db.add(db_session)
    db.commit()
    db.refresh(db_session)
    return db_session

def get_all_sessions(db: Session):
    """Lấy danh sách tất cả các session, sắp xếp mới nhất lên đầu"""
    return db.query(TopicModel).order_by(TopicModel.created_at.desc()).all()

def get_session_by_id(db: Session, topic_id: str):
    """Lấy chi tiết 1 session (Dùng để kiểm tra trước khi xóa)"""
    return db.query(TopicModel).filter(TopicModel.topic_id == topic_id).first()

def delete_session(db: Session, topic_id: str):
    """Xóa session khỏi database và xóa file vật lý trên ổ cứng"""
    #TODO: mốt có update xóa thêm các session liên quan tới topic này
    # Tìm session cần xóa
    topic_to_delete = get_session_by_id(db, topic_id)
    
    if topic_to_delete:
        # Xóa file Slide (PDF) nếu có
        if topic_to_delete.slide_path and os.path.exists(topic_to_delete.slide_path):
            try:
                os.remove(topic_to_delete.slide_path)
                print(f"🗑️ Đã xóa file slide: {topic_to_delete.slide_path}")
            except Exception as e:
                print(f"⚠️ Lỗi khi xóa file slide: {e}")

        # Xóa file Script (TXT) nếu có
        if topic_to_delete.script_path and os.path.exists(topic_to_delete.script_path):
            try:
                os.remove(topic_to_delete.script_path)
                print(f"🗑️ Đã xóa file script: {topic_to_delete.script_path}")
            except Exception as e:
                print(f"⚠️ Lỗi khi xóa file script: {e}")

        # Xóa record trong Database
        db.delete(topic_to_delete)
        db.commit()
        return True
        
    return False