from fastapi import FastAPI
from app.database import init_db
from app.routers import upload

app = FastAPI(title="VR Presentation Trainer API")

@app.on_event("startup")
def on_startup():
    init_db()

# Đăng ký router
app.include_router(upload.router, prefix="/api/v1", tags=["Context Upload"])

# Root check
@app.get("/")
def read_root():
    return {"message": "System is ready!"}