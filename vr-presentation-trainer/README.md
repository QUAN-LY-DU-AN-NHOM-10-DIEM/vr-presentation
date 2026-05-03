# VR Presentation Trainer: Hệ Thống Đào Tạo Thuyết Trình VR Thông Minh

![Unity](https://img.shields.io/badge/Unity-2021.3%2B-blue.svg) ![VR](https://img.shields.io/badge/VR-Oculus%2FMeta%20Quest-green.svg) ![AI](https://img.shields.io/badge/AI-Powered-red.svg)

## 🎯 1. Mục Tiêu Dự Án (Project Goals)

**VR Presentation Trainer** là một giải pháp mô phỏng thực tế ảo nhằm giúp người dùng vượt qua nỗi sợ nói trước đám đông và cải thiện kỹ năng thuyết trình chuyên nghiệp. Dự án kết hợp công nghệ VR tiên tiến với Trí tuệ nhân tạo (AI) để tạo ra một môi trường luyện tập tương tác cao, cung cấp phản hồi chính xác và khách quan về:
*   **Kỹ năng diễn đạt**: Phân tích giọng nói, tốc độ và ngữ điệu.
*   **Hành vi phi ngôn ngữ**: Phân tích sự tập trung và phân bổ ánh mắt (Gaze Interaction).
*   **Khả năng ứng biến**: Trả lời các câu hỏi hóc búa do AI tự động tạo ra dựa trên nội dung thuyết trình thực tế.

---

## 🛠 2. Công Nghệ Sử Dụng (Tech Stack)

### Frontend (Unity VR)
*   **Engine**: Unity 2021.3 LTS.
*   **VR SDK**: XR Interaction Toolkit (v2.x/v3.x).
*   **UI System**: TextMeshPro & Canvas Group Alpha Blending.
*   **Audio**: Unity Microphone API & High-fidelity WAV encoding.

### Backend & AI
*   **API**: RESTful API (Giao thức Multipart Form Data).
*   **AI Models**: LLM (Large Language Model) để tạo câu hỏi và chấm điểm nội dung.
*   **Analytics**: Thuật toán phân tích tọa độ ánh mắt và xử lý tín hiệu âm thanh kỹ thuật số.

---

## 🚀 3. Hướng Dẫn Cài Đặt (Installation Guide)

### Yêu cầu hệ thống
*   **Unity**: Phiên bản 2021.3.x (LTS) trở lên.
*   **Thiết bị**: Meta Quest 2/3/Pro hoặc kính VR tương thích OpenXR.
*   **Internet**: Kết nối ổn định để giao tiếp với AI Server.

### Các bước triển khai
1.  **Clone Project**: Tải mã nguồn về máy.
2.  **Import Dependencies**: Mở Unity và đợi hệ thống tự động tải các Package (XR Plugin Management, TMP).
3.  **Cấu hình API**: 
    *   Mở file `Assets/Scripts/Managers/ApiManager.cs`.
    *   Thay đổi `baseUrl` thành địa chỉ Server của bạn.
4.  **Thiết lập Scene**:
    *   Mở Scene chính trong thư mục `Scenes/`.
    *   Đảm bảo đối tượng **XR Origin** đã được cấu hình đúng cho thiết bị của bạn.
---

## 📝 7. Giấy phép & Đóng góp
Dự án được phát triển cho mục đích giáo dục và đào tạo kỹ năng mềm. Mọi đóng góp vui lòng tạo Pull Request hoặc liên hệ qua email quản trị dự án.
