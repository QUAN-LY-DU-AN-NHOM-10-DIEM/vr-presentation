import os
from sqlalchemy import ForeignKey, Integer, create_engine, Column, String, Text, DateTime
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.orm import relationship, sessionmaker
from sqlalchemy.sql import func
from dotenv import load_dotenv

# Import thêm 2 hàm này
from sqlalchemy_utils import database_exists, create_database

load_dotenv()

DB_URL = f"postgresql://{os.getenv('DB_USER')}:{os.getenv('DB_PASS')}@{os.getenv('DB_HOST')}:{os.getenv('DB_PORT')}/{os.getenv('DB_NAME')}"

engine = create_engine(DB_URL)
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)
Base = declarative_base()

class TopicModel(Base):
    __tablename__ = "topics"
    topic_id = Column(String, primary_key=True, index=True)
    title = Column(Text, nullable=True)
    description = Column(Text, nullable=True)
    context_text = Column(Text, nullable=True)
    slide_path = Column(String, nullable=True)  
    script_path = Column(String, nullable=True)
    created_at = Column(DateTime(timezone=True), server_default=func.now())
    
    # Quan hệ 1-N: 1 Topic có nhiều Session luyện tập
    sessions = relationship("SessionModel", back_populates="topic", cascade="all, delete-orphan")
    
    
class SessionModel(Base):
    __tablename__ = "sessions"

    session_id = Column(String, primary_key=True, index=True)
    topic_id = Column(String, ForeignKey("topics.topic_id"), nullable=False)
    mode = Column(String, nullable=False) # 'practice' hoặc 'exam'
    user_speech = Column(Text, nullable=False) # Băng ghi âm bóc chữ
    created_at = Column(DateTime(timezone=True), server_default=func.now())

    # Kết nối ngược lại bảng Topic
    topic = relationship("TopicModel", back_populates="sessions")
    # Quan hệ 1-1: 1 Session có 1 Bảng đánh giá
    evaluation = relationship("EvaluationModel", back_populates="session", uselist=False, cascade="all, delete-orphan")

class EvaluationModel(Base):
    __tablename__ = "evaluations"

    evaluation_id = Column(String, primary_key=True, index=True)
    session_id = Column(String, ForeignKey("sessions.session_id"), nullable=False)
    
    # Các tiêu chí đánh giá (Thang điểm)
    accuracy_score = Column(Integer, nullable=False)    # Độ chính xác
    fluency_score = Column(Integer, nullable=False)     # Lưu loát
    repetition_score = Column(Integer, nullable=False)  # Lặp từ
    structure_score = Column(Integer, nullable=False)   # Cấu trúc
    overall_score = Column(Integer, nullable=False)     # Điểm tổng
    
    feedback = Column(Text, nullable=True) # Nhận xét bằng chữ
    
    # Kết nối ngược lại bảng Session
    session = relationship("SessionModel", back_populates="evaluation")

def init_db():
    # 1. Kiểm tra xem Database có tồn tại không
    if not database_exists(engine.url):
        create_database(engine.url) # Tự động tạo DB nếu chưa có
        print(f"✅ Created database {os.getenv('DB_NAME')}")
    else:
        print(f"ℹ️  Database {os.getenv('DB_NAME')} already exists")

    # 2. Tạo bảng
    Base.metadata.create_all(bind=engine)
    print("✅ Tables initialized!")

def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()