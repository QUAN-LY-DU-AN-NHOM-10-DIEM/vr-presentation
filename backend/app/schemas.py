from typing import List

from pydantic import BaseModel
from datetime import datetime

class TopicResponse(BaseModel):
    session_id: str
    title: str
    description: str

    class Config:
        from_attributes = True
        
class GenerateQuestionResponse(BaseModel):
    questions: List[str]
