from fastapi import FastAPI
from app.database import init_db
from app.routers import topics, evaluate

app = FastAPI(title="VR Presentation Trainer API")

@app.on_event("startup")
def on_startup():
    init_db()

# Đăng ký router
app.include_router(topics.router, prefix="/api/v1", tags=["Context Upload"])
app.include_router(evaluate.router, prefix="/api/v1", tags=["Evaluate Speech"])

# Root check
@app.get("/")
def read_root():
    return {"message": "System is ready!"}