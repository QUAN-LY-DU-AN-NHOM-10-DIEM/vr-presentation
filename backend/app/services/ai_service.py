import json
import os
import re
import httpx
from dotenv import load_dotenv

# Import các SDK
from openai import AsyncOpenAI, AsyncAzureOpenAI
import google.genai as genai

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

async def clean_and_summarize(full_text: str) -> dict:
    """
    Tóm tắt và làm sạch nội dung (Hàm chính)
    """
    prompt = f"""
    Bạn là Giám khảo chấm thi. Hãy đọc tài liệu dưới đây và trích xuất ý chính.

    NHIỆM VỤ:
    Bạn cần tạo ra 3 thông tin từ tài liệu: tiêu đề (title), mô tả ngắn (description), và tóm tắt ý chính (context_text).

    YÊU CẦU CHO PHẦN TÓM TẮT (context_text):
    1. Viết các gạch đầu dòng tóm tắt ý chính (để dùng làm checklist chấm điểm).
    2. Không được viết thêm lời dẫn, không markdown.

    --- VÍ DỤ ĐỊNH DẠNG MẪU CHO PHẦN TÓM TẮT (CHỈ THAM KHẢO CẤU TRÚC - KHÔNG COPY NỘI DUNG) ---
    - [Khái niệm quan trọng A]: [Định nghĩa ngắn gọn]
    - [Quy trình/Các bước]: [Bước 1] -> [Bước 2] -> [Bước 3]
    -------------------------------------------------------------------------

    YÊU CẦU OUTPUT (Bắt buộc tuân thủ):
    Trả về kết quả dưới định dạng JSON hợp lệ. KHÔNG dùng markdown block (không bọc trong ```json), KHÔNG viết thêm bất kỳ lời dẫn nào.
    
    Cấu trúc JSON bắt buộc:
    {{
        "title": "[Tạo một tiêu đề ngắn gọn cho tài liệu]",
        "description": "[Tạo mô tả tóm tắt tài liệu trong 1-2 câu]",
        "context_text": "[BẮT BUỘC LÀ 1 CHUỖI STRING DUY NHẤT. Dùng ký tự \\n để xuống dòng. Điền toàn bộ nội dung tóm tắt checklist với các gạch đầu dòng theo đúng format ví dụ ở trên]"
    }}

    ---NỘI DUNG CẦN XỬ LÝ---
    {full_text}
    """

    try:
        # 1. Gọi LLM lấy raw text
        raw_text = await _call_llm(prompt)
        raw_text = raw_text.strip()
        
        # 2. Xử lý an toàn: Tìm chuỗi JSON
        json_match = re.search(r'\{.*\}', raw_text, re.DOTALL)
        
        if json_match:
            json_str = json_match.group(0)
            data = json.loads(json_str)
            
            # --- FIX: Xử lý mảng thành chuỗi ---
            context_data = data.get("context_text", raw_text)
            if isinstance(context_data, list):
                # Nếu AI trả về list, nối các phần tử lại bằng dấu xuống dòng
                final_context = "\n".join(str(item) for item in context_data)
            else:
                final_context = str(context_data)
            # ------------------------------------
            
            return {
                "title": str(data.get("title", "Untitled Presentation")),
                "description": str(data.get("description", "")),
                "context_text": final_context
            }
        else:
            return {
                "title": "Untitled Presentation",
                "description": "Failed to parse structured data.",
                "context_text": raw_text
            }

    except Exception as e:
        print(f"❌ AI Service Error: {e}")
        return {
            "title": "Error Processing AI",
            "description": "An error occurred.",
            "context_text": "Error: Không thể tóm tắt nội dung lúc này."
        }