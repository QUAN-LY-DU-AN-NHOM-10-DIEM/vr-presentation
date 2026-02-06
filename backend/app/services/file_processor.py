from fastapi import UploadFile
from pypdf import PdfReader
import io

async def extract_pdf(file: UploadFile) -> str:
    """Đọc nội dung text từ file PDF"""
    try:
        content = await file.read()
        pdf_reader = PdfReader(io.BytesIO(content))
        text = ""
        for page in pdf_reader.pages:
            text += page.extract_text() + "\n"
        return text.strip()
    except Exception as e:
        print(f"Error reading PDF: {e}")
        return ""

async def read_txt(file: UploadFile) -> str:
    """Đọc nội dung file TXT, xử lý decode utf-8"""
    try:
        content = await file.read()
        # Decode utf-8 để hiển thị đúng tiếng Việt
        return content.decode("utf-8").strip()
    except Exception as e:
        print(f"Error reading TXT: {e}")
        return ""