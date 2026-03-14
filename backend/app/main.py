from fastapi import FastAPI
from app.routers import topics

app = FastAPI(title="VR Presentation Trainer API")

# Đăng ký router
app.include_router(topics.router, prefix="/api/v1", tags=["Context Upload"])

# Root check
@app.get("/")
def read_root():
    return {"message": "System is ready!"}