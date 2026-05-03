import json
import os
import re
import httpx
from typing import Any, List, Optional, Dict
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
            messages=[
                {
                    "role": "system",
                    "content": "You are a JSON-only API. You must output only a valid JSON object. NEVER output conversational text like 'Here is the result'. Start your response with '{' and end with '}'.",
                },
                {"role": "user", "content": prompt},
            ],
            temperature=0.1,
            max_tokens=2048,  # QUAN TRỌNG: Bơm thêm token để model không bị "hết hơi" giữa chừng
            response_format={"type": "json_object"},
        )
        content = _get_content(response.choices[0].message.content)

        # Mẹo dọn dẹp phòng hờ: Nếu model vẫn lỡ mồm ở đầu, ta cắt bỏ phần text thừa
        if "{" in content:
            content = content[content.find("{") :]

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
    prompt = f"""Bạn là một Chuyên gia phân tích và đánh giá tài liệu cấp cao.
Nhiệm vụ của bạn là đọc tài liệu dưới đây và trích xuất thông tin trọng tâm.

KỶ LUẬT NGÔN NGỮ (TUYỆT ĐỐI TUÂN THỦ):
- BẮT BUỘC phải viết "title", "description" và "context" bằng CHÍNH NGÔN NGỮ CỦA TÀI LIỆU GỐC.
- NẾU TÀI LIỆU LÀ TIẾNG ANH -> BẠN PHẢI TRẢ LỜI BẰNG TIẾNG ANH.
- NẾU TÀI LIỆU LÀ TIẾNG VIỆT -> BẠN TRẢ LỜI BẰNG TIẾNG VIỆT.
- TUYỆT ĐỐI KHÔNG DỊCH tài liệu tiếng Anh sang tiếng Việt. Việc dịch sẽ làm hỏng hệ thống chấm điểm thuật ngữ của chúng tôi.

YÊU CẦU DỮ LIỆU:
1. BẢO TOÀN toàn bộ số liệu, facts, và thuật ngữ chuyên ngành quan trọng.
2. KHÔNG viết lan man, chỉ trích xuất đúng trọng tâm.
3. Output PHẢI là một chuỗi JSON hợp lệ. TUYỆT ĐỐI KHÔNG bọc trong code block (như ```json).

CẤU TRÚC JSON YÊU CẦU:
(LƯU Ý: ĐIỀN NỘI DUNG VÀO DƯỚI ĐÂY BẰNG NGÔN NGỮ CỦA TÀI LIỆU)
{{
    "title": "<điền tiêu đề ngắn gọn>",
    "description": "<điền mô tả tổng quan>",
    "context": [
        "<ý chính 1 chứa số liệu/fact>",
        "<ý chính 2>",
        "<ý chính 3>"
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
    if mode == "exam":
        persona = """ĐÓNG VAI: Bạn là HỘI ĐỒNG GIÁM KHẢO KHÓ TÍNH. Mục tiêu: Đặt câu hỏi chất vấn, vạch lá tìm sâu."""
    else:
        persona = """ĐÓNG VAI: Bạn là MENTOR THÂN THIỆN. Mục tiêu: Đặt câu hỏi mở, gợi mở vấn đề."""

    prompt = f"""{persona}

TÀI LIỆU GỐC (Chuẩn kiến thức):
{context}

PHẦN TRÌNH BÀY CỦA THÍ SINH:
"{user_speech}"

NHIỆM VỤ: Tạo ra 10 câu hỏi dựa trên TÀI LIỆU GỐC.
1. Câu hỏi ngắn (dưới 30 từ), không hỏi Có/Không.
2. TUYỆT ĐỐI KHÔNG dùng dấu nháy kép (") bên trong câu hỏi. Dùng nháy đơn (') nếu cần trích dẫn thuật ngữ.
3. Sử dụng Tiếng Việt để đặt câu hỏi.

KỶ LUẬT ĐẦU RA (CHỈ XUẤT JSON):
Tuyệt đối không giải thích, không dùng markdown. Bạn phải trả về một object JSON có đúng một trường duy nhất tên là "questions". 
Giá trị của "questions" phải là một mảng (array) chứa đúng 10 chuỗi văn bản (10 câu hỏi).

Ví dụ cấu trúc bạn cần tự tưởng tượng: Object -> chứa key "questions" -> chứa Array -> chứa 10 Strings.
"""

    try:
        raw_text = await _call_llm(prompt)
        print(f"[generate_questions] Raw response:\n{raw_text[:500]}...")

        # 1. Thử parse JSON trực tiếp trước (Nếu bạn đã bật JSON mode cho Groq)
        try:
            parsed_data = json.loads(raw_text)
            if "questions" in parsed_data and isinstance(
                parsed_data["questions"], list
            ):
                return [str(q).strip() for q in parsed_data["questions"][:10]]
        except json.JSONDecodeError:
            pass  # Chuyển sang fallback nếu JSON hỏng

        # 2. Parse bằng hàm JSON extraction custom của bạn
        parsed_data = _extract_json(raw_text)
        if parsed_data and isinstance(parsed_data, dict) and "questions" in parsed_data:
            questions = parsed_data["questions"]
            if isinstance(questions, list) and len(questions) > 0:
                return [str(q).strip() for q in questions[:10]]

        if parsed_data and isinstance(parsed_data, list):
            return [str(q).strip() for q in parsed_data[:10]]

        # 3. Fallback Regex (Đã được tinh chỉnh để bỏ qua lỗi lặt vặt)
        print("[generate_questions] JSON parse failed, falling back to Regex...")

        # Bắt danh sách đánh số: 1. Câu hỏi, 2) Câu hỏi... (Ưu tiên regex này hơn vì nó an toàn với nháy kép)
        numbered_items = re.findall(
            r"(?:^\d+[\.\)]\s*|- \s*)(.+?)(?:\n|$)", raw_text, re.MULTILINE
        )
        if len(numbered_items) >= 3:
            return [q.strip().strip('",') for q in numbered_items[:10]]

        # Regex bắt chuỗi trong JSON (Chỉ dùng cuối cùng)
        quoted_items = re.findall(r'"([^"\\]*(?:\\.[^"\\]*)*)"', raw_text)
        filtered_items = [
            q for q in quoted_items if q.lower() != "questions" and len(q) > 15
        ]
        if len(filtered_items) >= 3:
            return filtered_items[:10]

        print(f"❌ Cảnh báo: AI trả về format không hợp lệ.")
        return []

    except Exception as e:
        print(f"❌ Lỗi AI sinh câu hỏi: {e}")
        return []


async def evaluate_ac1(context: str, answer_text: str) -> dict:
    prompt = f"""Bạn là một Giám khảo chấm thi chuyên nghiệp.
Phân tích TÀI LIỆU GỐC và BÀI TRÌNH BÀY để trích xuất từ khóa.

QUY TẮC BẮT BUỘC:
1. Trích xuất đúng 10 TỪ KHÓA cốt lõi từ TÀI LIỆU GỐC.
2. Mỗi từ khóa PHẢI là tiếng Anh, kèm theo bản dịch tiếng Việt trong ngoặc đơn.
   Ví dụ: "Boundary Value Analysis (Phân tích giá trị biên)".
3. Đối chiếu với BÀI TRÌNH BÀY để đánh giá trạng thái.
4. Không bao giờ dùng dấu nháy kép (") bên trong nội dung nhận xét.

KỶ LUẬT ĐẦU RA (CHỈ XUẤT JSON):
Tuyệt đối không giải thích, không dùng markdown. Bạn phải trả về một object JSON chứa đúng 4 trường (keys) sau:
- "keywords": Là một mảng. Mỗi phần tử là một object chứa "keyword" (tên từ khóa tiếng Anh kèm dịch tiếng Việt) và "status" (chỉ được chọn 1 trong 3 giá trị: "found", "paraphrased", hoặc "missing").
- "coverage_percent": Số thực từ 0 đến 100, thể hiện tỷ lệ % bám sát.
- "score": Số thực từ 0 đến 100, điểm số tương ứng.
- "feedback": Chuỗi văn bản, nhận xét ngắn gọn của bạn.

--- BẮT ĐẦU ---
TÀI LIỆU GỐC:
{context}

BÀI TRÌNH BÀY CỦA THÍ SINH:
{answer_text}
"""
    try:
        raw = await _call_llm(prompt)

        # Do ta đã dùng mẹo cắt chuỗi ở hàm _call_llm, raw text bây giờ chắc chắn bắt đầu bằng {
        data = json.loads(raw)

        keywords = data.get("keywords", [])
        return {
            "score": float(data.get("score", 50.0)),
            "coverage_percent": float(data.get("coverage_percent", 50.0)),
            "feedback": data.get("feedback", "Đã đánh giá mức độ bám sát nội dung."),
            "keywords": keywords,
        }

    except json.JSONDecodeError as e:
        print(f"[evaluate_ac1] json.loads failed: {e}. Fallback to _extract_json")
        data = _extract_json(raw)
        if data and isinstance(data, dict):
            return {
                "score": float(data.get("score", 50.0)),
                "coverage_percent": float(data.get("coverage_percent", 50.0)),
                "feedback": data.get("feedback", ""),
                "keywords": data.get("keywords", []),
            }
        return {
            "score": 50.0,
            "coverage_percent": 50.0,
            "feedback": "Lỗi format JSON do bị cắt đứt giữa chừng",
            "keywords": [],
        }
    except Exception as e:
        print(f"❌ AC1 Error: {e}")
        return {
            "score": 50.0,
            "coverage_percent": 50.0,
            "feedback": f"Lỗi hệ thống: {str(e)}",
            "keywords": [],
        }


async def evaluate_ac2(answer_text: str, total_words: int) -> Dict[str, Any]:
    """
    AC2: Đánh giá bố cục trình bày (Mở bài, Thân bài, Kết bài).
    Cải tiến: Dùng Regex word boundary (\b) để bắt từ chính xác hơn, bỏ qua dấu câu.
    """
    # Xóa dấu câu, chuyển chữ thường để chuẩn hóa
    clean_text = re.sub(r"[^\w\s]", " ", answer_text.lower())
    words = clean_text.split()
    actual_total = len(words)

    if actual_total < 10:
        return {
            "score": 0.0,
            "has_intro": False,
            "has_closing": False,
            "feedback": "Bài trình bày quá ngắn để đánh giá bố cục.",
        }

    intro_patterns = [
        r"\bxin chào\b",
        r"\bkính thưa\b",
        r"\bem xin\b",
        r"\btôi xin\b",
        r"\bbắt đầu\b",
        r"\bhôm nay\b",
        r"\bchủ đề\b",
        r"\bbáo cáo\b",
    ]
    closing_patterns = [
        r"\btóm tắt\b",
        r"\bkết luận\b",
        r"\bcảm ơn\b",
        r"\bhết\b",
        r"\bcâu hỏi\b",
        r"\bq a\b",
        r"\bxin phép\b",
        r"\bkết thúc\b",
    ]

    # Quét trong 30% đầu cho Mở bài và 30% cuối cho Kết bài (tối đa 200 từ để tránh lỗi bài quá dài)
    intro_limit = min(int(actual_total * 0.3), 200)
    closing_limit = max(int(actual_total * 0.7), actual_total - 200)

    intro_text = " ".join(words[:intro_limit])
    closing_text = " ".join(words[closing_limit:])

    has_intro = any(re.search(pattern, intro_text) for pattern in intro_patterns)
    has_closing = any(re.search(pattern, closing_text) for pattern in closing_patterns)

    score = 100.0
    if not has_intro:
        score -= 20.0  # Thiếu mở bài trừ nặng hơn
    if not has_closing:
        score -= 20.0

    feedback_parts = []
    feedback_parts.append(
        "Có mở bài rõ ràng" if has_intro else "Thiếu phần chào hỏi/mở bài"
    )
    feedback_parts.append(
        "Có kết bài/cảm ơn" if has_closing else "Thiếu phần kết luận/cảm ơn"
    )

    return {
        "score": score,
        "has_intro": has_intro,
        "has_closing": has_closing,
        "feedback": " và ".join(feedback_parts).capitalize() + ".",
    }


async def evaluate_ac3(questions: Dict) -> Dict[str, Any]:
    """
    AC3: Đánh giá tổng hợp phần Q&A - chấm hết các câu.
    """
    detailed = []
    total_score = 0.0
    count = 0

    for qid, qdata in questions.items():
        question = qdata.get("question", "")
        answer = qdata.get("answer", "")

        score_result = await evaluate_qa_item(question, answer)

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
            "feedback": f"Lỗi hệ thống khi đánh giá: {str(e)}",
        }
