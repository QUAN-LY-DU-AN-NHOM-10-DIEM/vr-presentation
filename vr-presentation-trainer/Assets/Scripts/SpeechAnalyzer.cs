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
    // SỬA LẠI: Phải thấp hơn 20 để ngưỡng TooQuiet (20) có thể hoạt động!
    public float silenceDbThreshold = 15f;
    public float maxSilenceDuration = 2.0f;

    [Header("AC4 - Scoring Thresholds")]
    public float tooQuietLimit = 20f; // < 20dB như Jira yêu cầu
    public float tooLoudLimit = 75f;  // > 75dB như Jira yêu cầu

    [Header("Live Warnings (AC9)")]
    public float warningBufferTime = 10f;  // THAY ĐỔI: Phải liên tục 10 giây mới báo
    public float warningCooldownTime = 30f;// THÊM MỚI: AC9-4 (Cooldown 30 giây)
    public WarningEvent onLiveWarning;
    public UnityEvent onWarningCleared;

    private float badVolumeTimer = 0f;
    private string currentWarningState = "Good";

    // BIẾN QUẢN LÝ COOLDOWN
    private bool isCooldownActive = false;
    private float currentCooldownTimer = 0f;

    // --- State Tracking ---
    private bool isAnalyzing = false;
    private string activeMicName;
    private AudioClip activeClip;
    private float analyzeTimer = 0f;

    // --- Data for Report ---
    [HideInInspector] public float totalAnalyzedTime = 0f;
    [HideInInspector] public float timeTooQuiet = 0f;
    [HideInInspector] public float timeTooLoud = 0f;
    [HideInInspector] public float timeGood = 0f;

    [HideInInspector] public float currentSilenceTimer = 0f;
    [HideInInspector] public int totalPauseCount = 0; // Số lần ngừng quá 2 giây

    private float totalVolumeSum = 0f;
    private int volumeSampleCount = 0;
    public float AvgVolume => volumeSampleCount > 0 ? totalVolumeSum / volumeSampleCount : 0f;
    public int finalVolumeScore = 100;

    private float[] waveData;

    private void Start()
    {
        waveData = new float[sampleWindow];
    }

    private void Update()
    {
        // 1. CHẠY BỘ ĐẾM COOLDOWN NGẦM
        if (isCooldownActive)
        {
            currentCooldownTimer -= Time.deltaTime;
            if (currentCooldownTimer <= 0)
            {
                isCooldownActive = false;
            }
        }

        if (!isAnalyzing || activeClip == null) return;

        analyzeTimer += Time.deltaTime;
        if (analyzeTimer >= analyzeInterval)
        {
            AnalyzeCurrentAudio();
            analyzeTimer = 0f;
        }
    }

    public void StartAnalysis(string micName, AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("⚠️ SpeechAnalyzer: Không tìm thấy AudioClip để phân tích!");
            return;
        }

        activeMicName = micName;
        activeClip = clip;
        isAnalyzing = true;

        // Reset toàn bộ dữ liệu báo cáo
        ResetMetrics();
        Debug.Log("🎙️ SpeechAnalyzer: Đã bắt đầu phân tích âm thanh.");
    }

    private void ResetMetrics()
    {
        totalAnalyzedTime = 0f; timeTooQuiet = 0f; timeTooLoud = 0f; timeGood = 0f;
        currentSilenceTimer = 0f; totalPauseCount = 0;
        totalVolumeSum = 0f; volumeSampleCount = 0;
        finalVolumeScore = 100;
        badVolumeTimer = 0f;
    }

    public void PauseAnalysis()
    {
        isAnalyzing = false;
        Debug.Log("⏸ SpeechAnalyzer: Đã tạm dừng phân tích.");
    }

    public void ResumeAnalysis()
    {
        // Khi resume, cập nhật lại Clip từ MicManager (vì Clip cũ có thể đã bị hủy)
        if (MicrophoneManager.Instance != null)
        {
            activeClip = MicrophoneManager.Instance.CurrentClip;
        }
        
        if (activeClip != null)
        {
            isAnalyzing = true;
            Debug.Log("▶️ SpeechAnalyzer: Đã tiếp tục phân tích.");
        }
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

        // Theo dõi âm lượng trung bình
        totalVolumeSum += dbValue;
        volumeSampleCount++;

        // 3. Cập nhật các khoảng thời gian cho AC4
        totalAnalyzedTime += analyzeInterval;

        if (dbValue < tooQuietLimit) timeTooQuiet += analyzeInterval;
        else if (dbValue > tooLoudLimit) timeTooLoud += analyzeInterval;
        else timeGood += analyzeInterval;

        // 4. LIVE WARNING LOGIC (THÊM PHẦN NÀY VÀO TRƯỚC PHẦN KHOẢNG LẶNG)
        if (dbValue > tooLoudLimit)
        {
            HandleLiveWarning("TooLoud", "Bạn đang nói quá TO!", Color.white);
        }
        else if (dbValue < tooQuietLimit && dbValue > silenceDbThreshold)
        {
            // Lưu ý: Chỉ báo "Too Quiet" nếu họ ĐANG NÓI (lớn hơn ngưỡng im lặng)
            HandleLiveWarning("TooQuiet", "Bạn đang nói quá NHỎ!", Color.white);
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

    public void GenerateAC4Report()
    {
        if (totalAnalyzedTime == 0) return;

        float quietRatio = timeTooQuiet / totalAnalyzedTime;
        float loudRatio = timeTooLoud / totalAnalyzedTime;
        float goodRatio = timeGood / totalAnalyzedTime;

        finalVolumeScore = 100;

        if (quietRatio > 0.20f)
        {
            finalVolumeScore -= 15;
            finalVolumeScore -= Mathf.FloorToInt((quietRatio - 0.20f) * 100f);
        }
        if (loudRatio > 0.10f)
        {
            finalVolumeScore -= 10;
            finalVolumeScore -= Mathf.FloorToInt((loudRatio - 0.10f) * 100f);
        }
        finalVolumeScore = Mathf.Max(0, finalVolumeScore);

        // --- TẠO DỮ LIỆU JSON ---
        SpeechReportData report = new SpeechReportData
        {
            presentationDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            totalDurationSeconds = (float)System.Math.Round(totalAnalyzedTime, 1),
            thinkingPausesCount = totalPauseCount,
            percentTooQuiet = (float)System.Math.Round(quietRatio * 100f, 1),
            percentGoodVolume = (float)System.Math.Round(goodRatio * 100f, 1),
            percentTooLoud = (float)System.Math.Round(loudRatio * 100f, 1),
            finalVolumeScore = finalVolumeScore
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



    private void HandleLiveWarning(string stateName, string message, Color warningColor)
    {
        // AC9-4: NẾU ĐANG TRONG THỜI GIAN COOLDOWN 30S -> KHÔNG LÀM GÌ CẢ
        if (isCooldownActive) return;

        if (currentWarningState == stateName)
        {
            badVolumeTimer += analyzeInterval;
            if (badVolumeTimer >= warningBufferTime) // Đủ 10 giây
            {
                Debug.Log($"⚠️ [Speech Warning]: {message}");
                onLiveWarning?.Invoke(message, warningColor);
                
                if (VRWarningHUD.Instance != null)
                    VRWarningHUD.Instance.ShowWarning(message, warningColor);

                badVolumeTimer = 0f;

                // KÍCH HOẠT COOLDOWN 30 GIÂY NGAY KHI VỪA BÁO CẢNH BÁO
                isCooldownActive = true;
                currentCooldownTimer = warningCooldownTime;
            }
        }
        else
        {
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