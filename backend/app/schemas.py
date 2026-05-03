from typing import List, Dict

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


class KeywordStatus(BaseModel):
    keyword: str
    status: str  # found | paraphrased | missing


class QAItemEvaluation(BaseModel):
    question_id: int
    score: float
    feedback: str
    content_match_percent: float


class EvaluationResponse(BaseModel):
    total_score: float
    ac1_score: float
    ac1_feedback: str
    ac1_keywords: List[KeywordStatus]
    ac2_score: float
    ac2_feedback: str
    ac2_has_intro: bool
    ac2_has_closing: bool
    ac3_score: float
    ac3_feedback: str
    detailed_qa: List[QAItemEvaluation]
    session_id: str


class EvaluateRequest(BaseModel):
    session_id: str
    time_management_score: float
    eye_contact_score: float
    volume_score: float
    eye_contact_zones: List[float]
    eye_contact_zone_names: List[str]
    eye_contact_advice: str
    presentation_duration: int
    target_time: int
    qa_duration: int
    quiet_ratio: float
    loud_ratio: float
    avg_volume: float
