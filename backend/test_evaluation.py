import requests
import json

# Cấu hình API
API_URL = "http://127.0.0.1:8000/api/v1/evaluate-speech"

# ⚠️ QUAN TRỌNG: Lấy 1 cái topic_id từ Database paste vào đây
TOPIC_ID = "c0bd4c1f-d012-4e82-9759-3b9d3793651f" 

# --- DATA TEST ---
test_cases = [
    {
        "name": "TEST 1: Chế độ Practice (Nói tạm ổn, hơi lặp từ)",
        "payload": {
            "topic_id": TOPIC_ID,
            "mode": "practice",
            "user_speech": "Chào thầy và các bạn, hôm nay em xin trình bày về kiểm thử phần mềm. Kiểm thử rất quan trọng để đảm bảo chất lượng. Ờ... thì... nó có 4 cấp độ chính là Unit test, Integration test, System test và Acceptance test. Nguyên tắc cơ bản là test càng sớm càng tốt để tiết kiệm chi phí. Em xin hết ạ."
        }
    },
    {
        "name": "TEST 2: Chế độ Exam (Nói sai kiến thức, lủng củng)",
        "payload": {
            "topic_id": TOPIC_ID,
            "mode": "exam",
            "user_speech": "Chào thầy và các bạn, hôm nay em xin trình bày về kiểm thử phần mềm. Kiểm thử rất quan trọng để đảm bảo chất lượng. Ờ... thì... nó có 4 cấp độ chính là Unit test, Integration test, System test và Acceptance test. Nguyên tắc cơ bản là test càng sớm càng tốt để tiết kiệm chi phí. Em xin hết ạ."
        }
    }
]

# --- CHẠY TEST ---
print("🚀 Bắt đầu test API Chấm điểm AI...\n")

for idx, case in enumerate(test_cases, 1):
    print(f"--- {case['name']} ---")
    try:
        response = requests.post(API_URL, json=case["payload"])
        
        if response.status_code == 200:
            data = response.json()
            print("✅ Trạng thái: THÀNH CÔNG (200 OK)")
            print(f"🆔 Session ID: {data.get('session_id')}")
            print(f"🌟 Điểm tổng (Overall): {data.get('overall_score')}/10")
            
            criteria = data.get('criteria_scores', {})
            print("📊 Chi tiết điểm:")
            print(f"   - Chính xác (Accuracy): {criteria.get('accuracy')}")
            print(f"   - Lưu loát (Fluency): {criteria.get('fluency')}")
            print(f"   - Lặp từ (Repetition): {criteria.get('repetition')}")
            print(f"   - Cấu trúc (Structure): {criteria.get('structure')}")
            
            print(f"💬 Nhận xét (Feedback):\n{data.get('feedback')}\n")
        else:
            print(f"❌ Lỗi {response.status_code}: {response.text}\n")
            
    except Exception as e:
        print(f"❌ Lỗi kết nối: {e}\n")

print("🏁 Hoàn thành test!")