from pydantic import BaseModel

class TopicResponse(BaseModel):
    session_id: str
    title: str
    description: str

    class Config:
        from_attributes = True