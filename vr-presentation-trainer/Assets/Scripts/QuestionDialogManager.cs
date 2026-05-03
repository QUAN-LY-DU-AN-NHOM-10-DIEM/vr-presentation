using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Quản lý giao diện hộp thoại câu hỏi trong phòng thuyết trình.
/// Tương tác trực tiếp với GameController để đồng bộ trạng thái.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class QuestionDialogManager : MonoBehaviour
{
    public static QuestionDialogManager Instance;

    [Header("UI References")]
    public TextMeshProUGUI questionTextUI;
    public TextMeshProUGUI statusTextUI;
    public TextMeshProUGUI progressTextUI;
    public GameObject questionCanvas; // Canvas chứa các chữ câu hỏi
    public GameObject reportObject;   // Object chứa PDF Viewer kết quả

    private CanvasGroup canvasGroup;

    [Header("Settings")]
    public float preparationTime = 15f;
    public float fadeDuration = 0.5f;
    
    private List<string> currentQuestionList = new List<string>();
    // Loại bỏ biến local để dùng chung với GameController
    // private int currentQuestionIndex = 0; 

    private Coroutine countdownCoroutine; // Để có thể dừng Coroutine khi Skip
    private Coroutine fadeCoroutine;      // Để quản lý hiệu ứng ẩn/hiện bảng

    [Header("Room Setup")]
    public ModeManager modeManager;
    public Transform normalRoomAnchor;
    public Transform defenseRoomAnchor;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Start()
    {
        canvasGroup.alpha = 0;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void RepositionUIToCurrentRoom()
    {
        if (modeManager == null) return;
        Transform targetAnchor = (modeManager.selectedMode == "Defense") ? defenseRoomAnchor : normalRoomAnchor;
        if (targetAnchor != null) 
        {
            Debug.Log($"[UI Reposition] Moving {gameObject.name} to {targetAnchor.name} at {targetAnchor.position}");
            transform.position = targetAnchor.position;
        }
    }

    /// <summary>
    /// Bắt đầu phiên Q&A với danh sách câu hỏi từ API.
    /// </summary>
    public void StartQuestionSession(List<string> questions)
    {
        if (questions == null || questions.Count == 0) return;
        currentQuestionList = questions;

        if (NpcManager.Instance != null) NpcManager.Instance.PrepareNpcQueue();

        ShowQuestionUI(); // Đảm bảo hiện bảng câu hỏi, ẩn bảng report
        RepositionUIToCurrentRoom();
        TriggerFadeUI(1f);
        StartNextQuestionFlow();
    }

    private void StartNextQuestionFlow()
    {
        GameController.Instance.StartNextQuestion(currentQuestionList);
        DisplayCurrentQuestion();
    }

    private void DisplayCurrentQuestion()
    {
        if (NpcManager.Instance != null) NpcManager.Instance.GetNextNPC();

        int currentIndex = GameController.Instance.CurrentQuestionIndex;
        string currentQ = currentQuestionList[currentIndex];
        questionTextUI.text = $"\"{currentQ}\"";
        progressTextUI.text = $"Câu {currentIndex + 1} / {currentQuestionList.Count}";
        
        if (countdownCoroutine != null) StopCoroutine(countdownCoroutine);
        countdownCoroutine = StartCoroutine(PreparationCountdown());
    }

    public void SkipPreparation()
    {
        // Hàm này giờ sẽ gọi chung logic chuyển câu hỏi
        OnFinishAnswerClicked();
    }

    private IEnumerator PreparationCountdown()
    {
        float t = preparationTime;

        while (t > 0)
        {
            statusTextUI.text = $"Chuẩn bị trả lời trong: {Mathf.CeilToInt(t)}s";
            statusTextUI.color = (t <= 3f) ? Color.red : Color.white;
            yield return null;
            t -= Time.deltaTime;
        }

        statusTextUI.text = "BẮT ĐẦU GHI ÂM!";
        statusTextUI.color = Color.green;
        
        countdownCoroutine = null;
        GameController.Instance.StartAnswering();
        
        // Ẩn bảng sau khi đọc xong để người dùng tập trung trả lời
        HideDialog();
    }

    public void OnFinishAnswerClicked()
    {
        // 0. Nếu chưa có câu hỏi thì không cho bấm
        if (currentQuestionList == null || currentQuestionList.Count == 0) return;

        // 1. Dừng đếm ngược nếu đang trong phase chuẩn bị
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
            Debug.Log("[Q&A] Bỏ qua giai đoạn chuẩn bị hoặc bỏ qua câu hỏi.");
        }

        // 2. Gọi GameController xử lý kết thúc (Lưu audio nếu đang ghi âm, hoặc chỉ dừng nếu đang đọc)
        GameController.Instance.FinishAnswer();

        // Không tăng index ở đây nữa vì GameController.StartNextQuestion sẽ tăng
        int nextIndex = GameController.Instance.CurrentQuestionIndex + 1;
        
        if (nextIndex < currentQuestionList.Count)
        {
            TriggerFadeUI(1f);
            StartNextQuestionFlow();
        }
        else
        {
            GameController.Instance.FinishAll();
        }
    }

    public void OnFinishQnAClicked()
    {
        GameController.Instance.FinishAll();
    }

    public void HideDialog() => TriggerFadeUI(0f);

    private void TriggerFadeUI(float targetAlpha)
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeUI(targetAlpha));
    }

    private IEnumerator FadeUI(float targetAlpha)
    {
        float startAlpha = canvasGroup.alpha;
        float time = 0;
        canvasGroup.interactable = targetAlpha > 0;
        canvasGroup.blocksRaycasts = targetAlpha > 0;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = targetAlpha;
    }

    public void ShowLoadingState(string message)
    {
        // Ẩn nội dung câu hỏi cũ
        if (questionCanvas != null) questionCanvas.SetActive(false);
        if (reportObject != null) reportObject.SetActive(false);

        questionTextUI.text = "";
        progressTextUI.text = "";
        statusTextUI.text = message;
        statusTextUI.color = Color.white;
        RepositionUIToCurrentRoom();
        TriggerFadeUI(1f);
    }

    public void ShowErrorState(string error)
    {
        statusTextUI.text = error;
        statusTextUI.color = Color.red;
        TriggerFadeUI(1f);
        Invoke("HideDialog", 3f);
    }

    public void ShowReportUI()
    {
        // 1. Hủy tất cả các lệnh chạy ngầm
        CancelInvoke();
        StopAllCoroutines();
        fadeCoroutine = null;
        countdownCoroutine = null;
        
        // 2. Chuyển đổi Object
        if (questionCanvas != null && questionCanvas != gameObject) 
            questionCanvas.SetActive(false);
            
        if (reportObject != null) 
            reportObject.SetActive(true);
        
        // 4. Ép hiển thị tuyệt đối
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        
        Debug.Log("[UI] Switched to Report Mode. Force visible. Alpha: " + canvasGroup.alpha);
    }

    public void ShowQuestionUI()
    {
        if (questionCanvas != null) questionCanvas.SetActive(true);
        if (reportObject != null) reportObject.SetActive(false);
    }
}