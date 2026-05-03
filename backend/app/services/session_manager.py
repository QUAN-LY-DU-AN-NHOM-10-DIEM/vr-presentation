import os
from datetime import datetime
from typing import Optional
from pymongo import MongoClient
from dotenv import load_dotenv

load_dotenv()

CONFIG = {
    "session_ttl_hours": 6,
}

MONGO_URI = os.getenv("MONGO_URI", "mongodb://localhost:27017/")
MONGO_DB_NAME = os.getenv("MONGO_DB_NAME", "vr_presentation")

client = MongoClient(MONGO_URI)
db = client[MONGO_DB_NAME]
sessions_collection = db["sessions"]

# Create TTL index to automatically expire sessions
sessions_collection.create_index(
    "created_at", expireAfterSeconds=CONFIG["session_ttl_hours"] * 3600
)


def get_session(session_id: str) -> Optional[dict]:
    session = sessions_collection.find_one({"session_id": session_id})
    if session:
        session.pop("_id", None)
        return session
    return None


def set_session(session_id: str, data: dict) -> None:
    data_to_set = {**data, "session_id": session_id}
    sessions_collection.update_one(
        {"session_id": session_id}, {"$set": data_to_set}, upsert=True
    )


def create_session(title: str, description: str, context: str) -> str:
    import uuid

    session_id = str(uuid.uuid4())
    data = {
        "title": title,
        "description": description,
        "context": context,
        "history": [],
        "created_at": datetime.now(),
    }
    set_session(session_id, data)
    return session_id
