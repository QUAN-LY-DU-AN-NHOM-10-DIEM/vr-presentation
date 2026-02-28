import os
import shutil
from fastapi import UploadFile
from dotenv import load_dotenv
load_dotenv()

UPLOAD_DIR = os.getenv("UPLOAD_DIR", "uploads")

# Tạo thư mục uploads nếu chưa có
os.makedirs(UPLOAD_DIR, exist_ok=True)

async def save_upload_file(file: UploadFile, topic_id: str, extension: str) -> str:
    """
    Lưu file upload vào đĩa với tên là topic_id.
    Ví dụ: uploads/123-abc.pdf
    Trả về: Đường dẫn file (str)
    """
    filename = f"{topic_id}.{extension}"
    file_path = os.path.join(UPLOAD_DIR, filename)
    
    # Lưu nội dung file xuống đĩa
    with open(file_path, "wb") as buffer:
        # Copy từ stream của UploadFile sang file trên đĩa
        shutil.copyfileobj(file.file, buffer)
        
    return file_path