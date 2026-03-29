using UnityEngine;
using TMPro;

public class PresentationTimer : MonoBehaviour
{
    public enum TimerMode { CountUp, CountDown }

    [Header("Cài đặt Timer")]
    public TimerMode mode = TimerMode.CountUp;
    public float countdownDuration = 300f;

    [Header("Hiển thị UI (Hỗ trợ nhiều màn hình)")]
    public TMP_Text[] timerTexts3D;

    private float currentTime = 0f;
    private bool isTimerRunning = false;

    // BIẾN NÀY DÙNG ĐỂ CHỐNG TRÀN RAM NÈ:
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
                Debug.Log("[Timer] Đã hết giờ thuyết trình!");
            }
        }

        // TỐI ƯU HIỆU NĂNG: Chỉ format chuỗi chữ khi số giây thực sự thay đổi
        int currentSecond = Mathf.FloorToInt(currentTime);
        if (currentSecond != lastUpdatedSecond)
        {
            lastUpdatedSecond = currentSecond;
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        int hours = Mathf.FloorToInt(currentTime / 3600f);
        int minutes = Mathf.FloorToInt((currentTime % 3600f) / 60f);
        int seconds = Mathf.FloorToInt(currentTime % 60f);

        string timeString = string.Format("{0:00}:{1:00}:{2:00}", hours, minutes, seconds);

        foreach (TMP_Text text3D in timerTexts3D)
        {
            if (text3D != null) text3D.text = timeString;
        }
    }

    // --- HÀM TẨY NÃO (DỌN SẠCH KHI RA LOBBY/CHUYỂN PHÒNG) ---
    public void ForceResetTimer()
    {
        isTimerRunning = false;
        if (mode == TimerMode.CountDown) currentTime = countdownDuration;
        else currentTime = 0f;

        lastUpdatedSecond = -1; // Ép UpdateUI chạy ngay lập tức 1 lần
        UpdateUI();
        Debug.Log("[Timer] Đã dọn dẹp và Reset sạch sẽ về 00:00:00");
    }

    public void StartTimer()
    {
        ForceResetTimer(); // Dọn sạch rác trước khi bắt đầu
        isTimerRunning = true;
        Debug.Log($"[Timer] BẮT ĐẦU CHẠY. Chế độ: {mode}");
    }

    public void PauseTimer() => isTimerRunning = false;
    public void ResumeTimer() => isTimerRunning = true;
    public void StopTimer() => isTimerRunning = false;
}