using System.IO;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class WarningEvent : UnityEvent<string, Color> { }

public class SpeechAnalyzer : MonoBehaviour
{
    [Header("Settings - Audio Sampling")]
    public int sampleWindow = 512; // Lấy 512 mẫu mỗi 0.1 giây (đủ nhẹ để không giật lag)
    public float analyzeInterval = 0.1f; // Tần suất đọc dữ liệu (0.1 giây/lần)

    [Header("Settings - Calibration (dB)")]
    [Tooltip("Cộng bù để chuyển từ dB số (âm) sang dB vật lý (dương). Tùy chỉnh theo kính VR của bạn.")]
    public float dbCalibrationOffset = 85f;

    [Header("Settings - Silence & Volumes")]
    public float silenceDbThreshold = 25f;  // Dưới 25dB được coi là đang không nói
    public float maxSilenceDuration = 2.0f; // Ngừng nói > 2s là tính 1 khoảng lặng

    [Header("AC4 - Scoring Thresholds")]
    public float tooQuietLimit = 20f; // < 20dB
    public float tooLoudLimit = 75f;  // > 75dB

    [Header("Live Warnings")]
    public float warningBufferTime = 1.5f; // Phải nói to/nhỏ liên tục 1.5s mới báo động
    public WarningEvent onLiveWarning;     // Gửi chữ và màu sắc ra ngoài UI
    public UnityEvent onWarningCleared;    // Tắt cảnh báo khi âm lượng tốt lại

    private float badVolumeTimer = 0f;
    private string currentWarningState = "Good";

    // --- State Tracking ---
    private bool isAnalyzing = false;
    private string activeMicName;
    private AudioClip activeClip;
    private float analyzeTimer = 0f;

    // --- Data for Report ---
    private float totalAnalyzedTime = 0f;
    private float timeTooQuiet = 0f;
    private float timeTooLoud = 0f;
    private float timeGood = 0f;

    private float currentSilenceTimer = 0f;
    private int totalPauseCount = 0; // Số lần ngừng quá 2 giây

    private float[] waveData;

    private void Start()
    {
        waveData = new float[sampleWindow];
    }

    private void Update()
    {
        if (!isAnalyzing || activeClip == null) return;

        // Bộ đếm nhịp: Chỉ chạy hàm phân tích mỗi 0.1 giây để tiết kiệm CPU
        analyzeTimer += Time.deltaTime;
        if (analyzeTimer >= analyzeInterval)
        {
            AnalyzeCurrentAudio();
            analyzeTimer = 0f;
        }
    }

    // GỌI HÀM NÀY KHI BẮT ĐẦU RECORD (Từ PauseMenuManager)
    public void StartAnalysis(string micName, AudioClip clip)
    {
        activeMicName = micName;
        activeClip = clip;
        isAnalyzing = true;

        // Reset toàn bộ dữ liệu báo cáo
        totalAnalyzedTime = 0f; timeTooQuiet = 0f; timeTooLoud = 0f; timeGood = 0f;
        currentSilenceTimer = 0f; totalPauseCount = 0;

        Debug.Log("🎙️ [Speech Analyzer] Bắt đầu phân tích giọng nói ngầm...");
    }

    // GỌI HÀM NÀY KHI DỪNG RECORD -> NÓ SẼ TRẢ VỀ BÁO CÁO!
    public void StopAndGenerateReport()
    {
        isAnalyzing = false;
        GenerateAC4Report();
    }

    private void AnalyzeCurrentAudio()
    {
        int micPosition = Microphone.GetPosition(activeMicName) - sampleWindow;
        if (micPosition < 0) return; // Bỏ qua frame đầu tiên khi chưa đủ dữ liệu

        activeClip.GetData(waveData, micPosition);

        // 1. Tính toán RMS (Năng lượng âm thanh)
        float sumSquare = 0f;
        for (int i = 0; i < sampleWindow; i++)
        {
            sumSquare += waveData[i] * waveData[i];
        }
        float rmsValue = Mathf.Sqrt(sumSquare / sampleWindow);

        // 2. Tính dB và bù thành chuẩn Dương (SPL)
        float dbValue = 0f;
        if (rmsValue > 0.0001f)
        {
            // Cộng thêm offset để đưa -60dB -> 25dB, 0dB -> 85dB
            dbValue = (20f * Mathf.Log10(rmsValue)) + dbCalibrationOffset;
        }
        // Ép dbValue không bao giờ rớt xuống dưới 0
        dbValue = Mathf.Max(0, dbValue);

        // 3. Cập nhật các khoảng thời gian cho AC4
        totalAnalyzedTime += analyzeInterval;

        if (dbValue < tooQuietLimit) timeTooQuiet += analyzeInterval;
        else if (dbValue > tooLoudLimit) timeTooLoud += analyzeInterval;
        else timeGood += analyzeInterval;

        // 4. LIVE WARNING LOGIC (THÊM PHẦN NÀY VÀO TRƯỚC PHẦN KHOẢNG LẶNG)
        if (dbValue > tooLoudLimit)
        {
            HandleLiveWarning("TooLoud", "Bạn đang nói quá TO!", Color.red);
        }
        else if (dbValue < tooQuietLimit && dbValue > silenceDbThreshold)
        {
            // Lưu ý: Chỉ báo "Too Quiet" nếu họ ĐANG NÓI (lớn hơn ngưỡng im lặng)
            HandleLiveWarning("TooQuiet", "Bạn đang nói quá NHỎ!", new Color32(100, 200, 255, 255)); // Màu xanh dương
        }
        else if (dbValue >= tooQuietLimit && dbValue <= tooLoudLimit)
        {
            // Âm lượng hoàn hảo -> Xóa cảnh báo
            if (currentWarningState != "Good")
            {
                currentWarningState = "Good";
                badVolumeTimer = 0f;
                onWarningCleared?.Invoke(); // Tắt UI
            }
        }

        // 4. Phát hiện Khoảng lặng (Thinking Pause)
        if (dbValue < silenceDbThreshold)
        {
            currentSilenceTimer += analyzeInterval;
            // Nếu ngừng nói vượt quá 2 giây và chưa được đếm
            if (currentSilenceTimer >= maxSilenceDuration && currentSilenceTimer < (maxSilenceDuration + analyzeInterval))
            {
                totalPauseCount++;
                Debug.Log($"⏳ [Phát hiện khoảng lặng] Người dùng đang ngừng để suy nghĩ. (Tổng: {totalPauseCount} lần)");
            }
        }
        else
        {
            // Có tiếng nói lại -> Reset bộ đếm khoảng lặng
            currentSilenceTimer = 0f;
        }

        // TÙY CHỌN: In ra Log để bạn Debug xem dB có chuẩn không (Comment lại khi build game)
         Debug.Log($"Current dB: {dbValue:F1}");
    }

    private void GenerateAC4Report()
    {
        if (totalAnalyzedTime == 0) return;

        float quietRatio = timeTooQuiet / totalAnalyzedTime;
        float loudRatio = timeTooLoud / totalAnalyzedTime;
        float goodRatio = timeGood / totalAnalyzedTime;

        int finalScore = 100;

        if (quietRatio > 0.20f)
        {
            finalScore -= 15;
            finalScore -= Mathf.FloorToInt((quietRatio - 0.20f) * 100f);
        }
        if (loudRatio > 0.10f)
        {
            finalScore -= 10;
            finalScore -= Mathf.FloorToInt((loudRatio - 0.10f) * 100f);
        }
        finalScore = Mathf.Max(0, finalScore);

        // --- TẠO DỮ LIỆU JSON ---
        SpeechReportData report = new SpeechReportData
        {
            presentationDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            totalDurationSeconds = (float)System.Math.Round(totalAnalyzedTime, 1),
            thinkingPausesCount = totalPauseCount,
            percentTooQuiet = (float)System.Math.Round(quietRatio * 100f, 1),
            percentGoodVolume = (float)System.Math.Round(goodRatio * 100f, 1),
            percentTooLoud = (float)System.Math.Round(loudRatio * 100f, 1),
            finalVolumeScore = finalScore
        };

        // Chuyển Data thành chuỗi JSON (chữ 'true' giúp format JSON đẹp và dễ đọc)
        string jsonOutput = JsonUtility.ToJson(report, true);

        // Lưu file vào bộ nhớ thiết bị
        string timeStamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string fileName = "SpeechReport_" + timeStamp + ".json";
        string filePath = Path.Combine(Application.persistentDataPath, fileName);

        File.WriteAllText(filePath, jsonOutput);

        Debug.Log("JSON Report Saved Successfully at: " + filePath);
    }

    // TẠM DỪNG PHÂN TÍCH
    public void PauseAnalysis()
    {
        if (isAnalyzing)
        {
            isAnalyzing = false;
            Debug.Log("⏸ [Speech Analyzer] Đã tạm dừng phân tích.");
        }
    }

    // TIẾP TỤC PHÂN TÍCH LẠI
    public void ResumeAnalysis()
    {
        if (!isAnalyzing && activeClip != null)
        {
            isAnalyzing = true;
            Debug.Log("▶️ [Speech Analyzer] Tiếp tục phân tích.");
        }
    }

    private void HandleLiveWarning(string stateName, string message, Color warningColor)
    {
        if (currentWarningState == stateName)
        {
            // Tích lũy thời gian nếu họ tiếp tục nói sai
            badVolumeTimer += analyzeInterval;
            if (badVolumeTimer >= warningBufferTime)
            {
                onLiveWarning?.Invoke(message, warningColor);
                badVolumeTimer = 0f; // Reset để không spam event liên tục
            }
        }
        else
        {
            // Vừa mới đổi trạng thái (Từ Good sang TooLoud chẳng hạn)
            currentWarningState = stateName;
            badVolumeTimer = 0f;
        }
    }
}

[System.Serializable]
public class SpeechReportData
{
    public string presentationDate;
    public float totalDurationSeconds;
    public int thinkingPausesCount;
    public float percentTooQuiet;
    public float percentGoodVolume;
    public float percentTooLoud;
    public int finalVolumeScore;
}