import json
import os
import re
import httpx
from typing import List, Optional, Dict
from dotenv import load_dotenv

from app.schemas import KeywordStatus, QAItemEvaluation

# Import các SDK
from openai import AsyncOpenAI, AsyncAzureOpenAI
from google import genai

load_dotenv()

PROVIDER = os.getenv("LLM_PROVIDER", "ollama").lower()
MODEL_NAME = os.getenv("MODEL_NAME", "qwen2.5:7b")


def _extract_json(text: str) -> Optional[dict]:
    """Extract JSON from LLM response, handling various formats."""
    try:
        # Try direct JSON parse first
        return json.loads(text)
    except json.JSONDecodeError:
        # Try to find JSON in markdown code block
        match = re.search(r"```(?:json)?\s*(\{.*?\})\s*```", text, re.DOTALL)
        if match:
            try:
                return json.loads(match.group(1))
            except json.JSONDecodeError:
                pass
        # Try to find any JSON object in text
        match = re.search(r"\{.*\}", text, re.DOTALL)
        if match:
            try:
                return json.loads(match.group(0))
            except json.JSONDecodeError:
                pass
    return None


async def _call_llm(prompt: str) -> str:
    """Hàm nội bộ để gọi API dựa trên Provider được cấu hình"""

    def _get_content(content) -> str:
        """Extract string content safely"""
        if content is None:
            return ""
        if hasattr(content, "content"):
            c = content.content
        else:
            c = content
        if c is None:
            return ""
        return str(c).strip()

    if PROVIDER == "openai":
        client = AsyncOpenAI(api_key=os.getenv("OPENAI_API_KEY"))
        response = await client.chat.completions.create(
            model=MODEL_NAME,
            messages=[{"role": "user", "content": prompt}],
            temperature=0.3,
        )
        content = _get_content(response.choices[0].message.content)
        print(f"[OpenAI] Raw response: {content[:500]}...")
        if not content:
            raise ValueError("OpenAI returned empty response")
        return content

    elif PROVIDER == "deepseek":
        client = AsyncOpenAI(
            api_key=os.getenv("DEEPSEEK_API_KEY"),
            base_url="https://api.deepseek.com/v1",
        )
        response = await client.chat.completions.create(
            model=MODEL_NAME,
            messages=[{"role": "user", "content": prompt}],
            temperature=0.3,
        )
        content = _get_content(response.choices[0].message.content)
        print(f"[DeepSeek] Raw response: {content[:500]}...")
        if not content:
            raise ValueError("DeepSeek returned empty response")
        return content

    elif PROVIDER == "groq":
        client = AsyncOpenAI(
            api_key=os.getenv("GROQ_API_KEY"), base_url="https://api.groq.com/openai/v1"
        )
        response = await client.chat.completions.create(
            model=MODEL_NAME,
            messages=[{"role": "user", "content": prompt}],
            temperature=0.3,
        )
        content = _get_content(response.choices[0].message.content)
        print(f"[Groq] Raw response: {content[:500]}...")
        if not content:
            raise ValueError("Groq returned empty response")
        return content

    elif PROVIDER == "azure":
        client = AsyncAzureOpenAI(
            azure_endpoint=os.getenv("AZURE_OPENAI_ENDPOINT"),
            api_key=os.getenv("AZURE_OPENAI_API_KEY"),
            api_version=os.getenv("AZURE_OPENAI_API_VERSION"),
        )
        response = await client.chat.completions.create(
            model=MODEL_NAME,
            messages=[{"role": "user", "content": prompt}],
            temperature=0.3,
        )
        content = _get_content(response.choices[0].message.content)
        print(f"[Azure] Raw response: {content[:500]}...")
        if not content:
            raise ValueError("Azure returned empty response")
        return content

    elif PROVIDER == "gemini":
        client = genai.Client(api_key=os.getenv("GEMINI_API_KEY"))
        response = client.models.generate_content(
            model=MODEL_NAME,
            contents=prompt,
        )
        content = _get_content(response.text)
        print(f"[Gemini] Raw response: {content[:500]}...")
        if not content:
            raise ValueError("Gemini returned empty response")
        return content

    elif PROVIDER == "ollama":
        ollama_url = os.getenv("OLLAMA_URL", "http://localhost:11434/api/generate")
        payload = {"model": MODEL_NAME, "prompt": prompt, "stream": False}
        async with httpx.AsyncClient(timeout=120.0) as client:
            response = await client.post(ollama_url, json=payload)
            response.raise_for_status()
            result = response.json()
            content = _get_content(result.get("response", ""))
            print(f"[Ollama] Raw response: {content[:500]}...")
            if not content:
                raise ValueError("Ollama returned empty response")
            return content

    else:
        raise ValueError(f"Provider '{PROVIDER}' không được hỗ trợ.")


async def clean_and_summarize(full_text: str) -> dict:
    """
    Tóm tắt và làm sạch nội dung với cấu trúc JSON strict để dễ parse.
    """
    prompt = f"""Bạn là một Chuyên gia phân tích và đánh giá tài liệu cấp cao.
Nhiệm vụ của bạn là đọc tài liệu dưới đây và trích xuất thông tin trọng tâm để Hội đồng giám khảo có thể dùng làm cơ sở chấm điểm bài thuyết trình.

YÊU CẦU BẮT BUỘC:
1. GIỮ NGUYÊN ngôn ngữ gốc của tài liệu (không dịch).
2. BẢO TOÀN toàn bộ số liệu, facts, và thuật ngữ chuyên ngành quan trọng.
3. KHÔNG viết lan man, chỉ trích xuất đúng trọng tâm.
4. Output PHẢI là một chuỗi JSON hợp lệ. TUYỆT ĐỐI KHÔNG bọc trong code block (như ```json), KHÔNG có text thừa ở trước hay sau JSON.

CẤU TRÚC JSON YÊU CẦU:
{{
    "title": "Tiêu đề ngắn gọn của tài liệu (tối đa 15 từ)",
    "description": "Mô tả tổng quan tài liệu gồm những phần chính nào (1-2 câu)",
    "context": [
        "Ý chính 1 (chứa facts/số liệu)",
        "Ý chính 2",
        "Ý chính 3"
    ]
}}

---TÀI LIỆU CẦN PHÂN TÍCH---
{full_text}
"""

    try:
        raw_text = await _call_llm(prompt)
        print(f"[clean_and_summarize] Raw response:\n{raw_text[:500]}...")

        # 1. Ưu tiên dùng hàm extract JSON
        parsed_data = _extract_json(raw_text)

        if parsed_data and isinstance(parsed_data, dict):
            # Xử lý context: LLM có thể trả về list hoặc string
            context_data = parsed_data.get("context", [])
            if isinstance(context_data, list):
                context_text = "\n".join([f"- {item}" for item in context_data])
            else:
                context_text = str(context_data)

            return {
                "title": parsed_data.get("title", "Untitled").strip(),
                "description": parsed_data.get("description", "").strip(),
                "context_text": context_text.strip(),
            }

        # 2. Fallback: Parse bằng Regex nếu JSON hỏng nặng
        print("[clean_and_summarize] JSON parse failed, falling back to Regex...")
        title_m = re.search(
            r"(?:\"title\"|Title)[\s:\"\']*([^\"\n]+)", raw_text, re.IGNORECASE
        )
        desc_m = re.search(
            r"(?:\"description\"|Description)[\s:\"\']*([^\"\n]+)",
            raw_text,
            re.IGNORECASE,
        )
        ctx_m = re.search(
            r"(?:\"context\"|Context)[^\n]*\n((?:[^\n]+\n?)+)", raw_text, re.IGNORECASE
        )

        title = title_m.group(1).strip() if title_m else "Untitled"
        description = desc_m.group(1).strip() if desc_m else ""
        context = ctx_m.group(1).strip() if ctx_m else raw_text

        # Dọn dẹp lại context rác do regex bắt nhầm ngoặc vuông/nhọn
        context = re.sub(r"[\{\}\[\]\"]", "", context).strip()

        return {"title": title, "description": description, "context_text": context}

    except Exception as e:
        print(f"❌ AI Service Error: {e}")
        return {
            "title": "Lỗi xử lý tài liệu",
            "description": f"Hệ thống không thể xử lý: {str(e)}",
            "context_text": "Vui lòng kiểm tra lại kết nối AI.",
        }


async def generate_questions_with_ai(context: str, user_speech: str, mode: str) -> list:
    """
    Sinh câu hỏi phản biện dựa trên bối cảnh và lời thoại thực tế, xuất ra JSON mảng chuẩn.
    """
    if mode == "exam":
        persona = """
ĐÓNG VAI: Bạn là một HỘI ĐỒNG GIÁM KHẢO KHÓ TÍNH VÀ SẮC BÉN trong một buổi bảo vệ.
MỤC TIÊU: Đặt câu hỏi chất vấn, vạch lá tìm sâu, yêu cầu người trình bày phải giải thích rõ nguyên lý, dẫn chứng số liệu và bảo vệ được quan điểm của mình.
GIỌNG ĐIỆU: Chuyên nghiệp, nghiêm khắc, trực diện. Xưng hô: "bạn" hoặc "em".
"""
    else:
        persona = """
ĐÓNG VAI: Bạn là một MENTOR THÂN THIỆN VÀ TÂM HUYẾT.
MỤC TIÊU: Đặt câu hỏi mở để dẫn dắt, gợi ý người trình bày tự nhận ra vấn đề và đào sâu hơn vào mảng kiến thức cốt lõi.
GIỌNG ĐIỆU: Gợi mở, khích lệ, mang tính xây dựng. Xưng hô: "bạn" hoặc "em".
"""

    prompt = f"""{persona}

TÀI LIỆU GỐC (SỰ THẬT CHUẨN XÁC DÙNG ĐỂ ĐỐI CHIẾU):
{context}

PHẦN TRÌNH BÀY CỦA NGƯỜI DÙNG (Nhận diện từ giọng nói, có thể chứa lỗi STT):
"{user_speech}"

NHIỆM VỤ:
Tạo ra đúng 10 câu hỏi dựa trên TÀI LIỆU GỐC. Dùng PHẦN TRÌNH BÀY để biết người dùng đang nói tới đoạn nào, nhưng BÁM SÁT SỰ THẬT vào TÀI LIỆU GỐC. 

QUY TẮC:
1. Câu hỏi phải ngắn gọn (dưới 30 từ), đánh thẳng vào vấn đề.
2. Tuyệt đối KHÔNG hỏi dạng Có/Không.
3. Nếu phát hiện từ lạ do lỗi nhận diện giọng nói, hãy tự động suy luận khái niệm đúng từ TÀI LIỆU GỐC.
4. Output PHẢI là một chuỗi JSON hợp lệ với format bên dưới. KHÔNG dùng markdown code block, KHÔNG text thừa.

ĐỊNH DẠNG JSON BẮT BUỘC:
{{
    "questions": [
        "Nội dung câu hỏi 1",
        "Nội dung câu hỏi 2",
        "Nội dung câu hỏi 3",
        "...",
        "Nội dung câu hỏi 10"
    ]
}}
"""

    try:
        raw_text = await _call_llm(prompt)
        print(f"[generate_questions] Raw response:\n{raw_text[:500]}...")

        # 1. Parse bằng hàm JSON extraction
        parsed_data = _extract_json(raw_text)
        if parsed_data and isinstance(parsed_data, dict) and "questions" in parsed_data:
            questions = parsed_data["questions"]
            if isinstance(questions, list) and len(questions) > 0:
                return [str(q).strip() for q in questions[:10]]

        # 2. Fallback 1: Nếu parse ra list trực tiếp (phòng khi LLM phớt lờ object wrap)
        if parsed_data and isinstance(parsed_data, list):
            return [str(q).strip() for q in parsed_data[:10]]

        # 3. Fallback 2: Parse bằng Regex truy bắt list format
        print(
            "[generate_questions] JSON parse failed, falling back to Regex arrays/lists..."
        )

        # Bắt các câu nằm trong dấu ngoặc kép của một mảng giả định
        quoted_items = re.findall(r'"([^"\\]*(?:\\.[^"\\]*)*)"', raw_text)
        if len(quoted_items) >= 5:  # Bỏ qua key "questions" nếu có
            filtered_items = [
                q for q in quoted_items if q.lower() != "questions" and len(q) > 10
            ]
            if filtered_items:
                return filtered_items[:10]

        # Bắt danh sách đánh số: 1. Câu hỏi, 2) Câu hỏi...
        numbered_items = re.findall(
            r"(?:^\d+[\.\)]\s*|- \s*)(.+?)(?:\n|$)", raw_text, re.MULTILINE
        )
        if len(numbered_items) >= 3:
            return [q.strip() for q in numbered_items[:10]]

        print(f"❌ Cảnh báo: AI không trả về format hợp lệ có thể parse được.")
        return []

    except Exception as e:
        print(f"❌ Lỗi AI sinh câu hỏi: {e}")
        return []

import re
import json
from typing import Dict, List, Any

# Giả định bạn đã có class KeywordStatus và QAItemEvaluation (nếu dùng Pydantic)
# Nếu dùng dict thông thường, bạn có thể bỏ qua việc instantiate các class này.

async def evaluate_ac1(context: str, answer_text: str) -> Dict[str, Any]:
    """
    AC1: Đánh giá tỷ lệ bám sát nội dung/từ khóa gốc.
    """
    prompt = f"""Bạn là một Giám khảo chấm thi chuyên môn cao.
Nhiệm vụ của bạn là đối chiếu TÀI LIỆU GỐC với BÀI TRÌNH BÀY của thí sinh để xem thí sinh nắm bắt và truyền đạt được bao nhiêu % lượng thông tin cốt lõi.

QUY TRÌNH ĐÁNH GIÁ:
1. Trích xuất đúng 10-15 TỪ KHÓA hoặc CỤM TỪ cốt lõi nhất từ TÀI LIỆU GỐC.
2. Đối chiếu với BÀI TRÌNH BÀY để gán trạng thái cho TỪNG từ khóa:
   - "found": Từ/cụm từ xuất hiện chính xác.
   - "paraphrased": Thí sinh có nói ý nghĩa tương đương.
   - "missing": Thí sinh bỏ sót.
3. Tính tỷ lệ (coverage_percent) và điểm (score).

YÊU CẦU ĐẦU RA BẮT BUỘC (CHỈ XUẤT JSON, KHÔNG CÓ BẤT KỲ TEXT NÀO KHÁC):
(LƯU Ý: ĐÂY CHỈ LÀ CẤU TRÚC, BẠN PHẢI TỰ ĐIỀN DỮ LIỆU THẬT TỪ TÀI LIỆU, TUYỆT ĐỐI KHÔNG COPY LẠI CÁC CHỮ TRONG NGOẶC KÉP DƯỚI ĐÂY)
{{
    "keywords": [
        {{"keyword": "<trích_xuất_từ_khóa_1>", "status": "found"}},
        {{"keyword": "<trích_xuất_từ_khóa_2>", "status": "paraphrased"}},
        {{"keyword": "<trích_xuất_từ_khóa_3>", "status": "missing"}}
    ],
    "coverage_percent": <điền_số_từ_0_đến_100>,
    "score": <điền_số_từ_0_đến_100>,
    "feedback": "<tự_viết_nhận_xét_ngắn_gọn_của_bạn>"
}}

--- BẮT ĐẦU PHÂN TÍCH ---
TÀI LIỆU GỐC:
{context}

BÀI TRÌNH BÀY CỦA THÍ SINH:
{answer_text}
"""
    try:
        raw = await _call_llm(prompt)
        print(f"[evaluate_ac1] Raw response:\n{raw[:500]}...")
        
        data = _extract_json(raw)
        if data and isinstance(data, dict):
             # Map sang KeywordStatus nếu bạn đang dùng Pydantic/Dataclass
            keywords = data.get("keywords", [])
            return {
                "score": float(data.get("score", 50.0)),
                "coverage_percent": float(data.get("coverage_percent", 50.0)),
                "feedback": data.get("feedback", "Đã đánh giá mức độ bám sát nội dung."),
                "keywords": keywords,
            }

        raise ValueError("Không parse được JSON hợp lệ từ LLM.")
    except Exception as e:
        print(f"❌ AC1 Error: {e}")
        return {
            "score": 50.0, 
            "coverage_percent": 50.0,
            "feedback": f"Lỗi đánh giá AC1: {str(e)}", 
            "keywords": []
        }


async def evaluate_ac2(answer_text: str, total_words: int) -> Dict[str, Any]:
    """
    AC2: Đánh giá bố cục trình bày (Mở bài, Thân bài, Kết bài).
    Cải tiến: Dùng Regex word boundary (\b) để bắt từ chính xác hơn, bỏ qua dấu câu.
    """
    # Xóa dấu câu, chuyển chữ thường để chuẩn hóa
    clean_text = re.sub(r'[^\w\s]', ' ', answer_text.lower())
    words = clean_text.split()
    actual_total = len(words)

    if actual_total < 10:
        return {
            "score": 0.0,
            "has_intro": False,
            "has_closing": False,
            "feedback": "Bài trình bày quá ngắn để đánh giá bố cục."
        }

    intro_patterns = [r"\bxin chào\b", r"\bkính thưa\b", r"\bem xin\b", r"\btôi xin\b", 
                      r"\bbắt đầu\b", r"\bhôm nay\b", r"\bchủ đề\b", r"\bbáo cáo\b"]
    closing_patterns = [r"\btóm tắt\b", r"\bkết luận\b", r"\bcảm ơn\b", r"\bhết\b", 
                        r"\bcâu hỏi\b", r"\bq a\b", r"\bxin phép\b", r"\bkết thúc\b"]

    # Quét trong 30% đầu cho Mở bài và 30% cuối cho Kết bài (tối đa 200 từ để tránh lỗi bài quá dài)
    intro_limit = min(int(actual_total * 0.3), 200)
    closing_limit = max(int(actual_total * 0.7), actual_total - 200)

    intro_text = " ".join(words[:intro_limit])
    closing_text = " ".join(words[closing_limit:])

    has_intro = any(re.search(pattern, intro_text) for pattern in intro_patterns)
    has_closing = any(re.search(pattern, closing_text) for pattern in closing_patterns)

    score = 100.0
    if not has_intro:
        score -= 20.0 # Thiếu mở bài trừ nặng hơn
    if not has_closing:
        score -= 20.0

    feedback_parts = []
    feedback_parts.append("Có mở bài rõ ràng" if has_intro else "Thiếu phần chào hỏi/mở bài")
    feedback_parts.append("Có kết bài/cảm ơn" if has_closing else "Thiếu phần kết luận/cảm ơn")

    return {
        "score": score,
        "has_intro": has_intro,
        "has_closing": has_closing,
        "feedback": " và ".join(feedback_parts).capitalize() + ".",
    }


async def evaluate_ac3(questions: Dict) -> Dict[str, Any]:
    """
    AC3: Đánh giá tổng hợp phần Q&A.
    """
    detailed = []
    total_score = 0.0
    count = 0

    for qid, qdata in questions.items():
        question = qdata.get("question", "")
        answer = qdata.get("answer", "")

        score_result = await evaluate_qa_item(question, answer)
        
        # Nếu dùng Pydantic model QAItemEvaluation, bạn wrap lại ở đây
        item_eval = {
            "question_id": int(qid),
            "score": score_result["score"],
            "feedback": score_result["feedback"],
            "content_match_percent": score_result["content_match"],
        }
        detailed.append(item_eval)
        
        total_score += score_result["score"]
        count += 1

    avg_score = round(total_score / count, 1) if count > 0 else 0.0

    return {
        "avg_score": avg_score,
        "detailed": detailed,
        "feedback": f"Đã đánh giá {count} câu hỏi Q&A. Điểm trung bình phần này đạt {avg_score}/100.",
    }


async def evaluate_qa_item(question: str, answer: str) -> Dict[str, Any]:
    """
    Đánh giá chi tiết 1 cặp Câu Hỏi - Trả Lời.
    """
    if not answer or len(answer.split()) < 3:
        return {
            "score": 0.0,
            "content_match": 0.0,
            "feedback": "Câu trả lời quá ngắn hoặc không có nội dung.",
        }

    prompt = f"""Bạn là Giám khảo vấn đáp chuyên môn.
Hãy đánh giá mức độ chính xác của CÂU TRẢ LỜI so với CÂU HỎI. 

TIÊU CHÍ ĐÁNH GIÁ:
- 90-100đ: Trả lời đúng, đủ, không dư thừa (Khớp >=80% nội dung).
- 70-89đ: Trả lời được ý chính, hơi thiếu sót (Khớp 50-79%).
- 50-69đ: Chỉ trả lời được một phần nhỏ (Khớp 20-49%).
- 10-49đ: Lạc đề, sai hoàn toàn (Khớp < 20%).

YÊU CẦU ĐẦU RA BẮT BUỘC (CHỈ XUẤT JSON):
(LƯU Ý: TUYỆT ĐỐI KHÔNG COPY VÍ DỤ NÀY, HÃY TỰ ĐÁNH GIÁ VÀ ĐIỀN SỐ THỰC TẾ)
{{
    "content_match": <điền_phần_trăm_khớp_vào_đây>,
    "score": <điền_điểm_vào_đây>,
    "feedback": "<viết_lý_do_chấm_điểm_vào_đây>"
}}

--- BẮT ĐẦU PHÂN TÍCH ---
CÂU HỎI: "{question}"
CÂU TRẢ LỜI: "{answer}"
"""
    try:
        raw = await _call_llm(prompt)
        data = _extract_json(raw)
        
        if data and isinstance(data, dict):
            return {
                "score": float(data.get("score", 50.0)),
                "content_match": float(data.get("content_match", 0.0)),
                "feedback": data.get("feedback", "Đánh giá hoàn tất.").strip(),
            }
            
        raise ValueError("Không parse được JSON từ AI.")
    except Exception as e:
        print(f"❌ QA Evaluation Error: {e}")
        return {
            "score": 50.0, 
            "content_match": 0.0, 
            "feedback": f"Lỗi hệ thống khi đánh giá: {str(e)}"
        }
