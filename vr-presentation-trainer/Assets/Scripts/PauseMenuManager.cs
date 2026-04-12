using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenuManager : MonoBehaviour
{
    [Header("Menu Setup")]
    public GameObject pauseCanvas;
    public ModeManager modeManager;
    public InputActionReference menuButtonInput;
    public GameObject micOnImage;
    public GameObject micOffImage;
    public bool isPaused = false;

    [Header("Navigation")]
    public GameObject vrPlayer;
    public Transform lobbySpawnPoint;
    [Header("Hand Menu Offsets")]
    public GameObject playerLeftHand;
    public Vector3 menuPositionOffset = new Vector3(-0.35f, 0.01f, -0.1f); // Lệch lên trên và ra trước một chút
    public Vector3 menuRotationOffset = new Vector3(0f, -180f, -90f);    // Nghiêng 45 độ để dễ nhìn

    // Mic Info
    public string hardwareMicName;
    private bool isRecording = false;
    private AudioClip tempRecordingClip; // File ghi âm tạm thời cho mỗi lần bấm
    private List<float> allAudioChunks = new List<float>(); // Cái "xô" chứa toàn bộ âm thanh
    private int sampleRate = 44100;

    [Header("External References")]
    public GazeTrackingManager gazeTracker;
    public PresentationTimer presentationTimer;

    private void Start()
    {
        if (playerLeftHand != null && pauseCanvas != null) {
            // 1. Biến Canvas thành con của Bàn tay trái
            pauseCanvas.transform.SetParent(playerLeftHand.transform);
            // 2. Chỉnh vị trí lệch (Local Position) so với bàn tay
            pauseCanvas.transform.localPosition = menuPositionOffset;
            // 3. Chỉnh góc xoay (Local Rotation) để menu ngửa lên nhìn thẳng vào mắt
            pauseCanvas.transform.localEulerAngles = menuRotationOffset;
        }
        if (pauseCanvas != null) pauseCanvas.SetActive(false);
    }

    public void ToggleRecording()
    {
        if (string.IsNullOrEmpty(hardwareMicName)) { Debug.Log("No mic yet"); return; }

        if (!isRecording)
        {
            // BẮT ĐẦU THU ÂM
            tempRecordingClip = Microphone.Start(hardwareMicName, false, 600, sampleRate);
            isRecording = true;
            if (micOnImage != null) micOnImage.SetActive(true);
            if (micOffImage != null) micOffImage.SetActive(false);
            Debug.Log("Continue Recording");
        }
        else
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
        if (isRecording) ToggleRecording();
        AudioClip finalClip = CreateFinalStitchedClip();

        if (finalClip != null)
        {
            string timeStamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string fileName = "VR_Presentation_" + timeStamp + ".wav";

            string savedPath = WavUtility.Save(fileName, finalClip);

            Debug.Log("Audio File Saved Successfully at: " + savedPath);
            // [TỐI ƯU RAM] - Hủy file tổng sau khi đã xuất ra file .wav thành công!
            Destroy(finalClip);
        }
        else
        {
            Debug.Log("⚠️ No audio to save!");
        }
    }

    private void OnEnable()
    {
        if (menuButtonInput != null)
            menuButtonInput.action.performed += TogglePauseState;
    }

    private void OnDisable()
    {
        if (menuButtonInput != null)
            menuButtonInput.action.performed -= TogglePauseState;
    }

    // --- 1. MENU CONTROLS ---
    public void TogglePauseState(InputAction.CallbackContext context)
    {
        TogglePauseMenu();
    }
    public void TogglePauseMenu()
    {
        if (!isPaused)
        {
            if (pauseCanvas != null) pauseCanvas.SetActive(true);
            if (isRecording) ToggleRecording();
            if (gazeTracker != null) gazeTracker.PauseTracking();
            if (presentationTimer != null) presentationTimer.PauseTimer();
        }
        else
        {
            if (pauseCanvas != null) pauseCanvas.SetActive(false);
            if (!isRecording) ToggleRecording();
            if (gazeTracker != null) gazeTracker.ResumeTracking();
            if (presentationTimer != null) presentationTimer.ResumeTimer();
        }
        isPaused = !isPaused;
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
}