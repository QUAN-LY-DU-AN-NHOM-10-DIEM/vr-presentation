import requests
import os

# Cấu hình
API_URL = "http://127.0.0.1:8000/api/v1/upload-context"
SLIDE_PATH = "dummy_slide.pdf"
SCRIPT_PATH = "kich_ban_test.txt"

# 1. Tạo file PDF giả (nếu chưa có)
if not os.path.exists(SLIDE_PATH):
    from pypdf import PdfWriter
    writer = PdfWriter()
    writer.add_blank_page(width=72, height=72)
    with open(SLIDE_PATH, "wb") as f:
        writer.write(f)
    print(f"📄 Đã tạo file PDF giả: {SLIDE_PATH}")

# 2. Tạo file Script giả (Nội dung ở trên)
content = """
Xin chào thầy và các bạn, hôm nay em xin trình bày về Tổng quan Kiểm thử Phần mềm.
Đầu tiên, về cơ sở và tầm quan trọng: Kiểm thử phần mềm đảm bảo chất lượng sản phẩm.
Về các cấp độ kiểm thử, có 4 mức: Unit Test, Integration Test, System Test, và Acceptance Test.
Tiếp theo, Testcase là tập hợp điều kiện để xác minh chức năng. Nguyên tắc là "Test càng sớm càng tốt".
Về quy trình: Lập kế hoạch -> Thiết kế -> Thực thi -> Báo cáo.
Cuối cùng là Tự động hóa kiểm thử (Automation Testing) giúp test nhanh hơn.
"""
with open(SCRIPT_PATH, "w", encoding="utf-8") as f:
    f.write(content.strip())
print(f"📝 Đã tạo file Script: {SCRIPT_PATH}")

# 3. Gửi Request lên API
print("🚀 Đang gửi request lên API...")
files = {
    'slide_file': (SLIDE_PATH, open(SLIDE_PATH, 'rb'), 'application/pdf'),
    'script_file': (SCRIPT_PATH, open(SCRIPT_PATH, 'rb'), 'text/plain')
}

try:
    response = requests.post(API_URL, files=files)
    
    # 4. In kết quả
    if response.status_code == 200:
        data = response.json()
        print("\n✅ THÀNH CÔNG!")
        print(f"🆔 Session ID: {data['session_id']}")
        print(f"📌 Topic: {data['title']}")
        print(f"📄 Summary:\n{data['context_text']}")
    else:
        print(f"\n❌ LỖI: {response.status_code}")
        print(response.text)

except Exception as e:
    print(f"\n❌ Lỗi kết nối: {e}")

finally:
    # Đóng file
    files['slide_file'][1].close()
    files['script_file'][1].close()