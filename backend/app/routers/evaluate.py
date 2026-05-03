from typing import List
import io
from datetime import datetime
from collections import defaultdict
from pathlib import Path

from fastapi import APIRouter, UploadFile, File, Query, Request
from fastapi.responses import StreamingResponse
from app.services.workflow import (
    process_generate_questions,
    process_batch_transcribe,
    process_evaluate,
)
from app.schemas import (
    GenerateQuestionResponse,
    BatchTranscriptResponse,
    EvaluationResponse,
    EvaluateRequest,
)

router = APIRouter()


@router.post("/generate-question", response_model=GenerateQuestionResponse)
async def generate_question_endpoint(session_id: str, audio_file: UploadFile = File(...), mode: str = "practice"):
    """
    API Sinh câu hỏi phản biện.
    Đóng vai giám khảo/khán giả đặt câu hỏi dựa trên Context gốc và đoạn vừa trình bày.
    """
    return await process_generate_questions(session_id, audio_file, mode)


@router.post("/submit", response_model=BatchTranscriptResponse)
async def batch_transcribe_endpoint(
    session_id: str = Query(..., description="Session ID"),
    audio_files: List[UploadFile] = File(..., description="Danh sách file audio (Question_1.mp4, Question_2.mp4...)"),
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
    - Tự động chấm điểm nội dung (AC1, AC2, AC3)
    - Tự động tính số câu hỏi đã trả lời từ session
    - Xuất PDF báo cáo chi tiết
    """
    from app.services.session_manager import get_session

    session_data = get_session(request.session_id)
    if not session_data:
        from fastapi import HTTPException

        raise HTTPException(status_code=404, detail="Session không tìm thấy")

    # Tự động tính số câu hỏi đã trả lời
    questions = session_data.get("questions", {})
    answered_questions = sum(1 for q in questions.values() if q.get("answer", "").strip())

    # Gọi process_evaluate để lấy AC1, AC2, AC3
    evaluation_result = await process_evaluate(request.session_id)

    # Điểm nội dung tổng hợp = trung bình AC1 + AC2 + AC3
    content_total = (evaluation_result.ac1_score + evaluation_result.ac2_score + evaluation_result.ac3_score) / 3

    total_score = round(
        (content_total + request.time_management_score + request.eye_contact_score + request.volume_score) / 4,
        1,
    )

    pdf_bytes = generate_pdf_report(
        session_data=session_data,
        request=request,
        evaluation_result=evaluation_result,
        total_score=total_score,
        answered_questions=answered_questions,
    )

    return StreamingResponse(
        io.BytesIO(pdf_bytes),
        media_type="application/pdf",
        headers={"Content-Disposition": f"attachment; filename=evaluation_{request.session_id}.pdf"},
    )


def generate_pdf_report(session_data, request, evaluation_result, total_score, answered_questions) -> bytes:
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
    font_name = "Helvetica"
    font_bold = "Helvetica-Bold"
    try:
        backend_dir = Path(__file__).parent.parent.parent
        dejavu_path = backend_dir / "DejaVuSans.ttf"
        dejavu_bold_path = backend_dir / "DejaVuSans-Bold.ttf"
        if dejavu_path.exists() and dejavu_bold_path.exists():
            pdfmetrics.registerFont(TTFont("DejaVu", str(dejavu_path)))
            pdfmetrics.registerFont(TTFont("DejaVu-Bold", str(dejavu_bold_path)))
            font_name = "DejaVu"
            font_bold = "DejaVu-Bold"
        else:
            print(f"Font files not found. DejaVu: {dejavu_path.exists()}, Bold: {dejavu_bold_path.exists()}")
    except Exception as e:
        print(f"Font load error: {e}")

    buffer = io.BytesIO()
    page_width = 1152  # 16 inch
    page_height = 648  # 9 inch

    c = canvas.Canvas(buffer, pagesize=(page_width, page_height))

    # ========== TRANG 1: HEADER + ĐIỂM (TRÁI) + CHART (PHẢI) ==========
    c.setFont(font_bold, 40)
    c.drawCentredString(page_width / 2, page_height - 60, "BÁO CÁO ĐÁNH GIÁ THUYẾT TRÌNH")

    left_x = 60
    c.setFont(font_bold, 24)
    c.drawString(left_x, page_height - 120, f"Chủ đề: {session_data.get('title', 'N/A')}")
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

    # Điểm tổng kết
    c.setFillColor(colors.HexColor("#4a7ff7"))
    c.setFont(font_bold, 80)
    c.drawString(left_x, page_height - 300, f"{total_score}")
    c.setFillColor(colors.black)
    c.setFont(font_bold, 24)
    c.drawString(left_x, page_height - 340, "Điểm tổng kết")

    # ========== BIỂU ĐỒ RADAR ==========
    labels = ["Nội dung", "Tương tác mắt", "Âm lượng", "Quản lý thời gian"]
    stats = [
        evaluation_result.ac1_score,
        request.eye_contact_score,
        request.volume_score,
        request.time_management_score,
    ]

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
    for label in ax.get_yticklabels():
        label.set_fontweight("bold")
    ax.grid(True, linewidth=1.5)

    img_buffer = io.BytesIO()
    plt.savefig(img_buffer, format="PNG", bbox_inches="tight")
    plt.close(fig)
    img_buffer.seek(0)

    img_reader = ImageReader(img_buffer)
    right_x = page_width - 15 * cm
    c.drawImage(
        img_reader,
        right_x,
        page_height / 2 - 6 * cm,
        width=12 * cm,
        height=12 * cm,
    )

    # ========== TRANG 2: CHI TIẾT ĐÁNH GIÁ MẮT ==========
    c.showPage()

    c.setFont(font_bold, 32)
    c.drawCentredString(page_width / 2, page_height - 3 * cm, "CHI TIẾT ĐÁNH GIÁ MẮT")

    zone_labels = request.eye_contact_zone_names if request.eye_contact_zone_names else ["Trái trước", "Phải trước", "Trái sau", "Phải sau"]
    zones = request.eye_contact_zones

    target_groups = defaultdict(list)
    for label, pct in zip(zone_labels, zones):
        if " - " in label:
            parts = label.split(" - ")
            target_name = parts[0].strip()
            zone_name = parts[1].strip() if len(parts) > 1 else label
        else:
            target_name = label
            zone_name = label
        target_groups[target_name].append((zone_name, pct))

    target_colors = ["#4CAF50", "#2196F3", "#FF9800", "#9C27B0", "#F44336"]

    margin_left = 2 * cm
    card_width = page_width - 4 * cm
    y_pos = page_height - 6 * cm
    card_padding = 0.5 * cm
    zone_height = 1.5 * cm
    header_height = 0.8 * cm
    gap_between_cards = 1 * cm

    for idx, (target_name, zone_list) in enumerate(target_groups.items()):
        color = colors.HexColor(target_colors[idx % len(target_colors)])

        card_height = header_height + len(zone_list) * zone_height + 2 * card_padding

        if y_pos - card_height < 3 * cm:
            c.showPage()
            y_pos = page_height - 3 * cm

        c.setFillColor(colors.HexColor("#F5F5F5"))
        c.setStrokeColor(color)
        c.setLineWidth(1.5)
        c.rect(
            margin_left,
            y_pos - card_height,
            card_width,
            card_height,
            stroke=True,
            fill=True,
        )

        c.setFillColor(color)
        c.rect(
            margin_left,
            y_pos - header_height,
            card_width,
            header_height,
            fill=True,
            stroke=False,
        )
        c.setFillColor(colors.white)
        c.setFont(font_bold, 18)
        c.drawString(margin_left + 0.3 * cm, y_pos - header_height + 0.25 * cm, f"{target_name}")
        c.setFillColor(colors.black)
        y_pos -= header_height + card_padding

        for zone_name, pct in zone_list:
            zone_card_height = 1.2 * cm
            c.setFillColor(colors.HexColor("#F0F0F0"))
            c.setStrokeColor(color)
            c.setLineWidth(0.5)
            c.rect(
                margin_left + 0.3 * cm,
                y_pos - zone_card_height + 0.2 * cm,
                card_width - 0.6 * cm,
                zone_card_height,
                fill=True,
                stroke=True,
            )
            c.setFont(font_bold, 15)
            c.setFillColor(colors.black)
            c.drawString(margin_left + 0.6 * cm, y_pos - 0.25 * cm, f"{zone_name}")
            c.setFont(font_name, 13)
            c.setFillColor(color)
            c.drawString(margin_left + 0.6 * cm, y_pos - 0.75 * cm, f"{pct:.1f}%")
            c.setFillColor(colors.black)
            c.setLineWidth(1.5)
            y_pos -= zone_card_height + 0.2 * cm

        y_pos -= card_padding + gap_between_cards

    y_pos -= 0.5 * cm
    if y_pos < 3 * cm:
        c.showPage()
        y_pos = page_height - 3 * cm

    c.setFont(font_bold, 16)
    advice_text = request.eye_contact_advice
    text_obj = c.beginText(margin_left, y_pos)
    text_obj.setFont(font_name, 14)
    text_obj.setLeading(18)
    words = advice_text.split()
    line = ""
    for word in words:
        test_line = line + " " + word if line else word
        if len(test_line) * 8 < card_width:
            line = test_line
        else:
            text_obj.textLine(line)
            line = word
    if line:
        text_obj.textLine(line)
    c.drawText(text_obj)

    # ========== TRANG 4: CHI TIẾT ĐÁNH GIÁ ÂM LƯỢNG ==========
    c.showPage()

    c.setFont(font_bold, 32)
    c.drawCentredString(page_width / 2, page_height - 3 * cm, "CHI TIẾT ĐÁNH GIÁ ÂM LƯỢNG")

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
    c.drawCentredString(page_width / 2, page_height - 3 * cm, "CHI TIẾT ĐÁNH GIÁ THỜI GIAN")

    y_pos = page_height - 6 * cm
    c.setFont(font_bold, 24)
    c.drawString(2 * cm, y_pos, f"Điểm quản lý thời gian: {request.time_management_score}/100")
    y_pos -= 2 * cm

    c.setFont(font_bold, 20)
    c.drawString(2 * cm, y_pos, f"Thời gian trình bày: {request.presentation_duration}s")
    y_pos -= 1.5 * cm
    c.drawString(2 * cm, y_pos, f"Thời gian Q&A: {request.qa_duration}s")
    y_pos -= 1.5 * cm
    c.drawString(2 * cm, y_pos, f"Số câu hỏi đã trả lời: {answered_questions}")

    # ========== TRANG 3: CHI TIẾT ĐÁNH GIÁ NỘI DUNG (AC1, AC2, AC3) ==========
    c.showPage()

    c.setFont(font_bold, 32)
    c.drawCentredString(page_width / 2, page_height - 3 * cm, "CHI TIẾT ĐÁNH GIÁ NỘI DUNG")

    y_pos = page_height - 6 * cm

    # AC1 - Nội dung
    c.setFont(font_bold, 24)
    c.drawString(2 * cm, y_pos, f"AC1 - Nội dung: {evaluation_result.ac1_score}/100")
    y_pos -= 1.5 * cm

    c.setFont(font_bold, 20)
    c.drawString(2 * cm, y_pos, "Đánh giá từ khóa:")
    y_pos -= 1.2 * cm
    c.setFont(font_name, 16)
    for kw in evaluation_result.ac1_keywords:
        status_text = {
            "found": "Tìm thấy",
            "paraphrased": "Paraphrased",
            "missing": "Thiếu",
        }.get(kw.status, kw.status)
        c.drawString(3 * cm, y_pos, f"• {kw.keyword}: {status_text}")
        y_pos -= 0.8 * cm

    # AC2 - Cấu trúc
    y_pos -= 1 * cm
    c.setFont(font_bold, 24)
    c.drawString(2 * cm, y_pos, f"AC2 - Cấu trúc: {evaluation_result.ac2_score}/100")
    y_pos -= 1.5 * cm

    c.setFont(font_name, 16)
    c.drawString(3 * cm, y_pos, f"Mở đầu: {'Có' if evaluation_result.ac2_has_intro else 'Không'}")
    y_pos -= 0.8 * cm
    c.drawString(
        3 * cm,
        y_pos,
        f"Kết luận: {'Có' if evaluation_result.ac2_has_closing else 'Không'}",
    )
    y_pos -= 0.8 * cm
    c.drawString(2 * cm, y_pos, f"Nhận xét: {evaluation_result.ac2_feedback}")
    y_pos -= 1.2 * cm

    # AC3 - Q&A (hiển thị chi tiết từng câu kèm feedback)
    c.setFont(font_bold, 24)
    c.drawString(2 * cm, y_pos, f"AC3 - Q&A: {evaluation_result.ac3_score}/100")
    y_pos -= 2 * cm

    detailed_qa = evaluation_result.detailed_qa
    for qa in detailed_qa:
        # Mỗi câu Q&A sang trang mới để dễ đọc
        c.showPage()
        y_pos = page_height - 3 * cm

        c.setFont(font_bold, 18)
        c.drawString(
            2 * cm,
            y_pos,
            f"Câu {qa.question_id}: Điểm {qa.score}/100 (Khớp {qa.content_match_percent}%)",
        )
        y_pos -= 1.5 * cm

        # Hiển thị feedback của câu hỏi
        c.setFont(font_bold, 14)
        c.drawString(2 * cm, y_pos, "Nhận xét:")
        y_pos -= 1 * cm

        c.setFont(font_name, 11)
        text_obj = c.beginText(2 * cm, y_pos)
        text_obj.setFont(font_name, 11)
        text_obj.setLeading(14)
        words = qa.feedback.split()
        current_line = ""
        for word in words:
            test = current_line + " " + word if current_line else word
            if len(test) < 80:
                current_line = test
            else:
                text_obj.textLine(current_line)
                current_line = word
        if current_line:
            text_obj.textLine(current_line)
        c.drawText(text_obj)
        y_pos -= 1 * cm

    c.save()
    return buffer.getvalue()
