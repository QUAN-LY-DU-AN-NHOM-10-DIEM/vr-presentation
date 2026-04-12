import json
import os
import re
import httpx
from dotenv import load_dotenv

# Import các SDK
from openai import AsyncOpenAI, AsyncAzureOpenAI
from google import genai

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
        # Khởi tạo client theo chuẩn SDK mới
        client = genai.Client(api_key=os.getenv("GEMINI_API_KEY"))
        
        # Gọi API tạo nội dung
        response = client.models.generate_content(
            model=MODEL_NAME, # vd: gemini-2.5-flash
            contents=prompt
        )
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
    
async def generate_questions_with_ai(context: str, user_speech: str, mode: str) -> list:
    # 1. Setup Nhân cách (Persona) và Xưng hô
    if mode == "exam":
        persona = """
        ĐÓNG VAI: Bạn là HỘI ĐỒNG GIÁM KHẢO KHÓ TÍNH đang ngồi trực tiếp đối diện người thuyết trình.
        THÁI ĐỘ: Đặt câu hỏi chất vấn sắc bén, phản biện, vạch lá tìm sâu, yêu cầu làm rõ số liệu/dẫn chứng. Giọng văn đanh thép, nghi ngờ.
        XƯNG HÔ: Trực tiếp gọi người đối diện là "bạn" hoặc "em". (Ví dụ: "Cơ sở nào để em khẳng định...", "Tại sao bạn lại cho rằng...")
        """
    else:
        persona = """
        ĐÓNG VAI: Bạn là MENTOR THÂN THIỆN đang ngồi dưới ghế khán giả nghe thuyết trình.
        THÁI ĐỘ: Đặt câu hỏi mở, gợi ý để giúp người nói khai triển thêm ý tưởng, giải thích sâu hơn. Giọng văn tò mò, khích lệ.
        XƯNG HÔ: Trực tiếp gọi người đối diện là "bạn". (Ví dụ: "Mình thấy ý này rất thú vị, bạn có thể chia sẻ thêm...", "Theo bạn thì...")
        """

    # 2. Xây dựng Prompt
    prompt = f"""
    {persona}
    
    TÀI LIỆU GỐC CỦA BÀI THUYẾT TRÌNH:
    {context}
    
    NGƯỜI TRÌNH BÀY VỪA NÓI ĐOẠN SAU:
    "{user_speech}"
    
    NHIỆM VỤ:
    Dựa vào tài liệu gốc và nội dung người trình bày vừa nói, hãy ĐẶT TRỰC TIẾP 10 CÂU HỎI cho họ.
    
    QUY TẮC BẮT BUỘC (TUÂN THỦ NGHIÊM NGẶT):
    1. HỎI TRỰC TIẾP: Nói thẳng với người trình bày, tuyệt đối không dùng từ "thí sinh" hay "người trình bày" trong câu hỏi.
    2. NGẮN GỌN: Dưới 30 từ mỗi câu (để hệ thống Text-to-Speech đọc lên tự nhiên nhất).
    3. HỎI SÂU: Tránh câu hỏi Có/Không. Yêu cầu giải thích, phân tích hoặc bảo vệ quan điểm.
    4. BÁM SÁT THỰC TẾ: Câu hỏi phải liên quan chặt chẽ đến đoạn text họ vừa nói.
    
    YÊU CẦU OUTPUT:
    Chỉ trả về 1 mảng JSON thuần túy chứa 10 chuỗi string (KHÔNG bọc markdown, KHÔNG viết lời dẫn).
    
    Ví dụ định dạng mong muốn:
    [
        "Bạn lấy số liệu ở đâu để chứng minh cho luận điểm vừa rồi?",
        "Ý tưởng này rất hay, nhưng bạn sẽ giải quyết rủi ro về chi phí như thế nào?",
        "Tại sao bạn lại chọn phương pháp này thay vì các giải pháp truyền thống khác?"
    ]
    """

    try:
        # 1. Gọi LLM lấy raw text
        raw_text = await _call_llm(prompt)
        raw_text = raw_text.strip()
        
        # 2. Xử lý an toàn: Tìm chuỗi JSON dạng mảng (List) bằng Regex
        # Lưu ý: Tìm ngoặc vuông [...] thay vì ngoặc nhọn {...}
        json_match = re.search(r'\[.*\]', raw_text, re.DOTALL)
        
        if json_match:
            json_str = json_match.group(0)
            data = json.loads(json_str)
            
            # --- FIX: Đảm bảo dữ liệu trả về đúng chuẩn List ---
            if isinstance(data, list):
                return data
            
            # Phòng trường hợp AI trả về object chứa mảng (vd: {"questions": [...]})
            elif isinstance(data, dict):
                for key, value in data.items():
                    if isinstance(value, list):
                        return value
            # ------------------------------------
            
            return [] # Parse được JSON nhưng không tìm thấy list
        else:
            print(f"❌ Cảnh báo: AI không trả về format JSON List hợp lệ. Raw text: {raw_text}")
            return []
            
    except Exception as e:
        print(f"❌ Lỗi AI sinh câu hỏi: {e}")
        return []
