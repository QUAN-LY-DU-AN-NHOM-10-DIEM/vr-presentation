from typing import List, Literal, Optional

from pydantic import BaseModel, ConfigDict
from datetime import datetime

class TopicResponse(BaseModel):
    topic_id: str
    title: str
    description: str
    context_text: str
    slide_path: str
    script_path: str = None
    created_at: datetime

    class Config:
        from_attributes = True
        
# --- INPUT MODEL ---
class EvaluateSpeechRequest(BaseModel):
    topic_id: str
    user_speech: str
    mode: Literal["practice", "exam"] = "practice"

# --- OUTPUT MODEL ---
class CriteriaScores(BaseModel):
    accuracy: int      # Độ chính xác
    fluency: int       # Độ lưu loát
    repetition: int    # Lặp từ
    structure: int     # Cấu trúc

class EvaluateSpeechResponse(BaseModel):
    session_id: str    # Trả về ID của phiên tập này để Frontend lưu lại
    overall_score: int
    criteria_scores: CriteriaScores
    feedback: str
    
class EvaluationDetail(BaseModel):
    accuracy_score: int
    fluency_score: int
    repetition_score: int
    structure_score: int
    overall_score: int
    feedback: Optional[str] = None
    
    model_config = ConfigDict(from_attributes=True)

class SessionDetailResponse(BaseModel):
    session_id: str
    topic_id: str
    mode: str
    user_speech: str
    created_at: Optional[datetime] = None
    
    # Kéo luôn cái bảng điểm đính kèm vào đây
    evaluation: Optional[EvaluationDetail] = None 
    
    model_config = ConfigDict(from_attributes=True)
    
class GenerateQuestionResponse(BaseModel):
    questions: List[str]