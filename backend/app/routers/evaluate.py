from typing import List
import io
from datetime import datetime

from fastapi import APIRouter, UploadFile, File, Query, Request
from fastapi.responses import StreamingResponse
from app.services.workflow import (
    process_generate_questions,
    process_batch_transcribe,
    process_evaluate,
    process_evaluate_content,
)
from app.schemas import (
    GenerateQuestionResponse,
    BatchTranscriptResponse,
    EvaluationResponse,
    EvaluateRequest,
)

router = APIRouter()


@router.post("/generate-question", response_model=GenerateQuestionResponse)
async def generate_question_endpoint(
    session_id: str, audio_file: UploadFile = File(...), mode: str = "practice"
):
    """
    API Sinh câu hỏi phản biện.
    Đóng vai giám khảo/khán giả đặt câu hỏi dựa trên Context gốc và đoạn vừa trình bày.
    """
    return await process_generate_questions(session_id, audio_file, mode)


@router.post("/submit", response_model=BatchTranscriptResponse)
async def batch_transcribe_endpoint(
    session_id: str = Query(..., description="Session ID"),
    audio_files: List[UploadFile] = File(
        ..., description="Danh sách file audio (Question_1.mp4, Question_2.mp4...)"
    ),
):
    """
    API nhận nhiều file audio cùng lúc, transcript và lưu vào session.
    - File phải đặt tên theo format: Question_[id].mp4 (VD: Question_1.mp4, Question_2.mp4)
    """
    import re

    audio_data = []
    for f in audio_files:
        filename = f.filename or ""
        match = re.match(r"Question_(\d+)", filename)
        if match:
            question_id = int(match.group(1))
            audio_data.append(
                {
                    "question_id": question_id,
                    "audio_file": f,
                    "file_name": filename,
                }
            )

    if not audio_data:
        from fastapi import HTTPException

        raise HTTPException(
            status_code=400,
            detail="Không tìm thấy file nào đúng format Question_[id].mp4",
        )

    return await process_batch_transcribe(session_id, audio_data)


@router.post("/evaluate")
async def evaluate_endpoint(request: EvaluateRequest):
    """
    API đánh giá toàn diện và xuất PDF:
    - Nhận các điểm đã chấm (time_management, eye_contact, volume)
    - Tự động chấm điểm nội dung (AC1)
    - Tự động tính số câu hỏi đã trả lời từ session
    - Xuất PDF báo cáo chi tiết
    """
    from app.services.session_manager import get_session

    session_data = get_session(request.session_id)
    if not session_data:
        from fastapi import HTTPException

        raise HTTPException(status_code=404, detail="Session không tìm thấy")

    context = session_data.get("context", "")
    presentation_transcript = session_data.get("presentation_transcript", "")

    # Tự động tính số câu hỏi đã trả lời
    questions = session_data.get("questions", {})
    answered_questions = sum(
        1 for q in questions.values() if q.get("answer", "").strip()
    )

    content_result = await process_evaluate_content(context, presentation_transcript)

    total_score = round(
        (
            content_result["score"]
            + request.time_management_score
            + request.eye_contact_score
            + request.volume_score
        )
        / 4,
        1,
    )

    pdf_bytes = generate_pdf_report(
        session_data=session_data,
        request=request,
        content_result=content_result,
        total_score=total_score,
        answered_questions=answered_questions,
    )

    return StreamingResponse(
        io.BytesIO(pdf_bytes),
        media_type="application/pdf",
        headers={
            "Content-Disposition": f"attachment; filename=evaluation_{request.session_id}.pdf"
        },
    )


def generate_pdf_report(
    session_data, request, content_result, total_score, answered_questions
) -> bytes:
    import io
    from reportlab.lib.pagesizes import A4, landscape
    from reportlab.pdfgen import canvas
    from reportlab.lib.units import cm
    from reportlab.lib import colors
    from reportlab.pdfbase import pdfmetrics
    from reportlab.pdfbase.ttfonts import TTFont
    from reportlab.lib.utils import ImageReader
    from matplotlib import pyplot as plt
    import numpy as np

    # Register Vietnamese font
    try:
        pdfmetrics.registerFont(TTFont("DejaVu", "DejaVuSans.ttf"))
        pdfmetrics.registerFont(TTFont("DejaVu-Bold", "DejaVuSans-Bold.ttf"))
        font_name = "DejaVu"
        font_bold = "DejaVu-Bold"
    except:
        font_name = "Helvetica"
        font_bold = "Helvetica-Bold"

    buffer = io.BytesIO()
    # Dùng kích thước slide 16:9 chuẩn (16x9 inch)
    page_width = 1152  # 16 inch
    page_height = 648  # 9 inch

    c = canvas.Canvas(buffer, pagesize=(page_width, page_height))

    # ========== TRANG 1: HEADER + ĐIỂM (TRÁI) + CHART (PHẢI) ==========
    c.setFont(font_bold, 40)
    c.drawCentredString(
        page_width / 2, page_height - 60, "BÁO CÁO ĐÁNH GIÁ THUYẾT TRÌNH"
    )

    # BÊN TRÁI: Card Header (AC1.2)
    left_x = 60
    c.setFont(font_bold, 24)
    c.drawString(
        left_x, page_height - 120, f"Chủ đề: {session_data.get('title', 'N/A')}"
    )
    c.setFont(font_bold, 20)
    c.drawString(
        left_x,
        page_height - 150,
        f"Thời gian trình bày: {request.presentation_duration}s",
    )
    c.drawString(
        left_x,
        page_height - 180,
        f"Q&A: {request.qa_duration}s | Câu trả lời: {answered_questions}",
    )

    # Điểm tổng kết (AC1.3) - bên trái
    c.setFillColor(colors.HexColor("#4a7ff7"))
    c.setFont(font_bold, 80)
    c.drawString(left_x, page_height - 300, f"{total_score}")
    c.setFillColor(colors.black)
    c.setFont(font_bold, 24)
    c.drawString(left_x, page_height - 340, "Điểm tổng kết")

    # ========== BIỂU ĐỒ RADAR (AC1.4) ==========
    # Vẽ radar chart bằng matplotlib - màu xanh lá, đậm nét
    labels = ["Nội dung", "Tương tác mắt", "Âm lượng", "Quản lý thời gian"]
    stats = [
        content_result["score"],
        request.eye_contact_score,
        request.volume_score,
        request.time_management_score,
    ]

    # Tạo radar chart
    angles = np.linspace(0, 2 * np.pi, len(labels), endpoint=False).tolist()
    stats += stats[:1]
    angles += angles[:1]

    fig, ax = plt.subplots(figsize=(8, 8), subplot_kw=dict(polar=True))
    ax.plot(angles, stats, color="#4a7ff7", linewidth=5)
    ax.fill(angles, stats, color="#4a7ff7", alpha=0.3)
    ax.set_xticks(angles[:-1])
    ax.set_xticklabels(labels, fontsize=20, fontweight="bold")
    ax.set_ylim(0, 100)
    ax.set_yticks([20, 40, 60, 80, 100])
    ax.tick_params(axis="y", labelsize=16)
    # Set bold cho nhãn trục y
    for label in ax.get_yticklabels():
        label.set_fontweight("bold")
    ax.grid(True, linewidth=1.5)

    # Lưu radar chart vào buffer
    img_buffer = io.BytesIO()
    plt.savefig(img_buffer, format="PNG", bbox_inches="tight")
    plt.close(fig)
    img_buffer.seek(0)

    # Vẽ ảnh radar vào PDF - bên PHẢI
    from reportlab.lib.utils import ImageReader

    img_reader = ImageReader(img_buffer)
    right_x = page_width - 15 * cm  # Cách lề phải 15cm
    c.drawImage(
        img_reader,
        right_x,
        page_height / 2 - 6 * cm,  # Canh giữa theo chiều dọc
        width=12 * cm,
        height=12 * cm,
    )

    # ========== TRANG 2: CHI TIẾT ĐÁNH GIÁ MẮT ==========
    c.showPage()

    c.setFont(font_bold, 32)
    c.drawCentredString(page_width / 2, page_height - 3 * cm, "CHI TIẾT ĐÁNH GIÁ MẮT")

    c.setFont(font_bold, 24)
    y_pos = page_height - 6 * cm
    zone_labels = (
        request.eye_contact_zone_names
        if request.eye_contact_zone_names
        else ["Trái trước", "Phải trước", "Trái sau", "Phải sau"]
    )
    for label, pct in zip(zone_labels, request.eye_contact_zones):
        c.setFont(font_name, 20)
        c.drawString(3 * cm, y_pos, f"{label}: {pct:.1f}%")
        # Thanh ngang
        c.setFillColor(colors.HexColor("#E0E0E0"))
        c.rect(10 * cm, y_pos - 6, 8 * cm, 14, fill=True, stroke=False)
        c.setFillColor(colors.HexColor("#4CAF50"))
        bar_width = (pct / 100) * 8 * cm
        c.rect(10 * cm, y_pos - 6, bar_width, 14, fill=True, stroke=False)
        c.setFillColor(colors.black)
        y_pos -= 1.5 * cm

    c.setFont(font_bold, 18)
    c.drawString(2 * cm, y_pos - 0.5 * cm, request.eye_contact_advice)

    # ========== TRANG 3: CHI TIẾT ĐÁNH GIÁ NỘI DUNG ==========
    c.showPage()

    c.setFont(font_bold, 32)
    c.drawCentredString(
        page_width / 2, page_height - 3 * cm, "CHI TIẾT ĐÁNH GIÁ NỘI DUNG"
    )

    y_pos = page_height - 6 * cm
    c.setFont(font_bold, 24)
    c.drawString(2 * cm, y_pos, f"Điểm nội dung: {content_result['score']}/100")
    y_pos -= 2 * cm

    c.setFont(font_bold, 24)
    c.drawString(2 * cm, y_pos, "Đánh giá từ khóa:")
    y_pos -= 1.5 * cm
    c.setFont(font_bold, 18)
    for kw in content_result.get("keywords", []):
        status_text = {
            "found": "Tìm thấy",
            "paraphrased": "Paraphrased",
            "missing": "Thiếu",
        }.get(kw["status"], kw["status"])
        c.drawString(3 * cm, y_pos, f"• {kw['keyword']}: {status_text}")
        y_pos -= 1 * cm

    # ========== TRANG 4: CHI TIẾT ĐÁNH GIÁ ÂM LƯỢNG ==========
    c.showPage()

    c.setFont(font_bold, 32)
    c.drawCentredString(
        page_width / 2, page_height - 3 * cm, "CHI TIẾT ĐÁNH GIÁ ÂM LƯỢNG"
    )

    y_pos = page_height - 6 * cm
    c.setFont(font_bold, 24)
    c.drawString(2 * cm, y_pos, f"Điểm âm lượng: {request.volume_score}/100")
    y_pos -= 2 * cm

    c.setFont(font_bold, 20)
    c.drawString(2 * cm, y_pos, f"Quiet Ratio: {request.quiet_ratio:.1%}")
    y_pos -= 1.5 * cm
    c.drawString(2 * cm, y_pos, f"Loud Ratio: {request.loud_ratio:.1%}")
    y_pos -= 1.5 * cm
    c.drawString(2 * cm, y_pos, f"Âm lượng trung bình: {request.avg_volume:.1f} dB")

    # ========== TRANG 5: CHI TIẾT ĐÁNH GIÁ THỜI GIAN ==========
    c.showPage()

    c.setFont(font_bold, 32)
    c.drawCentredString(
        page_width / 2, page_height - 3 * cm, "CHI TIẾT ĐÁNH GIÁ THỜI GIAN"
    )

    y_pos = page_height - 6 * cm
    c.setFont(font_bold, 24)
    c.drawString(
        2 * cm, y_pos, f"Điểm quản lý thời gian: {request.time_management_score}/100"
    )
    y_pos -= 2 * cm

    c.setFont(font_bold, 20)
    c.drawString(
        2 * cm, y_pos, f"Thời gian trình bày: {request.presentation_duration}s"
    )
    y_pos -= 1.5 * cm
    c.drawString(2 * cm, y_pos, f"Thời gian Q&A: {request.qa_duration}s")
    y_pos -= 1.5 * cm
    c.drawString(2 * cm, y_pos, f"Số câu hỏi đã trả lời: {answered_questions}")

    c.save()
    return buffer.getvalue()
