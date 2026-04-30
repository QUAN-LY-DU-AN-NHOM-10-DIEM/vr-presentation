using TMPro;
using UnityEngine;
using UnityEngine.Events;
using System.IO;

[System.Serializable]
public class PresentationScoreData
{
    public string exportTime;
    public float targetDurationSeconds;
    public string targetDurationFormatted;
    public float actualDurationSeconds;
    public string actualDurationFormatted;
    public float deviationSeconds;
    public int score;
    public int maxScore = 100;
}

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

    [Header("Score Settings")]
    [Tooltip("Vùng an toàn ±X giây quanh target")]
    public float bufferSeconds = 30f;

    [Tooltip("Mỗi X giây lệch sẽ trừ điểm")]
    public float penaltyInterval = 30f;

    [Tooltip("Số điểm trừ mỗi bước")]
    public int penaltyPoints = 5;

    [HideInInspector] public int lastScore = 0;
    [HideInInspector] public float lastDuration = 0f;

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

    public void StartPresentationTimer(float duration = 0f)
    {
        currentPhase = SessionPhase.Presentation;

        if (duration > 0f)
            presentationDuration = duration;

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

    public void SetPresentationDuration(float duration)
    {
        if (duration <= 0f)
        {
            Debug.LogWarning($"[Timer] Duration không hợp lệ: {duration}s. Giữ nguyên giá trị cũ: {presentationDuration}s");
            return;
        }

        presentationDuration = duration;
        Debug.Log($"[Timer] Đã set Presentation Duration: {duration}s");
    }

    public void CalculatePresentationScore()
    {
        float actualDuration = (mode == TimerMode.CountUp) ? currentTime : (currentDurationLimit - currentTime);

        lastDuration = actualDuration;

        float deviation = Mathf.Abs(actualDuration - presentationDuration);

        int score;

        if (deviation <= bufferSeconds)
        {
            score = 100;
        }
        else
        {
            // Công thức: 100 - (Floor(excess / 30) + 1) * 5
            float excess = deviation - bufferSeconds;
            int penaltySteps = Mathf.FloorToInt(excess / penaltyInterval) + 1;
            score = 100 - penaltySteps * penaltyPoints;
            score = Mathf.Max(0, score); // Không cho điểm âm
        }

        lastScore = score;

        int actualMin = Mathf.FloorToInt(actualDuration / 60f);
        int actualSec = Mathf.FloorToInt(actualDuration % 60f);
        int targetMin = Mathf.FloorToInt(presentationDuration / 60f);
        int targetSec = Mathf.FloorToInt(presentationDuration % 60f);

        Debug.Log($"[Score] Thực tế: {actualMin:00}:{actualSec:00} | Mục tiêu: {targetMin:00}:{targetSec:00} | Lệch: {deviation:F1}s | Điểm: {score}/100");

        ExportScoreToJson();
    }

    private void ExportScoreToJson()
    {
        try
        {
            int actualMin = Mathf.FloorToInt(lastDuration / 60f);
            int actualSec = Mathf.FloorToInt(lastDuration % 60f);
            int targetMin = Mathf.FloorToInt(presentationDuration / 60f);
            int targetSec = Mathf.FloorToInt(presentationDuration % 60f);

            PresentationScoreData data = new PresentationScoreData
            {
                exportTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                targetDurationSeconds = presentationDuration,
                targetDurationFormatted = $"{targetMin:00}:{targetSec:00}",
                actualDurationSeconds = lastDuration,
                actualDurationFormatted = $"{actualMin:00}:{actualSec:00}",
                deviationSeconds = Mathf.Abs(lastDuration - presentationDuration),
                score = lastScore
            };

            string json = JsonUtility.ToJson(data, true);

            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"TimeScore_{timestamp}.json";
            string filePath = Path.Combine(Application.persistentDataPath, fileName);

            File.WriteAllText(filePath, json);
            Debug.Log($"📄 Đã lưu file điểm tại: {filePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Lỗi khi ghi file điểm: {e.Message}");
        }
    }

    public void PauseTimer() => isTimerRunning = false;
    public void ResumeTimer() => isTimerRunning = true;
    public void StopTimer() => isTimerRunning = false;
}