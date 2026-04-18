using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(CanvasGroup))]
public class QuestionDialogManager : MonoBehaviour
{
    public static QuestionDialogManager Instance;

    [Header("UI References")]
    public TextMeshProUGUI questionTextUI;
    [Tooltip("Text này giờ sẽ gánh luôn cả Nhiệm vụ Status và Đếm ngược")]
    public TextMeshProUGUI statusTextUI;
    [Tooltip("Dùng để hiển thị: Câu 1/10")]
    public TextMeshProUGUI progressTextUI;

    private CanvasGroup canvasGroup;

    [Header("Settings")]
    public float preparationTime = 15f;
    public float fadeDuration = 0.5f;

    private List<string> currentQuestionList = new List<string>();
    private int currentQuestionIndex = 0;
    private bool isRecordingPhase = false; // Cờ kiểm soát việc ghi âm để tránh lỗi "No audio to save"

    // Quản lý chặt chẽ các Coroutine đang chạy
    private Coroutine currentTimerRoutine;
    private Coroutine currentFadeRoutine;

    [Header("Dependencies")]
    public PauseMenuManager pauseMenuManager;
    public NpcManager npcManager;

    [Header("Room Setup (Vị trí hiển thị UI)")]
    public ModeManager modeManager; // Để biết đang ở phòng nào
    [Tooltip("Kéo Empty GameObject chứa tọa độ UI phòng Normal vào đây")]
    public Transform normalRoomAnchor;
    [Tooltip("Kéo Empty GameObject chứa tọa độ UI phòng Defense vào đây")]
    public Transform defenseRoomAnchor;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject); // Đảm bảo chỉ có 1 instance tồn tại

        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Start()
    {
        canvasGroup.alpha = 0;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    // ==========================================
    // HÀM MỚI: TỰ ĐỘNG DỊCH CHUYỂN UI VỀ ĐÚNG PHÒNG
    // ==========================================
    private void RepositionUIToCurrentRoom()
    {
        if (modeManager == null) return;

        // Dựa vào mode để chọn điểm neo (giống logic bên NpcManager của bạn)
        Transform targetAnchor = (modeManager.selectedMode == "Defense") ? defenseRoomAnchor : normalRoomAnchor;

        if (targetAnchor != null)
        {
            // Bê nguyên cái Canvas này đặt vào tọa độ và góc xoay của điểm neo
            transform.position = targetAnchor.position;
        }
    }

    public void StartQuestionSession(List<string> questions)
    {
        if (questions == null || questions.Count == 0) return;

        currentQuestionList = questions;
        currentQuestionIndex = 0;

        RepositionUIToCurrentRoom(); // Dịch chuyển UI trước khi bật lên
        TriggerFadeUI(1f);
        DisplayCurrentQuestion();
    }

    private void DisplayCurrentQuestion()
    {
        isRecordingPhase = false; // Reset cờ ghi âm

        if (npcManager != null) npcManager.GetNextNPC();

        string currentQ = currentQuestionList[currentQuestionIndex];
        if (questionTextUI != null) questionTextUI.text = $"\"{currentQ}\"";

        if (progressTextUI != null)
        {
            progressTextUI.text = $"Câu {currentQuestionIndex + 1} / {currentQuestionList.Count}";
        }

        if (statusTextUI != null)
        {
            statusTextUI.text = $"Chuẩn bị trả lời... ({preparationTime:0}s)";
            statusTextUI.color = Color.white;
        }

        if (currentTimerRoutine != null) StopCoroutine(currentTimerRoutine);
        currentTimerRoutine = StartCoroutine(PreparationCountdown());
    }

    public void NextQuestion()
    {
        // CHỈ LƯU FILE NẾU ĐÃ QUA THỜI GIAN ĐẾM NGƯỢC
        if (isRecordingPhase)
        {
            if (pauseMenuManager != null) pauseMenuManager.SaveRecordingQuestionToFile(currentQuestionIndex);
        }

        currentQuestionIndex++;

        if (currentQuestionIndex < currentQuestionList.Count)
        {
            RepositionUIToCurrentRoom(); // Chắc cú đặt lại vị trí
            TriggerFadeUI(1f);
            DisplayCurrentQuestion();
        }
        else
        {
            EndSession();
        }
    }

    private IEnumerator PreparationCountdown()
    {
        float timeLeft = preparationTime;

        while (timeLeft > 0)
        {
            if (statusTextUI != null)
            {
                statusTextUI.text = $"Chuẩn bị trả lời... ({Mathf.CeilToInt(timeLeft)}s)";
                statusTextUI.color = (timeLeft <= 3f) ? new Color(1f, 0.4f, 0.4f) : Color.white;
            }

            yield return new WaitForSeconds(1f);
            timeLeft -= 1f;
        }

        if (statusTextUI != null)
        {
            statusTextUI.text = "BẮT ĐẦU GHI ÂM!";
            statusTextUI.color = Color.green;
        }

        isRecordingPhase = true; // Bật cờ cho phép lưu file

        yield return new WaitForSeconds(2f);
        if (pauseMenuManager != null) pauseMenuManager.TurnOnMic();

        HideDialog();
    }

    public void EndSession()
    {
        HideDialog();
        if (pauseMenuManager != null) pauseMenuManager.EndQaAPhase();
    }

    public void HideDialog() => TriggerFadeUI(0f);

    private void TriggerFadeUI(float targetAlpha)
    {
        if (currentFadeRoutine != null) StopCoroutine(currentFadeRoutine);
        currentFadeRoutine = StartCoroutine(FadeUI(targetAlpha));
    }

    private IEnumerator FadeUI(float targetAlpha)
    {
        float startAlpha = canvasGroup.alpha;
        float time = 0;

        if (targetAlpha > 0)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = targetAlpha;

        if (targetAlpha == 0)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    public void ShowLoadingState(string loadingMessage)
    {
        if (questionTextUI != null) questionTextUI.text = "";
        if (progressTextUI != null) progressTextUI.text = "";
        if (statusTextUI != null)
        {
            statusTextUI.text = loadingMessage;
            statusTextUI.color = Color.white;
        }

        if (currentTimerRoutine != null) StopCoroutine(currentTimerRoutine);

        RepositionUIToCurrentRoom(); // Dịch chuyển UI trước khi bật
        TriggerFadeUI(1f);
    }

    public void ShowErrorState(string errorMessage)
    {
        if (questionTextUI != null) questionTextUI.text = "";
        if (progressTextUI != null) progressTextUI.text = "";
        if (statusTextUI != null)
        {
            statusTextUI.text = errorMessage;
            statusTextUI.color = new Color(1f, 0.4f, 0.4f);
        }

        if (currentTimerRoutine != null) StopCoroutine(currentTimerRoutine);
        currentTimerRoutine = StartCoroutine(AutoCloseError());

        TriggerFadeUI(1f);
        RepositionUIToCurrentRoom(); // Dịch chuyển UI trước khi bật
    }

    private IEnumerator AutoCloseError()
    {
        yield return new WaitForSeconds(4f);
        HideDialog();
    }
}