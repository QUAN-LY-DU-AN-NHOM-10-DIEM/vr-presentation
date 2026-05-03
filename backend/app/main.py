from fastapi import FastAPI

from app.routers import topics, evaluate
from fastapi.middleware.cors import CORSMiddleware

app = FastAPI(title="VR Presentation Trainer API")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"], # Cho phép mọi tên miền gọi API
    allow_credentials=True,
    allow_methods=["*"], # Cho phép mọi phương thức (GET, POST...)
    allow_headers=["*"], # Cho phép mọi headers (bao gồm cả header của ngrok)
)

# Đăng ký router
app.include_router(topics.router, prefix="/api/v1", tags=["Context Upload"])
app.include_router(evaluate.router, prefix="/api/v1", tags=["Evaluate Speech"])

# Root check
@app.get("/")
def read_root():
    return {"message": "System is ready!"}
