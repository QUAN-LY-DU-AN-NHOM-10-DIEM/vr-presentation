from pydantic import BaseModel
from datetime import datetime

class SessionResponse(BaseModel):
    session_id: str
    title: str
    context_text: str
    created_at: datetime

    class Config:
        from_attributes = True