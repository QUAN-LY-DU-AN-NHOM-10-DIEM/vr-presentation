using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Các trạng thái chính của một phiên thuyết trình (State Machine).
/// </summary>
public enum GameState
{
    Lobby,              // Ở màn hình sảnh
    Presentation,       // Đang thuyết trình chính
    Break,              // Nghỉ giữa các câu hỏi Q&A (tắt mic)
    QuestionReading,    // NPC đứng lên hỏi, user đọc câu hỏi
    QuestionAnswering,  // User đang trả lời câu hỏi (bật mic)
    Finished            // Kết thúc toàn bộ, đang xử lý kết quả
}

/// <summary>
/// Bộ điều khiển trung tâm (Tổng tư lệnh).
/// Quản lý vòng đời và sự chuyển đổi giữa các trạng thái của ứng dụng.
/// </summary>
public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    [Header("Trạng thái hiện tại")]
    public GameState currentState = GameState.Lobby;

    [Header("Sự kiện")]
    public UnityEvent<GameState> OnStateChanged;

    [Header("Tham chiếu hệ thống")]
    public SpeechAnalyzer speechAnalyzer;
    public GazeTrackingManager gazeTracker;
    public PresentationTimer presentationTimer;
    public TimePicker timePicker;

    public int CurrentQuestionIndex { get; private set; } = -1;

    private List<string> questionList = new List<string>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ChangeState(GameState newState)
    {
        currentState = newState;
        OnStateChanged?.Invoke(newState);
        Debug.Log($"[GameController] State changed to: {newState}");
    }

    /// <summary>
    /// Bắt đầu phiên thuyết trình chính.
    /// </summary>
    public void StartPresentation()
    {
        ChangeState(GameState.Presentation);
        
        // 1. Hardware
        MicrophoneManager.Instance.StartRecording();
        
        // 2. Analyzers
        if (speechAnalyzer != null) 
            speechAnalyzer.StartAnalysis(MicrophoneManager.Instance.CurrentMicName, MicrophoneManager.Instance.CurrentClip);
        
        if (gazeTracker != null) gazeTracker.ResumeTracking();
        
        // 3. Timer
        float targetTime = (timePicker != null) ? timePicker.GetTimeInSeconds() : 0f;
        if (presentationTimer != null)
        {
            presentationTimer.StartPresentationTimer(targetTime);
            presentationTimer.ResumeTimer();
        }
    }

    /// <summary>
    /// Kết thúc thuyết trình chính, chuẩn bị sang Q&A.
    /// </summary>
    public void FinishPresentation()
    {
        ChangeState(GameState.Break);
        
        // 1. Hardware
        MicrophoneManager.Instance.StopAndCache();
        
        // 2. Analyzers
        if (speechAnalyzer != null) speechAnalyzer.PauseAnalysis();
        if (gazeTracker != null) gazeTracker.PauseTracking();
        if (presentationTimer != null) presentationTimer.PauseTimer();

        // 3. Save Presentation Audio
        string wavPath = MicrophoneManager.Instance.SaveCacheToWav("presentation.wav");
        ApiManager.Instance.CurrentWavPath = wavPath;
    }

    /// <summary>
    /// Chuyển sang đọc câu hỏi tiếp theo.
    /// </summary>
    public void StartNextQuestion(List<string> questions)
    {
        if (questionList != questions)
        {
            questionList = questions;
            // Nếu là danh sách mới, reset về -1 để câu đầu tiên là 0
            if (CurrentQuestionIndex >= questions.Count - 1) CurrentQuestionIndex = -1;
        }

        CurrentQuestionIndex++;

        
        if (CurrentQuestionIndex < questionList.Count)

        {
            ChangeState(GameState.QuestionReading);
            // UI sẽ hiển thị câu hỏi và đếm ngược thông qua Event OnStateChanged
        }
        else
        {
            FinishAll();
        }
    }

    /// <summary>
    /// Người dùng bắt đầu trả lời câu hỏi.
    /// </summary>
    public void StartAnswering()
    {
        ChangeState(GameState.QuestionAnswering);
        MicrophoneManager.Instance.StartRecording();
        
        if (speechAnalyzer != null) 
            speechAnalyzer.StartAnalysis(MicrophoneManager.Instance.CurrentMicName, MicrophoneManager.Instance.CurrentClip);
    }

    /// <summary>
    /// Kết thúc câu trả lời hiện tại.
    /// </summary>
    public void FinishAnswer()
    {
        if (currentState != GameState.QuestionAnswering && currentState != GameState.QuestionReading) return;

        if (currentState == GameState.QuestionAnswering)
        {
            string fileName = $"Question_{CurrentQuestionIndex + 1}.wav";

            MicrophoneManager.Instance.SaveCurrentRecordingToWav(fileName);
        }
        
        if (speechAnalyzer != null) speechAnalyzer.PauseAnalysis();

        // Cho NPC đang hỏi ngồi xuống
        if (NpcManager.Instance != null) NpcManager.Instance.ResetCurrentNPC();

        ChangeState(GameState.Break);
        // Chờ UI gọi StartNextQuestion hoặc FinishAll
    }

    /// <summary>
    /// Kết thúc toàn bộ và nộp bài.
    /// </summary>
    public void FinishAll()
    {
        ChangeState(GameState.Finished);
        
        // Dừng mọi thứ cuối cùng
        if (gazeTracker != null) gazeTracker.StopAndExportTracking();
        if (presentationTimer != null) presentationTimer.CalculatePresentationScore();
        
        // Gọi API Submission thông qua ApiManager
        ProcessSubmission();
    }

    public void PauseGame()
    {
        MicrophoneManager.Instance.StopAndCache();
        if (speechAnalyzer != null) speechAnalyzer.PauseAnalysis();
        if (gazeTracker != null) gazeTracker.PauseTracking();
        if (presentationTimer != null) presentationTimer.PauseTimer();
    }

    public void ResumeGame()
    {
        if (currentState == GameState.Presentation || currentState == GameState.QuestionAnswering)
        {
            MicrophoneManager.Instance.StartRecording();
            if (speechAnalyzer != null) speechAnalyzer.ResumeAnalysis();
        }

        if (currentState == GameState.Presentation)
        {
            if (gazeTracker != null) gazeTracker.ResumeTracking();
            if (presentationTimer != null) presentationTimer.ResumeTimer();
        }
    }

    private void ProcessSubmission()
    {
        // Kích hoạt quy trình nộp bài và hiển thị PDF trên PauseMenuManager
        if (PauseMenuManager.Instance != null)
        {
            PauseMenuManager.Instance.FinalSubmit();
        }
        else
        {
            Debug.LogError("[GameController] PauseMenuManager.Instance is null! Cannot submit.");
        }
    }
}
