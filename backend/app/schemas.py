from typing import List

from pydantic import BaseModel
from fastapi import UploadFile


class TopicResponse(BaseModel):
    session_id: str
    title: str
    description: str

    class Config:
        from_attributes = True


class GenerateQuestionResponse(BaseModel):
    questions: List[str]


class TranscriptResult(BaseModel):
    question_id: int
    transcript: str


class BatchTranscriptResponse(BaseModel):
    results: List[TranscriptResult]
    session_id: str


class BatchTranscribeRequest(BaseModel):
    session_id: str
    question_ids: List[int]
