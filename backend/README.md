# 🎤 VR Presentation Trainer - Backend API

Backend Service cho hệ thống luyện tập thuyết trình thực tế ảo. Hệ thống cung cấp API để upload tài liệu (Slide, Script), sử dụng AI (Local LLM) để tóm tắt nội dung và tạo context cho bài luyện tập.

## 🛠 Tech Stack

- **Language:** Python 3.10+
- **Framework:** FastAPI
- **Database:** PostgreSQL (với SQLAlchemy ORM)
- **AI Engine:** Ollama (chạy local với model Llama 3)
- **Architecture:** Clean Architecture (Controller - Service - Repository)

---

## 🚀 Hướng dẫn Cài đặt & Chạy (Dành cho Dev)

### 1. Chuẩn bị môi trường
Đảm bảo máy bạn đã cài đặt:
- [Python 3.10+](https://www.python.org/)
- [PostgreSQL](https://www.postgresql.org/) (và PgAdmin để quản lý DB)
- [Ollama](https://ollama.com/) (Để chạy AI Local)

### 2. Setup AI (Ollama)
Mở terminal và chạy lệnh sau để tải model về máy (chỉ làm 1 lần):
```bash
ollama pull qwen2.5:7b
```
_Lưu ý: Giữ ứng dụng Ollama chạy ngầm trong quá trình dev._

### 3. Cài đặt Project

*Bước 1:* Clone repo và đi vào thư mục backend:
``` bash
cd backend
```
*Bước 2:* Tạo môi trường ảo (Virtual Environment):
``` bash
python -m venv venv
```
*Bước 3:* Kích hoạt môi trường ảo:

- Windows: `venv\Scripts\activate`
- Mac/Linux: `source venv/bin/activate`

*Bước 4:* Cài đặt thư viện:
``` bash
pip install -r requirements.txt
```

### 4. Cấu hình Database & Môi trường

Tạo file .env tại thư mục gốc (copy từ .env.example nếu có) và điền thông tin của bạn:
Ini, TOML
```
# Database Config
DB_HOST=localhost
DB_PORT=5432
DB_USER=postgres
DB_PASS=your_password_here  <-- Sửa mật khẩu Postgres của bạn
DB_NAME=presentation_db

# AI Config
OLLAMA_URL=http://localhost:11434/api/generate
MODEL_NAME=llama3
```
### 5. Chạy Server

Sử dụng Uvicorn để start server ở chế độ reload (tự động cập nhật khi sửa code):
``` bash
uvicorn app.main:app --reload
```
Server sẽ chạy tại: http://127.0.0.1:8000

## 📚 API Documentation

Sau khi chạy server, truy cập link sau để xem tài liệu API và test trực tiếp:
- Swagger UI: http://127.0.0.1:8000/docs
- ReDoc: http://127.0.0.1:8000/redoc

## 🧪 Cách Test nhanh (Automation Test)

Dự án có sẵn script để test luồng Upload + AI Tóm tắt. Chạy lệnh sau (khi server đang bật):
``` bash
python test_api.py
```

Script sẽ tự động:
    - Tạo 1 file PDF giả và 1 file Script giả.
    - Gửi lên API.
    - In ra kết quả tóm tắt từ AI.

## 📂 Cấu trúc dự án
``` plaintext

app/
├── main.py              # Entry point (Cấu hình App)
├── database.py          # Kết nối Database & Model
├── schemas.py           # Định dạng dữ liệu (Pydantic)
├── crud.py              # Thao tác Database (Create/Read/Delete)
├── routers/             # API Endpoints (Controller)
│   └── upload.py        
└── services/            # Xử lý Logic nghiệp vụ
    ├── ai_service.py    # Giao tiếp với Ollama
    ├── file_processor.py# Đọc PDF/TXT
    └── workflow.py      # Điều phối luồng xử lý chính

```