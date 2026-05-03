using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityPdfViewer;
using Assets.CustomPdfViewer.Scripts;

public class GenerateQuestionResponse
{
    // Đổi từ string question sang mảng hoặc List
    public List<string> questions;
}

[System.Serializable]
public class EvaluateRequest
{
    public string session_id;
    public int time_management_score;
    public int eye_contact_score;
    public int volume_score;
    public List<float> eye_contact_zones;
    public List<string> eye_contact_zone_names;
    public string eye_contact_advice;
    public int presentation_duration;
    public int target_time;
    public int qa_duration;
    public float quiet_ratio;
    public float loud_ratio;
    public float avg_volume;
}
public class PauseMenuManager : MonoBehaviour
{
    [Header("Menu Setup")]
    public GameObject pauseCanvas;
    public ModeManager modeManager;
    public CustomPdfViewerUI pdf1;
    public CustomPdfViewerUI pdf2;
    public CustomPdfViewerUI reportPdfViewer;
    public InputActionReference menuButtonInput;
    public GameObject micOnImage;
    public GameObject micOffImage;
    public SpeechAnalyzer speechAnalyzer;
    [Header("Title UI References")]
    public TextMeshProUGUI titleText;  // Kéo object chứa chữ vào đây
    public Image titleBackground;
    public TextMeshProUGUI timerText;
    [HideInInspector]
    public bool isPaused = false;

    [Header("Navigation")]
    public GameObject vrPlayer;
    public Transform lobbySpawnPoint;
    [Header("Hand Menu Offsets")]
    public GameObject playerLeftHand;
    public Vector3 menuPositionOffset = new Vector3(-0.35f, 0.01f, -0.1f); // Lệch lên trên và ra trước một chút
    public Vector3 menuRotationOffset = new Vector3(0f, -180f, -90f);    // Nghiêng 45 độ để dễ nhìn

    // Mic Info
    [HideInInspector]
    public string hardwareMicName;
    private bool isRecording = false;
    private AudioClip tempRecordingClip; // File ghi âm tạm thời cho mỗi lần bấm
    private List<float> allAudioChunks = new List<float>(); // Cái "xô" chứa toàn bộ âm thanh
    private int sampleRate = 44100;

    [Header("External References")]
    public GazeTrackingManager gazeTracker;
    public PresentationTimer presentationTimer;
    public TimePicker timePicker;

    [Header("API Configuration")]
    [Tooltip("Nhập URL ngrok của bạn vào đây (Không có dấu gạch chéo ở cuối)")]
    public string backendBaseUrl = "https://your-ngrok-url.ngrok-free.app/api/v1";
    public string mode = "practice";


    // Thêm biến này để chặn người dùng bấm nhiều lần
    private bool isUploading = false;
    private float savedPresentationDuration = 0f;

    private void Start()
    {
        if (playerLeftHand != null && pauseCanvas != null)
        {
            // 1. Biến Canvas thành con của Bàn tay trái
            pauseCanvas.transform.SetParent(playerLeftHand.transform);
            // 2. Chỉnh vị trí lệch (Local Position) so với bàn tay
            pauseCanvas.transform.localPosition = menuPositionOffset;
            // 3. Chỉnh góc xoay (Local Rotation) để menu ngửa lên nhìn thẳng vào mắt
            pauseCanvas.transform.localEulerAngles = menuRotationOffset;
        }
        if (pauseCanvas != null) pauseCanvas.SetActive(false);
    }

    public void TurnOnVoiceAnalyzer()
    {
        speechAnalyzer.StartAnalysis(hardwareMicName, tempRecordingClip);
    }
    public void TurnOnMic()
    {
        if (string.IsNullOrEmpty(hardwareMicName)) { Debug.Log("No mic yet"); return; }
        if (!isRecording)
        {
            // BẮT ĐẦU THU ÂM
            tempRecordingClip = Microphone.Start(hardwareMicName, false, 600, sampleRate);
            isRecording = true;
            if (micOnImage != null) micOnImage.SetActive(true);
            if (micOffImage != null) micOffImage.SetActive(false);
            Debug.Log("Start/Continue Recording");
        }
    }
    public void TurnOffMic()
    {
        if (string.IsNullOrEmpty(hardwareMicName)) { Debug.Log("No mic yet"); return; }

        if (isRecording)
        {
            // DỪNG THU ÂM
            int lastPos = Microphone.GetPosition(hardwareMicName);
            Microphone.End(hardwareMicName);
            isRecording = false;

            // Rút trích âm thanh thực tế vừa thu và nhét vào List
            if (lastPos > 0 && tempRecordingClip != null)
            {
                float[] chunkData = new float[lastPos * tempRecordingClip.channels];
                tempRecordingClip.GetData(chunkData, 0);
                allAudioChunks.AddRange(chunkData); // Cứ thế nối tiếp vào cuối danh sách
            }
            // [TỐI ƯU RAM] - Hủy file tạm ngay lập tức
            if (tempRecordingClip != null)
            {
                Destroy(tempRecordingClip);
                tempRecordingClip = null;
            }

            if (micOnImage != null) micOnImage.SetActive(false);
            if (micOffImage != null) micOffImage.SetActive(true);

            Debug.Log("⏸ Paused. Ready to Resume or Save.");
        }
    }

    //public void PlayRecording()
    //{
    //    if (isRecording) return;

    //    if (recordedClip != null && activeMicSource != null)
    //    {
    //        activeMicSource.clip = recordedClip;
    //        activeMicSource.Play();
    //    }
    //}
    private AudioClip CreateFinalStitchedClip()
    {
        if (allAudioChunks.Count == 0) return null;

        // Tạo một AudioClip mới tinh, độ dài đúng bằng tổng tất cả các đoạn cộng lại
        AudioClip finalClip = AudioClip.Create("FinalPresentation", allAudioChunks.Count, 1, sampleRate, false);
        finalClip.SetData(allAudioChunks.ToArray(), 0);
        return finalClip;
    }

    public void SaveRecordingToFile()
    {
        // Nếu họ đang thu mà bấm Save luôn, ép nó tự động Pause để lấy đoạn cuối
        TurnOffMic();
        speechAnalyzer.StopAndGenerateReport();
        AudioClip finalClip = CreateFinalStitchedClip();

        if (finalClip != null)
        {
            string timeStamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string fileName = "VR_Presentation_" + timeStamp + ".wav";

            string savedPath = WavUtility.Save(fileName, finalClip);

            SessionManager.WavPath = savedPath;

            Debug.Log("Audio File Saved Successfully at: " + savedPath);
            // [TỐI ƯU RAM] - Hủy file tổng sau khi đã xuất ra file .wav thành công!
            Destroy(finalClip);
            allAudioChunks.Clear();
        }
        else
        {
            Debug.Log("⚠️ No audio to save!");
        }
    }

    public void SaveRecordingQuestionToFile(int index)
    {
        // Nếu họ đang thu mà bấm Save luôn, ép nó tự động Pause để lấy đoạn cuối
        TurnOffMic();
        AudioClip finalClip = CreateFinalStitchedClip();

        if (finalClip != null)
        {
            string fileName = "Question_" + index + ".wav";

            string savedPath = WavUtility.Save(fileName, finalClip);

            SessionManager.WavPath = savedPath;

            Debug.Log("Audio File Saved Successfully at: " + savedPath);
            // [TỐI ƯU RAM] - Hủy file tổng sau khi đã xuất ra file .wav thành công!
            Destroy(finalClip);
            allAudioChunks.Clear();
        }
        else
        {
            Debug.Log("⚠️ No audio to save!");
        }
    }

    private void OnEnable()
    {
        if (menuButtonInput != null)
            menuButtonInput.action.performed += TogglePauseMenu;
    }

    private void OnDisable()
    {
        if (menuButtonInput != null)
            menuButtonInput.action.performed -= TogglePauseMenu;
    }

    // --- 1. MENU CONTROLS ---

    public void TogglePauseMenu(InputAction.CallbackContext context)
    {
        if (pauseCanvas != null) pauseCanvas.SetActive(!pauseCanvas.activeSelf);
    }
    public void pauseAllOnGoingOperation()
    {
        if (!isPaused)
        {
            TurnOffMic();
            speechAnalyzer.PauseAnalysis();
            if (gazeTracker != null) gazeTracker.PauseTracking();
            if (presentationTimer != null) presentationTimer.PauseTimer();
            isPaused = true;
        }
    }
    public void unpauseAllOnGoingOperation()
    {
        if (isPaused)
        {
            if (!isRecording) TurnOnMic();
            speechAnalyzer.ResumeAnalysis();
            if (gazeTracker != null) gazeTracker.ResumeTracking();
            if (presentationTimer != null) presentationTimer.ResumeTimer();
            isPaused = false;
        }
    }

    public void ExitToLobby()
    {
        // 1. [TỐI ƯU RAM] - Đổ sạch âm thanh cũ
        allAudioChunks.Clear();
        if (tempRecordingClip != null)
        {
            Destroy(tempRecordingClip);
            tempRecordingClip = null;
        }
        // 2. Dọn dẹp hệ thống Micro & Xóa file ghi âm khỏi RAM
        //if (activeMicSource != null) activeMicSource.Stop();
        Microphone.End(hardwareMicName);
        isRecording = false;
        pauseCanvas.SetActive(false);

        if (micOnImage != null) micOnImage.SetActive(false);
        if (micOffImage != null) micOffImage.SetActive(true);

        // 3. NGẮT VÀ DỌN DẸP HỆ THỐNG ĐÁNH GIÁ (Gaze & Timer)
        if (gazeTracker != null) gazeTracker.StopAndExportTracking(); // Xuất file báo cáo rồi nghỉ
        if (presentationTimer != null) presentationTimer.ForceResetTimer(); // Ép đồng hồ về 0 và nghỉ

        // 4. Dịch chuyển về Lobby
        if (vrPlayer != null && lobbySpawnPoint != null)
        {
            float zOffset = 4.0f;
            float rotationOffset = 0f;
            Vector3 finalPosition = lobbySpawnPoint.position - (lobbySpawnPoint.forward * zOffset);
            Vector3 finalRotation = lobbySpawnPoint.eulerAngles + new Vector3(0f, rotationOffset, 0f);

            vrPlayer.transform.position = finalPosition;
            vrPlayer.transform.rotation = Quaternion.Euler(finalRotation);
        }

        // 5. [TỐI ƯU RAM CUỐI CÙNG] - Ép Unity dọn rác triệt để
        System.GC.Collect();
        Resources.UnloadUnusedAssets();
        Debug.Log("[System] Đã dọn dẹp RAM và thoát ra Lobby an toàn!");
    }

    public void ExitGame()
    {
        Debug.Log("Thoát Game!");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void StartQaAPhase()
    {
        if (presentationTimer != null)
        {
            presentationTimer.CalculatePresentationScore();
            savedPresentationDuration = presentationTimer.lastDuration;
            presentationTimer.StartQnATimer();
        }

        SaveRecordingToFile();
        SendAudioForQuestion();

        gazeTracker.StopAndExportTracking();
        timerText.text = "The system is currently recording the answer to this question";
        titleText.text = "Q&A Session";
        titleBackground.color = new Color32(252, 129, 131, 255);
    }

    public void EndQaAPhase()
    {
        Debug.LogError("Nộp bài");
        if (isUploading) return;
        isUploading = true;

        // --- DỪNG MỌI HOẠT ĐỘNG PHÂN TÍCH ---
        TurnOffMic(); 
        if (speechAnalyzer != null) speechAnalyzer.StopAndGenerateReport();
        if (presentationTimer != null) presentationTimer.StopTimer();
        if (gazeTracker != null) gazeTracker.StopAndExportTracking();
        // -------------------------------------

        if (QuestionDialogManager.Instance != null)
        {
            QuestionDialogManager.Instance.ShowLoadingState("Đang nộp toàn bộ câu trả lời...\nVui lòng đợi hệ thống xử lý!");
        }

        string sessionId = SessionManager.SessionId;
        if (string.IsNullOrEmpty(sessionId))
        {
            Debug.LogError("❌ Chưa có session_id để nộp bài!");
            if (QuestionDialogManager.Instance != null) QuestionDialogManager.Instance.ShowErrorState("Lỗi: Không tìm thấy phiên làm việc!");
            isUploading = false;
            return;
        }

        // Gom các file Question_X.wav
        List<string> filesToUpload = new List<string>();
        string saveDirectory = Application.persistentDataPath;

        for (int i = 0; i < 10; i++)
        {
            string filePath = Path.Combine(saveDirectory, $"Question_{i}.wav");
            if (File.Exists(filePath)) filesToUpload.Add(filePath);
        }

        if (filesToUpload.Count == 0)
        {
            Debug.LogWarning("⚠️ Không tìm thấy file âm thanh câu trả lời nào!");
            // Nếu không có câu trả lời nào, có thể nhảy thẳng tới chấm điểm bài thuyết trình luôn
            StartCoroutine(EvaluateCoroutine(sessionId));
            return;
        }

        Debug.Log($"📦 Bắt đầu gửi {filesToUpload.Count} câu trả lời...");

        // Kích hoạt mắt xích đầu tiên
        StartCoroutine(SubmitBatchAudioCoroutine(sessionId, filesToUpload));
    }

    // ==========================================
    // 2. MẮT XÍCH 1: GỬI HÀNG LOẠT FILE AUDIO
    // ==========================================
    private IEnumerator SubmitBatchAudioCoroutine(string sessionId, List<string> filePaths)
    {
        string urlWithParams = $"{backendBaseUrl}/submit?session_id={UnityWebRequest.EscapeURL(sessionId)}";
        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();

        foreach (string path in filePaths)
        {
            byte[] fileData = File.ReadAllBytes(path);
            formData.Add(new MultipartFormFileSection("audio_files", fileData, Path.GetFileName(path), "audio/wav"));
        }

        using (UnityWebRequest request = UnityWebRequest.Post(urlWithParams, formData))
        {
            request.SetRequestHeader("ngrok-skip-browser-warning", "69420");
            request.timeout = 300;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"✅ NỘP AUDIO THÀNH CÔNG. Chuẩn bị chấm điểm...");
                if (QuestionDialogManager.Instance != null)
                {
                    QuestionDialogManager.Instance.ShowLoadingState("Đã nhận bài!\nAI đang phân tích và chấm điểm toàn diện...\n(Quá trình này có thể mất 1-2 phút)");
                }

                // TỰ ĐỘNG GỌI MẮT XÍCH THỨ 2
                StartCoroutine(EvaluateCoroutine(sessionId));
            }
            else
            {
                isUploading = false;
                Debug.LogError($"❌ Lỗi nộp bài: {request.error}");
                if (QuestionDialogManager.Instance != null) QuestionDialogManager.Instance.ShowErrorState("Nộp bài thất bại. Vui lòng thử lại!");
            }
        }
    }

    // ==========================================
    // 3. MẮT XÍCH 2: GỌI CHẤM ĐIỂM & LƯU KẾT QUẢ
    // ==========================================
    private IEnumerator EvaluateCoroutine(string sessionId)
    {
        // 1. Chốt các chỉ số trước khi gửi
        if (presentationTimer != null) presentationTimer.CalculatePresentationScore();
        if (speechAnalyzer != null) speechAnalyzer.GenerateAC4Report();

        string url = $"{backendBaseUrl}/evaluate";

        // Thu thập dữ liệu từ các Manager
        PresentationEvaluationReport eyeContactReport = (gazeTracker != null) ? gazeTracker.GetCurrentReport() : null;
        
        EvaluateRequest evalRequest = new EvaluateRequest
        {
            session_id = sessionId,
            time_management_score = presentationTimer != null ? presentationTimer.lastScore : 0,
            eye_contact_score = eyeContactReport != null ? Mathf.RoundToInt(eyeContactReport.interactionPercentage) : 0,
            volume_score = speechAnalyzer != null ? speechAnalyzer.finalVolumeScore : 0,
            eye_contact_zones = new List<float>(),
            eye_contact_zone_names = new List<string>(),
            eye_contact_advice = eyeContactReport != null ? eyeContactReport.interactionGrade : "N/A",
            presentation_duration = Mathf.RoundToInt((savedPresentationDuration > 0) ? savedPresentationDuration : (presentationTimer != null ? presentationTimer.lastDuration : 0)),
            target_time = Mathf.RoundToInt(presentationTimer != null ? presentationTimer.presentationDuration : 0),
            qa_duration = Mathf.RoundToInt((presentationTimer != null && presentationTimer.currentPhase == PresentationTimer.SessionPhase.QnA) ? presentationTimer.lastDuration : 0),
            quiet_ratio = (speechAnalyzer != null && speechAnalyzer.totalAnalyzedTime > 0) ? (speechAnalyzer.timeTooQuiet / speechAnalyzer.totalAnalyzedTime) : 0,
            loud_ratio = (speechAnalyzer != null && speechAnalyzer.totalAnalyzedTime > 0) ? (speechAnalyzer.timeTooLoud / speechAnalyzer.totalAnalyzedTime) : 0,
            avg_volume = speechAnalyzer != null ? speechAnalyzer.AvgVolume : 0
        };

        if (eyeContactReport != null && eyeContactReport.targetDetails != null)
        {
            foreach (var detail in eyeContactReport.targetDetails)
            {
                evalRequest.eye_contact_zones.Add(detail.viewPercentage);
                evalRequest.eye_contact_zone_names.Add(detail.displayName);
            }
        }

        string jsonBody = JsonUtility.ToJson(evalRequest);
        Debug.Log($"[Evaluate] Sending JSON: {jsonBody}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("ngrok-skip-browser-warning", "69420");
            request.timeout = 600;

            yield return request.SendWebRequest();
            isUploading = false; // Mở khóa hệ thống

            if (request.result == UnityWebRequest.Result.Success)
            {
                byte[] pdfData = request.downloadHandler.data;
                Debug.Log($"✅ ĐÁNH GIÁ THÀNH CÔNG! Nhận file PDF ({pdfData.Length} bytes)");

                // Lưu file PDF
                string pdfPath = SaveEvaluationPdfToFile(sessionId, pdfData);
                
                // Load PDF lên UI sau một khoảng delay ngắn để hệ thống file Android kịp cập nhật
                StartCoroutine(LoadPdfDelayed(pdfPath));

                // if (QuestionDialogManager.Instance != null) QuestionDialogManager.Instance.HideDialog();

                // HOÀN TẤT DÂY CHUYỀN -> GỌI MÀN HÌNH REPORT
                StartReportPhase();
            }
            else
            {
                Debug.LogError($"❌ Lỗi chấm điểm: {request.error}\nChi tiết: {request.downloadHandler.text}");
                if (QuestionDialogManager.Instance != null) QuestionDialogManager.Instance.ShowErrorState("Server AI báo lỗi khi chấm điểm!");
            }
        }
    }

    private IEnumerator LoadPdfDelayed(string pdfPath)
    {
        // Chờ 1.5 giây cho chắc chắn file đã được ghi xong hoàn toàn trên Android
        yield return new WaitForSeconds(1.5f);

        if (reportPdfViewer != null && !string.IsNullOrEmpty(pdfPath))
        {
            reportPdfViewer.LoadPDF(pdfPath, true);
        }
    }

    // ==========================================
    // 4. HÀM PHỤ TRỢ: LƯU PDF VÀO MÁY
    // ==========================================
    private string SaveEvaluationPdfToFile(string sessionId, byte[] pdfData)
    {
        try
        {
            string safeSessionId = string.Join("_", sessionId.Split(Path.GetInvalidFileNameChars()));
            string filePath = Path.Combine(Application.persistentDataPath, $"EvaluationReport_{safeSessionId}.pdf");
            File.WriteAllBytes(filePath, pdfData);
            Debug.Log($"📄 Đã lưu file PDF kết quả tại: {filePath}");
            return filePath;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Lỗi khi ghi file PDF: {e.Message}");
            return null;
        }
    }

    public void SendAudioForQuestion()
    {
        // Nếu đang gửi rồi thì chặn luôn không cho chạy tiếp
        if (isUploading)
        {
            Debug.Log("⏳ Đang xử lý, vui lòng đợi...");
            return;
        }

        // 1. BẬT KHÓA VÀ HIỆN BẢNG LOADING LÊN VR
        isUploading = true;
        if (QuestionDialogManager.Instance != null)
        {
            QuestionDialogManager.Instance.ShowLoadingState("AI đang phân tích bài thuyết trình...\nVui lòng đợi trong giây lát!");
        }

        string wavPath = SessionManager.WavPath;
        if (string.IsNullOrEmpty(wavPath))
        {
            Debug.LogError("❌ Không có file wav để gửi!");
            return;
        }

        string sessionId = SessionManager.SessionId;
        if (string.IsNullOrEmpty(sessionId))
        {
            Debug.LogError("❌ Chưa có session_id! Upload context trước.");
            return;
        }

        StartCoroutine(UploadAudioCoroutine(wavPath, sessionId, mode)); // Bạn nhớ truyền biến mode vào nhé
    }

    private IEnumerator UploadAudioCoroutine(string wavPath, string sessionId, string uploadMode)
    {
        Debug.Log($"📤 Đang gửi audio... Session: {sessionId}, Mode: {uploadMode}");

        byte[] wavBytes = File.ReadAllBytes(wavPath);
        string fileName = Path.GetFileName(wavPath);

        WWWForm form = new WWWForm();
        form.AddBinaryData("audio_file", wavBytes, fileName, "audio/wav");

        string urlWithParams = $"{backendBaseUrl}/generate-question" +
            $"?session_id={UnityWebRequest.EscapeURL(sessionId)}" +
            $"&mode={UnityWebRequest.EscapeURL(uploadMode)}";

        using (UnityWebRequest request = UnityWebRequest.Post(urlWithParams, form))
        {
            request.SetRequestHeader("ngrok-skip-browser-warning", "69420");
            // request.certificateHandler = new BypassCertificate(); // Mở comment này nếu bạn đang dùng
            request.timeout = 180;

            yield return request.SendWebRequest();

            // 2. MỞ KHÓA KHI API TRẢ VỀ KẾT QUẢ
            isUploading = false;

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResult = request.downloadHandler.text;
                Debug.Log($"✅ THÀNH CÔNG: {jsonResult}");

                GenerateQuestionResponse response = JsonUtility.FromJson<GenerateQuestionResponse>(jsonResult);

                // Khi có câu hỏi, bảng Dialog sẽ tự động đè giao diện Loading bằng danh sách câu hỏi
                if (QuestionDialogManager.Instance != null && response.questions != null)
                {
                    QuestionDialogManager.Instance.StartQuestionSession(response.questions);
                }
            }
            else
            {
                // XỬ LÝ KHI BỊ LỖI
                Debug.LogError($"❌ Lỗi: {request.error}");
                if (QuestionDialogManager.Instance != null)
                {
                    QuestionDialogManager.Instance.ShowErrorState("Lỗi kết nối tới AI. Vui lòng thử lại!");
                }
            }
        }
    }

    public void StartReportPhase()
    {
        titleText.text = "General Report";
        titleBackground.color = new Color(0f, 0f, 0f, 0f);

        presentationTimer.CalculatePresentationScore();
    }

    public void RetakePresentation()
    {
        // 1. Reset UI back to normal
        timerText.text = "The system is currently recording your presentation";
        titleText.text = "Presentation Session";
        titleBackground.color = new Color32(113, 194, 236, 255);

        // 2. Stop current recording & clear old audio in RAM
        TurnOffMic();
        allAudioChunks.Clear();
        if (pdf1.isActiveAndEnabled) pdf1.GoToPage(0);
        if (pdf2.isActiveAndEnabled) pdf2.GoToPage(0);
        if (tempRecordingClip != null)
        {
            Destroy(tempRecordingClip);
            tempRecordingClip = null;
        }

        // ==========================================
        // THÊM MỚI: XÓA SẠCH FILE QUESTION*.WAV TRÊN Ổ CỨNG
        // ==========================================
        try
        {
            string saveDirectory = Application.persistentDataPath;
            string[] oldQuestionFiles = Directory.GetFiles(saveDirectory, "Question_*.wav");

            foreach (string filePath in oldQuestionFiles)
            {
                File.Delete(filePath);
            }
            Debug.Log($"🗑️ Đã xóa sạch {oldQuestionFiles.Length} file Question.wav cũ!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Lỗi khi xóa file cũ: {e.Message}");
        }
        // ==========================================

        // 3. Reset timer and gaze tracking
        if (gazeTracker != null) gazeTracker.StopAndExportTracking();

        float timeInSecond = (timePicker != null) ? timePicker.GetTimeInSeconds() : 0f;
        presentationTimer.StartPresentationTimer(timeInSecond);

        // 4. Restart everything fresh
        if (presentationTimer != null) presentationTimer.ResumeTimer();
        if (gazeTracker != null) gazeTracker.ResumeTracking();
        TurnOnMic();
        speechAnalyzer.StartAnalysis(hardwareMicName, tempRecordingClip);

        isPaused = false;
        Debug.Log("Retake started — everything reset to normal.");
    }
}