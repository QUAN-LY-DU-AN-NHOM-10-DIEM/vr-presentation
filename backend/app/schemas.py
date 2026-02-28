from pydantic import BaseModel
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