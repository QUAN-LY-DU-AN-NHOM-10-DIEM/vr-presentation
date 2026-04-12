import os
import json
import time
from datetime import datetime
from threading import Lock
from typing import Optional

CONFIG = {
    "sessions_file": "sessions.json",
    "session_ttl_hours": 24,
}

_session_lock = Lock()
_sessions_cache: Optional[dict] = None
_sessions_last_sync = 0


def _load_sessions() -> dict:
    global _sessions_cache, _sessions_last_sync

    if _sessions_cache is not None and time.time() - _sessions_last_sync < 60:
        return _sessions_cache

    with _session_lock:
        if os.path.exists(CONFIG["sessions_file"]):
            with open(CONFIG["sessions_file"], "r", encoding="utf-8") as f:
                _sessions_cache = json.load(f)
        else:
            _sessions_cache = {}
        _sessions_last_sync = time.time()
        return _sessions_cache


def _save_sessions(sessions: dict) -> None:
    global _sessions_cache, _sessions_last_sync

    with _session_lock:
        with open(CONFIG["sessions_file"], "w", encoding="utf-8") as f:
            json.dump(sessions, f, ensure_ascii=False, indent=2, default=str)
        _sessions_cache = sessions
        _sessions_last_sync = time.time()


def _cleanup_expired_sessions() -> int:
    sessions = _load_sessions()
    now = datetime.now()
    expired = [
        sid
        for sid, data in sessions.items()
        if (
            now - datetime.fromisoformat(data.get("created_at", now.isoformat()))
        ).total_seconds()
        > CONFIG["session_ttl_hours"] * 3600
    ]

    for sid in expired:
        del sessions[sid]

    if expired:
        _save_sessions(sessions)
        print(f"Cleaned up {len(expired)} expired sessions")

    return len(expired)


def get_session(session_id: str) -> Optional[dict]:
    _cleanup_expired_sessions()
    return _load_sessions().get(session_id)


def set_session(session_id: str, data: dict) -> None:
    sessions = _load_sessions()
    sessions[session_id] = data
    _save_sessions(sessions)


def create_session(title: str, description: str, context: str) -> str:
    import uuid

    session_id = str(uuid.uuid4())
    set_session(
        session_id,
        {
            "title": title,
            "description": description,
            "context": context,
            "history": [],
            "created_at": datetime.now().isoformat(),
        },
    )
    return session_id
