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
    
def clean_json_string(raw_str: str) -> str:
    match = re.search(r'\{.*\}', raw_str, re.DOTALL)
    return match.group(0) if match else "{}"

async def evaluate_speech_with_ai(context: str, user_speech: str, mode: str):
# 1. Định nghĩa Luật chấm điểm (Rubric) và Giọng điệu (Tone) riêng biệt
    if mode == "exam":
        mode_rules = """
        CHẾ ĐỘ: EXAM (THI THỬ) - ĐÁNH GIÁ CỰC KỲ KHẮT KHE, CHUẨN CHUYÊN NGHIỆP.
        
        [HƯỚNG DẪN CHẤM ĐIỂM]
        - accuracy (Độ chính xác): Yêu cầu tuyệt đối. Phải bao phủ 100% ý chính. Thiếu ý hoặc nói sai kiến thức -> Trừ thẳng tay xuống dưới 5 điểm.
        - fluency (Trôi chảy): Giọng điệu phải ngắt nghỉ chuẩn xác. Vấp váp nhẹ -> Trừ 2 điểm.
        - repetition (Lặp từ): KHÔNG KHOAN NHƯỢNG. Có xuất hiện "ờ", "à", "thì", "mà", "kiểu như" -> Chấm dưới 8 điểm ngay lập tức.
        - structure (Cấu trúc): Phải có mở bài, chuyển ý, và kết luận rõ ràng. Không có -> Dưới 7 điểm.
        
        [HƯỚNG DẪN NHẬN XÉT]
        - Giọng văn: Nghiêm khắc, trực diện, chuyên nghiệp như một giám khảo khó tính.
        - Nội dung: Nêu rõ lỗi sai kiến thức (nếu có). Chỉ ra 3 điểm mạnh, 3 điểm yếu, và cách khắc phục để đạt chuẩn báo cáo doanh nghiệp.
        """
    else:
        mode_rules = """
        CHẾ ĐỘ: PRACTICE (TỰ LUYỆN) - ĐÁNH GIÁ NHẸ NHÀNG, MANG TÍNH KHÍCH LỆ.
        
        [HƯỚNG DẪN CHẤM ĐIỂM]
        - accuracy (Độ chính xác): Thí sinh chỉ cần nắm được 60-70% ý chính hoặc từ khóa là có thể cho 7-8 điểm. Bỏ qua các lỗi sai nhỏ.
        - fluency (Trôi chảy): Khuyến khích sự tự tin. Dù có vấp váp nhưng vẫn nói hết câu -> Vẫn cho điểm khá (7-8 điểm).
        - repetition (Lặp từ): Chấp nhận các từ thừa (ờ, à) ở mức độ vừa phải. Chỉ trừ điểm nhẹ nếu lặp từ quá nhiều gây khó hiểu.
        - structure (Cấu trúc): Có ý thức trình bày tuần tự là đạt yêu cầu.
        
        [HƯỚNG DẪN NHẬN XÉT]
        - Giọng văn: Ấm áp, khích lệ, động viên (Dùng phương pháp Khen ngợi -> Góp ý -> Động viên).
        - Nội dung: Tìm ra 3 điểm mạnh để khen, 3 điểm cần cải thiện nhẹ nhàng, và gợi ý mẹo nhỏ để lần sau làm tốt hơn.
        """

    # 2. Ráp vào Prompt tổng
    prompt = f"""
    Bạn là Giám khảo AI chấm thi thuyết trình. Bạn sẽ so sánh "Bài nói của thí sinh" với "Tài liệu gốc" để cho điểm.
    
    {mode_rules}
    
    TÀI LIỆU GỐC (ĐÁP ÁN CHUẨN):
    {context}
    
    BÀI NÓI CỦA THÍ SINH:
    "{user_speech}"
    
    YÊU CẦU OUTPUT:
    Chỉ trả về 1 chuỗi JSON hợp lệ, KHÔNG bọc trong markdown (không dùng ```json), KHÔNG viết thêm lời giải thích.
    
    CẤU TRÚC JSON BẮT BUỘC:
    {{
        "overall_score": [Điểm tổng, thang 1-10],
        "criteria_scores": {{
            "accuracy": [Điểm chính xác, thang 1-10],
            "fluency": [Điểm lưu loát, thang 1-10],
            "repetition": [Điểm lặp từ, thang 1-10],
            "structure": [Điểm cấu trúc, thang 1-10]
        }},
        "feedback": "Phần nhận xét chi tiết (Gồm: Điểm mạnh, Điểm yếu, Gợi ý cải thiện - viết gộp chung trong 1 chuỗi này, dùng \\n để xuống dòng nếu cần)."
    }}
    """

    try:
        # 1. Gọi hàm _call_llm chung thay vì httpx (Nó sẽ tự chọn Provider theo .env)
        raw_text = await _call_llm(prompt)
        
        # 2. Dùng hàm Regex của Trân để cắt đúng phần JSON (bỏ mấy câu như "Here is your JSON...")
        clean_json = clean_json_string(raw_text)
        
        # 3. Parse thành Dictionary của Python
        parsed_data = json.loads(clean_json)
        return parsed_data
        
    except json.JSONDecodeError as e:
        print(f"❌ Lỗi Parse JSON do AI trả về sai định dạng: {e}")
        # In raw_text ra để xem AI nó lỡ nói bậy cái gì
        print(f"Raw Text: {raw_text}") 
        return None
    except Exception as e:
        print(f"❌ Lỗi gọi AI ({PROVIDER}): {e}")
        return None