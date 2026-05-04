using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityPdfViewer;
using Assets.CustomPdfViewer.Scripts;

/// <summary>
/// Quản lý giao diện Menu tạm dừng và các tương tác UI chính trong phòng thuyết trình.
/// Đã được rút gọn để chỉ tập trung vào hiển thị UI.
/// </summary>
public class PauseMenuManager : MonoBehaviour
{
    public static PauseMenuManager Instance { get; private set; }

    [Header("Giao diện Menu")]
    public GameObject pauseCanvas;
    public InputActionReference menuButtonInput;
    public GameObject micOnImage;
    public GameObject micOffImage;

    [Header("Thông tin Phiên thuyết trình")]
    public TextMeshProUGUI titleText;
    public Image titleBackground;
    public TextMeshProUGUI timerText;

    [Header("Báo cáo & Kết quả")]
    public CustomPdfViewerUI reportPdfViewer;
    public CustomPdfViewerUI pdf1;
    public CustomPdfViewerUI pdf2;

    [Header("Tham chiếu Điều hướng")]
    public GameObject vrPlayer;
    public Transform lobbySpawnPoint;
    public GameObject playerLeftHand;
    public Vector3 menuPositionOffset = new Vector3(-0.35f, 0.01f, -0.1f);
    public Vector3 menuRotationOffset = new Vector3(0f, -180f, -90f);

    private bool isPaused = false;
    private bool isUploading = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        SetupHandMenu();
        if (pauseCanvas != null) pauseCanvas.SetActive(false);
        UpdateMicUI(false);
    }

    private void OnEnable()
    {
        if (menuButtonInput != null)
            menuButtonInput.action.performed += TogglePauseMenu;
        
        GameController.Instance.OnStateChanged.AddListener(OnGameStateChanged);
    }

    private void OnDisable()
    {
        if (menuButtonInput != null)
            menuButtonInput.action.performed -= TogglePauseMenu;

        if (GameController.Instance != null)
            GameController.Instance.OnStateChanged.RemoveListener(OnGameStateChanged);
    }

    private void SetupHandMenu()
    {
        if (playerLeftHand != null && pauseCanvas != null)
        {
            pauseCanvas.transform.SetParent(playerLeftHand.transform);
            pauseCanvas.transform.localPosition = menuPositionOffset;
            pauseCanvas.transform.localEulerAngles = menuRotationOffset;
        }
    }

    public void TogglePauseMenu(InputAction.CallbackContext context)
    {
        if (pauseCanvas != null)
        {
            isPaused = !pauseCanvas.activeSelf;
            pauseCanvas.SetActive(isPaused);
        }
    }

    private void pauseAllOperations()
    {
        Time.timeScale = 0f;
        GameController.Instance.PauseGame();
    }

    private void unpauseAllOperations()
    {
        Time.timeScale = 1f;
        GameController.Instance.ResumeGame();
    }

    /// <summary>
    /// Đồng bộ UI dựa trên trạng thái của GameController.
    /// </summary>
    private void OnGameStateChanged(GameState newState)
    {
        switch (newState)
        {
            case GameState.Presentation:
                titleText.text = "Presentation Session";
                timerText.text = "The system is currently recording your presentation";
                titleBackground.color = new Color32(113, 194, 236, 255);
                UpdateMicUI(true);
                break;
            case GameState.Break:
                titleText.text = "Q&A Session (Break)";
                timerText.text = "Waiting for next question...";
                UpdateMicUI(false);
                break;
            case GameState.QuestionAnswering:
                titleText.text = "Q&A Session";
                timerText.text = "The system is currently recording your answer";
                titleBackground.color = new Color32(252, 129, 131, 255);
                UpdateMicUI(true);
                break;
            case GameState.Finished:
                titleText.text = "Final Report Phase";
                UpdateMicUI(false);
                break;
        }
    }

    public void UpdateMicUI(bool isRecording)
    {
        if (micOnImage != null) micOnImage.SetActive(isRecording);
        if (micOffImage != null) micOffImage.SetActive(!isRecording);
    }

    public void StartPresentation() 
    {
        CleanupOldAudioFiles();
        GameController.Instance.StartPresentation();
    }

    public void RetakePresentation()
    {
        // Xóa sạch file cũ
        CleanupOldAudioFiles();
        MicrophoneManager.Instance.ClearCache();
        
        if (pdf1.isActiveAndEnabled) pdf1.GoToPage(0);
        if (pdf2.isActiveAndEnabled) pdf2.GoToPage(0);

        GameController.Instance.StartPresentation();
    }

    private void CleanupOldAudioFiles()
    {
        try
        {
            string saveDirectory = Application.persistentDataPath;
            
            // Xóa file câu hỏi
            string[] qFiles = Directory.GetFiles(saveDirectory, "Question_*.wav");
            foreach (string f in qFiles) File.Delete(f);
            
            // Xóa file thuyết trình chính
            string presFile = Path.Combine(saveDirectory, "presentation.wav");
            if (File.Exists(presFile)) File.Delete(presFile);

            // Xóa các file báo cáo PDF cũ
            string[] pdfFiles = Directory.GetFiles(saveDirectory, "Report_*.pdf");
            foreach (string f in pdfFiles) File.Delete(f);

            Debug.Log("🧹 Đã dọn dẹp sạch sẽ dữ liệu cũ trong thư mục.");
        }
        catch (System.Exception e) { Debug.LogError($"Error cleanup: {e.Message}"); }
    }

    /// <summary>
    /// Kết thúc thuyết trình và yêu cầu AI tạo câu hỏi phản biện.
    /// </summary>
    public void RequestQuestionsAI()
    {
        if (isUploading) return;
        
        // 1. Dừng thuyết trình và lưu file âm thanh
        GameController.Instance.FinishPresentation();
        
        // 2. Hiển thị trạng thái đang tải trên bảng câu hỏi
        if (QuestionDialogManager.Instance != null)
            QuestionDialogManager.Instance.ShowLoadingState("AI đang phân tích bài nói của bạn\nvà tạo câu hỏi phản biện...");

        // 3. Gọi API lấy câu hỏi
        string wavPath = ApiManager.Instance.CurrentWavPath;
        string sessionId = ApiManager.Instance.CurrentSessionId;
        
        // Lấy mode từ ModeManager
        string mode = "Normal";
        GameObject modeObj = GameObject.Find("ModeManager"); // Tìm object mode nếu cần
        if (modeObj != null)
        {
            var mm = modeObj.GetComponent<ModeManager>();
            if (mm != null) mode = mm.selectedMode;
        }

        StartCoroutine(ApiManager.Instance.GenerateQuestions(wavPath, sessionId, mode, (success, response, err) => {
            if (success)
            {
                // Bắt đầu phiên Q&A với danh sách câu hỏi nhận được
                if (QuestionDialogManager.Instance != null)
                    QuestionDialogManager.Instance.StartQuestionSession(response.questions);
            }
            else
            {
                if (QuestionDialogManager.Instance != null)
                    QuestionDialogManager.Instance.ShowErrorState("Không thể tạo câu hỏi: " + err);
            }
        }));
    }

    public void FinalSubmit()
    {
        if (isUploading) return;
        StartCoroutine(FinalSubmitWorkflow());
    }

    private IEnumerator FinalSubmitWorkflow()
    {
        isUploading = true;
        if (QuestionDialogManager.Instance != null)
            QuestionDialogManager.Instance.ShowLoadingState("Đang tổng hợp dữ liệu và chấm điểm...\nVui lòng đợi AI xử lý!");

        string sessionId = ApiManager.Instance.CurrentSessionId;
        
        // 1. Thu thập danh sách file audio Q&A
        List<string> audioFiles = new List<string>();
        string saveDir = Application.persistentDataPath;
        string[] files = Directory.GetFiles(saveDir, "Question_*.wav");
        foreach (string f in files) audioFiles.Add(f);

        // 2. Gửi batch audio (nếu có)
        if (audioFiles.Count > 0)
        {
            bool batchSuccess = false;
            yield return ApiManager.Instance.SubmitBatchAudio(sessionId, audioFiles, (success, err) => batchSuccess = success);
            if (!batchSuccess)
            {
                isUploading = false;
                QuestionDialogManager.Instance.ShowErrorState("Gửi audio thất bại!");
                yield break;
            }
        }

        // 3. Gọi Evaluate
        EvaluateRequest req = BuildEvaluateRequest(sessionId);
        yield return ApiManager.Instance.EvaluateSession(req, (success, pdfData, err) => {
            isUploading = false;
            if (success)
            {
                // 1. Giữ bảng lại và hiện thông báo hoàn tất
                if (QuestionDialogManager.Instance != null)
                {
                    // Thay vì ẩn, chúng ta hiện thông báo "Xong"
                    QuestionDialogManager.Instance.ShowLoadingState("Đã chấm điểm xong! Đang mở báo cáo...");
                }

                // 2. Lưu và Load PDF kết quả
                string pdfPath = SavePdf(sessionId, pdfData);
                Debug.Log("✅ Report saved at: " + pdfPath);
                StartCoroutine(LoadPdfDelayed(pdfPath));
                
                GameController.Instance.ChangeState(GameState.Finished);
            }
            else
            {
                if (QuestionDialogManager.Instance != null)
                    QuestionDialogManager.Instance.ShowErrorState("Lỗi chấm điểm: " + err);
            }
        });
    }

    private EvaluateRequest BuildEvaluateRequest(string sessionId)
    {
        var presentationTimer = GameController.Instance.presentationTimer;
        var speechAnalyzer = GameController.Instance.speechAnalyzer;
        var gazeTracker = GameController.Instance.gazeTracker;
        var gazeReport = gazeTracker.GetCurrentReport();

        // Chuẩn bị dữ liệu Eye Contact
        List<float> zones = new List<float>();
        List<string> zoneNames = new List<string>();
        foreach (var detail in gazeReport.targetDetails)
        {
            zones.Add(detail.viewPercentage);
            zoneNames.Add(detail.displayName);
        }

        // Tính toán các tỷ lệ âm lượng
        float totalTime = speechAnalyzer.totalAnalyzedTime > 0 ? speechAnalyzer.totalAnalyzedTime : 1f;
        float quietRatio = speechAnalyzer.timeTooQuiet / totalTime;
        float loudRatio = speechAnalyzer.timeTooLoud / totalTime;

        // Tạo lời khuyên tự động cho Eye Contact
        string advice = "Tốt! Bạn duy trì tương tác mắt ổn định.";
        if (gazeReport.interactionPercentage < 30) advice = "Bạn cần nhìn vào khán giả nhiều hơn, tránh nhìn xuống đất hoặc quá tập trung vào slide.";
        else if (gazeReport.interactionPercentage < 60) advice = "Khá tốt, nhưng hãy cố gắng bao quát toàn bộ phòng thay vì chỉ nhìn một phía.";

        EvaluateRequest req = new EvaluateRequest
        {
            session_id = sessionId,
            time_management_score = Mathf.RoundToInt(presentationTimer.lastScore),
            eye_contact_score = Mathf.RoundToInt(gazeReport.interactionPercentage),
            volume_score = speechAnalyzer.finalVolumeScore,
            eye_contact_zones = zones,
            eye_contact_zone_names = zoneNames,
            eye_contact_advice = advice,
            presentation_duration = Mathf.RoundToInt(presentationTimer.lastDuration),
            target_time = Mathf.RoundToInt(presentationTimer.presentationDuration),
            qa_duration = 0, // Sẽ được tính toán nếu cần, hiện tại để 0
            quiet_ratio = quietRatio,
            loud_ratio = loudRatio,
            avg_volume = speechAnalyzer.AvgVolume
        };

        Debug.Log($"📊 [Evaluate] Data prepared: Eye={req.eye_contact_score}, Vol={req.volume_score}, Time={req.time_management_score}");
        return req;
    }

    private string SavePdf(string sessionId, byte[] data)
    {
        string path = Path.Combine(Application.persistentDataPath, $"Report_{sessionId}.pdf");
        File.WriteAllBytes(path, data);
        return path;
    }

    private IEnumerator LoadPdfDelayed(string path)
    {
        yield return new WaitForSeconds(1.5f);
        if (reportPdfViewer != null) 
        {
            // Chuyển đổi giao diện sang bảng Report
            if (QuestionDialogManager.Instance != null)
                QuestionDialogManager.Instance.ShowReportUI(); 

            reportPdfViewer.gameObject.SetActive(true);
            reportPdfViewer.LoadPDF(path, true);
        }
    }

    public void ExitToLobby()
    {
        Time.timeScale = 1f;
        CleanupOldAudioFiles();
        MicrophoneManager.Instance.ClearCache();
        GameController.Instance.ChangeState(GameState.Lobby);
        
        if (vrPlayer != null && lobbySpawnPoint != null)
        {
            vrPlayer.transform.position = lobbySpawnPoint.position;
            vrPlayer.transform.rotation = lobbySpawnPoint.rotation;
        }
    }

    public void ExitGame()
    {
        Debug.Log("Thoát Game!");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}