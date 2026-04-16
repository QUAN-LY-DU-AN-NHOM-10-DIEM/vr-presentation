using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class PresentationTimer : MonoBehaviour
{
    public enum TimerMode { CountUp, CountDown }
    public enum SessionPhase { Presentation, QnA } // Thêm trạng thái để biết đang ở phần nào

    [Header("Cài đặt Timer")]
    public TimerMode mode = TimerMode.CountUp;
    public float presentationDuration = 900f; // 15 phút
    public float qnaDuration = 600f;          // 10 phút

    [Header("Hiển thị UI (Hỗ trợ nhiều màn hình)")]
    public TMP_Text[] timerTexts3D;

    [Header("Sự kiện khi hết giờ")] 
    public UnityEvent onPresentationTimeUp;   // Sẽ kích hoạt nếu hết giờ Thuyết trình
    public UnityEvent onQnATimeUp;            // Sẽ kích hoạt nếu hết giờ Q&A

    [HideInInspector] public SessionPhase currentPhase = SessionPhase.Presentation;

    private float currentTime;
    private float currentDurationLimit; // Biến lưu lại xem đang dùng mốc thời gian nào
    private bool isTimerRunning = false;
    private int lastUpdatedSecond = -1;

    void Update()
    {
        if (!isTimerRunning) return;

        if (mode == TimerMode.CountUp)
        {
            currentTime += Time.deltaTime;
        }
        else
        {
            currentTime -= Time.deltaTime;
            if (currentTime <= 0)
            {
                currentTime = 0;
                StopTimer();

                // KIỂM TRA XEM ĐANG Ở PHẦN NÀO ĐỂ GỌI ĐÚNG SỰ KIỆN
                if (currentPhase == SessionPhase.Presentation)
                {
                    Debug.Log("[Timer] Đã hết giờ Thuyết Trình!");
                    onPresentationTimeUp?.Invoke();
                }
                else if (currentPhase == SessionPhase.QnA)
                {
                    Debug.Log("[Timer] Đã hết giờ Q&A!");
                    onQnATimeUp?.Invoke();
                }
            }
        }

        int currentSecond = Mathf.FloorToInt(currentTime);
        if (currentSecond != lastUpdatedSecond)
        {
            lastUpdatedSecond = currentSecond;
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        int minutes = Mathf.FloorToInt((currentTime % 3600f) / 60f);
        int seconds = Mathf.FloorToInt(currentTime % 60f);

        string timeString = $"{minutes:00}:{seconds:00}";

        foreach (TMP_Text text3D in timerTexts3D)
        {
            if (text3D != null) text3D.text = timeString;
        }
    }

    // --- HÀM TẨY NÃO (DỌN SẠCH KHI RA LOBBY/CHUYỂN PHÒNG) ---
    public void ForceResetTimer()
    {
        isTimerRunning = false;
        if (mode == TimerMode.CountDown) currentTime = currentDurationLimit;
        else currentTime = 0f;

        lastUpdatedSecond = -1; // Ép UpdateUI chạy ngay lập tức 1 lần
        UpdateUI();
        Debug.Log("[Timer] Đã dọn dẹp và Reset sạch sẽ về 00:00:00");
    }

    public void StartPresentationTimer()
    {
        currentPhase = SessionPhase.Presentation;
        currentDurationLimit = presentationDuration; // Áp dụng thời gian thuyết trình
        ForceResetTimer();
        isTimerRunning = true;
        Debug.Log("[Timer] BẮT ĐẦU THUYẾT TRÌNH.");
    }

    public void StartQnATimer()
    {
        currentPhase = SessionPhase.QnA;
        currentDurationLimit = qnaDuration; // Áp dụng thời gian Q&A
        ForceResetTimer();
        isTimerRunning = true;
        Debug.Log("[Timer] BẮT ĐẦU Q&A.");
    }

    public void PauseTimer() => isTimerRunning = false;
    public void ResumeTimer() => isTimerRunning = true;
    public void StopTimer() => isTimerRunning = false;
}