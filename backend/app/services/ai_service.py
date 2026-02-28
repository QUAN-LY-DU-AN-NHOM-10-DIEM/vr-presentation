import os
import re
import httpx
from dotenv import load_dotenv

# Import các SDK
from openai import AsyncOpenAI, AsyncAzureOpenAI
import google.generativeai as genai

load_dotenv()

PROVIDER = os.getenv("LLM_PROVIDER", "ollama").lower()
MODEL_NAME = os.getenv("MODEL_NAME", "qwen2.5:7b")

async def _call_llm(prompt: str) -> str:
    """Hàm nội bộ để gọi API dựa trên Provider được cấu hình"""
    
    if PROVIDER == "openai":
        client = AsyncOpenAI(api_key=os.getenv("OPENAI_API_KEY"))
        response = await client.chat.completions.create(
            model=MODEL_NAME,
            messages=[{"role": "user", "content": prompt}],
            temperature=0.3
        )
        return response.choices[0].message.content

    elif PROVIDER == "deepseek":
        # DeepSeek tương thích hoàn toàn với OpenAI SDK
        client = AsyncOpenAI(
            api_key=os.getenv("DEEPSEEK_API_KEY"), 
            base_url="https://api.deepseek.com/v1"
        )
        response = await client.chat.completions.create(
            model=MODEL_NAME, # vd: deepseek-chat
            messages=[{"role": "user", "content": prompt}],
            temperature=0.3
        )
        return response.choices[0].message.content

    elif PROVIDER == "azure":
        client = AsyncAzureOpenAI(
            azure_endpoint=os.getenv("AZURE_OPENAI_ENDPOINT"),
            api_key=os.getenv("AZURE_OPENAI_API_KEY"),
            api_version=os.getenv("AZURE_OPENAI_API_VERSION")
        )
        response = await client.chat.completions.create(
            model=MODEL_NAME, # Tên deployment trên Azure
            messages=[{"role": "user", "content": prompt}],
            temperature=0.3
        )
        return response.choices[0].message.content

    elif PROVIDER == "gemini":
        genai.configure(api_key=os.getenv("GEMINI_API_KEY"))
        # Chạy đồng bộ trong async context (do thư viện genai xử lý async khá phức tạp)
        model = genai.GenerativeModel(MODEL_NAME) # vd: gemini-1.5-flash
        response = model.generate_content(prompt)
        return response.text

    elif PROVIDER == "ollama":
        # Giữ nguyên cách gọi bằng httpx của bạn
        ollama_url = os.getenv("OLLAMA_URL", "http://localhost:11434/api/generate")
        payload = {"model": MODEL_NAME, "prompt": prompt, "stream": False}
        async with httpx.AsyncClient(timeout=120.0) as client:
            response = await client.post(ollama_url, json=payload)
            response.raise_for_status()
            return response.json().get("response", "")
            
    else:
        raise ValueError(f"Provider '{PROVIDER}' không được hỗ trợ.")

async def clean_and_summarize(full_text: str) -> str:
    """
    Tóm tắt và làm sạch nội dung (Hàm chính)
    """
    prompt = f"""
    Bạn là Giám khảo chấm thi. Hãy đọc tài liệu dưới đây và trích xuất ý chính.
    
    YÊU CẦU OUTPUT (Bắt buộc tuân thủ):
    1. Viết "SUMMARY: " theo sau là các gạch đầu dòng tóm tắt ý chính (để dùng làm checklist chấm điểm).
    2. Không được viết thêm lời dẫn, không markdown.

    --- VÍ DỤ ĐỊNH DẠNG MẪU (CHỈ THAM KHẢO CẤU TRÚC - KHÔNG COPY NỘI DUNG) ---
    SUMMARY: 
    - [Khái niệm quan trọng A]: [Định nghĩa ngắn gọn]
    - [Quy trình/Các bước]: [Bước 1] -> [Bước 2] -> [Bước 3]
    -------------------------------------------------------------------------
    
    ---NỘI DUNG CẦN XỬ LÝ---
    {full_text}
    """

    try:
        # 1. Gọi LLM lấy raw text
        raw_text = await _call_llm(prompt)
        raw_text = raw_text.strip()
        
        # 2. Tìm phần SUMMARY
        summary_match = re.search(r'SUMMARY:\s*(.+)', raw_text, re.IGNORECASE | re.DOTALL)
        if summary_match:
            summary = summary_match.group(1).strip()
        else:
            # Fallback
            summary = raw_text

        return summary

    except Exception as e:
        print(f"❌ AI Service Error ({PROVIDER}): {e}")
        # Lưu ý: Sửa lại return 1 biến string để tránh lỗi unpack tuple nếu bạn gọi hàm này ở nơi khác
        return "Error: Không thể tóm tắt nội dung lúc này."