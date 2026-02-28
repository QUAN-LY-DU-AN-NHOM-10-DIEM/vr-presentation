import requests
import json
import time

# Cấu hình API
API_URL = "http://127.0.0.1:8000/api/v1/generate-question"

# ⚠️ QUAN TRỌNG: Lấy 1 cái topic_id hợp lệ từ Database paste vào đây
TOPIC_ID = "c0bd4c1f-d012-4e82-9759-3b9d3793651f" 

# Bài nói "bẫy" AI: Đưa ra một quan điểm sai lầm nghiêm trọng để xem AI phản ứng sao
speech_transcript = "Như em vừa trình bày, kiểm thử hệ thống (System Test) là bước rất quan trọng. Tuy nhiên, nhóm em quyết định bỏ qua Unit Test vì nó quá mất thời gian của dev, thà để dồn lại test một lần ở System Test cho nhanh và tiết kiệm chi phí dự án."

test_cases = [
    {
        "name": "TEST 1: Chế độ Practice (Mentor thân thiện)",
        "payload": {
            "topic_id": TOPIC_ID,
            "mode": "practice",
            "user_speech": speech_transcript
        }
    },
    {
        "name": "TEST 2: Chế độ Exam (Giám khảo khó tính vặn vẹo)",
        "payload": {
            "topic_id": TOPIC_ID,
            "mode": "exam",
            "user_speech": speech_transcript
        }
    }
]

print("🚀 BẮT ĐẦU TEST API SINH CÂU HỎI PHẢN BIỆN...\n")

for idx, case in enumerate(test_cases, 1):
    print(f"==================================================")
    print(f"🎯 {case['name']}")
    print(f"==================================================")
    
    start_time = time.time()
    
    try:
        response = requests.post(API_URL, json=case["payload"])
        end_time = time.time()
        
        if response.status_code == 200:
            data = response.json()
            questions = data.get("questions", [])
            
            print(f"⏱️ Thời gian xử lý: {round(end_time - start_time, 2)} giây")
            print(f"✅ Đã sinh ra {len(questions)} câu hỏi:\n")
            
            for i, q in enumerate(questions, 1):
                print(f"  {i}. {q}")
            print("\n")
        else:
            print(f"❌ Lỗi {response.status_code}: {response.text}\n")
            
    except Exception as e:
        print(f"❌ Lỗi kết nối: {e}\n")

print("🏁 Hoàn thành test!")