import os
import httpx
import re
from dotenv import load_dotenv
load_dotenv()

OLLAMA_URL = os.getenv("OLLAMA_URL", "http://localhost:11434/api/generate")
MODEL_NAME = os.getenv("MODEL_NAME", "qwen2.5:7b")

async def clean_and_summarize(full_text: str):
    """
    Tóm tắt và làm sạch nội dung
    """
    
    prompt = f"""
    Bạn là Giám khảo chấm thi. Hãy đọc tài liệu dưới đây và trích xuất ý chính.
    
    YÊU CẦU OUTPUT (Bắt buộc tuân thủ):
    1. Dòng 1: Viết "TOPIC: " theo sau là tên chủ đề ngắn gọn.
    2. Dòng 2: Viết "SUMMARY: " theo sau là các gạch đầu dòng tóm tắt ý chính (để dùng làm checklist chấm điểm).
    3. Không được viết thêm lời dẫn, không markdown.

    --- VÍ DỤ ĐỊNH DẠNG MẪU (CHỈ THAM KHẢO CẤU TRÚC - KHÔNG COPY NỘI DUNG) ---
    TOPIC: [Tên chủ đề chính của bài nói]
    SUMMARY: - [Khái niệm quan trọng A]: [Định nghĩa ngắn gọn]
    - [Quy trình/Các bước]: [Bước 1] -> [Bước 2] -> [Bước 3]
    - [Các phân loại]: [Loại 1], [Loại 2], [Loại 3]
    - [Số liệu/Từ khóa chuyên môn]: [Liệt kê các từ khóa tiếng Anh hoặc con số]
    -------------------------------------------------------------------------
    
    ---NỘI DUNG CẦN XỬ LÝ---
    {full_text}
    """

    payload = {
        "model": MODEL_NAME,
        "prompt": prompt,
        "stream": False,
    }

    try:
        async with httpx.AsyncClient(timeout=120.0) as client:
            response = await client.post(OLLAMA_URL, json=payload)
            response.raise_for_status()
            
            result = response.json()
            raw_text = result.get("response", "").strip()
            
            # --- LOGIC PARSE THỦ CÔNG ---
            topic = "Chủ đề chưa xác định"
            summary = raw_text

            # 1. Tìm dòng bắt đầu bằng "TOPIC:"
            # Regex này tìm chữ TOPIC:, lấy nội dung cho đến khi xuống dòng
            topic_match = re.search(r'TOPIC:\s*(.+)', raw_text, re.IGNORECASE)
            if topic_match:
                topic = topic_match.group(1).strip()

            # 2. Tìm phần SUMMARY
            # Lấy tất cả nội dung sau chữ "SUMMARY:"
            summary_match = re.search(r'SUMMARY:\s*(.+)', raw_text, re.IGNORECASE | re.DOTALL)
            if summary_match:
                summary = summary_match.group(1).strip()
            else:
                # Fallback: Nếu không tìm thấy chữ SUMMARY, thử xóa phần Topic đi để lấy phần còn lại
                summary = raw_text.replace(f"TOPIC: {topic}", "").strip()

            return topic, summary

    except Exception as e:
        print(f"❌ AI Service Error: {e}")
        return "Error", "Không thể tóm tắt nội dung lúc này."