import os
from sqlalchemy.orm import Session
from app.database import EvaluationModel, SessionModel, TopicModel
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

def get_all_topics(db: Session):
    """Lấy danh sách tất cả các session, sắp xếp mới nhất lên đầu"""
    return db.query(TopicModel).order_by(TopicModel.created_at.desc()).all()

def get_topic_by_id(db: Session, topic_id: str):
    """Lấy chi tiết 1 session (Dùng để kiểm tra trước khi xóa)"""
    return db.query(TopicModel).filter(TopicModel.topic_id == topic_id).first()

def delete_topic(db: Session, topic_id: str):
    """Xóa session khỏi database và xóa file vật lý trên ổ cứng"""
    # Tìm session cần xóa
    topic_to_delete = get_topic_by_id(db, topic_id)
    
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

def create_evaluation_record(db: Session, topic_id: str, mode: str, user_speech: str, ai_result: dict):
    # 1. Tạo ID mới
    session_id = str(uuid.uuid4())
    evaluation_id = str(uuid.uuid4())

    # 2. Lưu vào bảng sessions (Phiên luyện tập)
    db_session = SessionModel(
        session_id=session_id,
        topic_id=topic_id,
        mode=mode,
        user_speech=user_speech
    )
    db.add(db_session)

    # 3. Lưu vào bảng evaluations (Bảng điểm)
    criteria = ai_result.get("criteria_scores", {})
    db_eval = EvaluationModel(
        evaluation_id=evaluation_id,
        session_id=session_id,
        accuracy_score=criteria.get("accuracy", 0),
        fluency_score=criteria.get("fluency", 0),
        repetition_score=criteria.get("repetition", 0),
        structure_score=criteria.get("structure", 0),
        overall_score=ai_result.get("overall_score", 0),
        feedback=ai_result.get("feedback", "")
    )
    db.add(db_eval)
    
    # 4. Commit cả 2 bảng cùng lúc
    db.commit()
    
    return session_id