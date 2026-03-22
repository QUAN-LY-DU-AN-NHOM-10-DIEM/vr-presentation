from pydantic import BaseModel

class TopicResponse(BaseModel):
    title: str
    description: str
    context_text: str

    class Config:
        from_attributes = True