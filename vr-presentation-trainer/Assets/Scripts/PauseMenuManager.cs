using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenuManager : MonoBehaviour
{
    [Header("Menu Setup")]
    public GameObject pauseCanvas;
    public ModeManager modeManager;
    public Transform vrCamera;
    public float spawnDistance = 1.5f;
    public InputActionReference menuButtonInput;
    public GameObject micOnImage;
    public GameObject micOffImage;
    [HideInInspector] public AudioSource activeMicSource;

    [Header("Navigation")]
    public GameObject vrPlayer;
    public Transform lobbySpawnPoint;

    public AudioClip recordedClip;
    private string hardwareMicName;
    private bool isRecording = false;

    [Header("External References")]
    public GazeTrackingManager gazeTracker;
    public PresentationTimer presentationTimer;

    private void Start()
    {
        // 1. Xin quyền và tìm Micro của Kính VR / PC
#if UNITY_ANDROID
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
        }
#endif

        if (Microphone.devices.Length > 0)
        {
            hardwareMicName = Microphone.devices[0];
        }
        if (micOnImage != null) micOnImage.SetActive(false);
        if (micOffImage != null) micOffImage.SetActive(true);
    }

    public void ToggleRecording()
    {
        if (string.IsNullOrEmpty(hardwareMicName)) return;

        if (!isRecording)
        {
            // BẮT ĐẦU THU ÂM
            if (activeMicSource != null) activeMicSource.Stop();

            // [TỐI ƯU RAM] - Bắt buộc HỦY file thu âm cũ trước khi tạo file mới
            if (recordedClip != null)
            {
                Destroy(recordedClip);
                recordedClip = null;
            }

            recordedClip = Microphone.Start(hardwareMicName, false, 300, 44100);
            isRecording = true;
            if (micOnImage != null) micOnImage.SetActive(true);
            if (micOffImage != null) micOffImage.SetActive(false);
        }
        else
        {
            // DỪNG THU ÂM
            Microphone.End(hardwareMicName);
            isRecording = false;
            PlayRecording();
            if (micOnImage != null) micOnImage.SetActive(false);
            if (micOffImage != null) micOffImage.SetActive(true);
        }
    }

    public void PlayRecording()
    {
        if (isRecording) return;

        if (recordedClip != null && activeMicSource != null)
        {
            activeMicSource.clip = recordedClip;
            activeMicSource.Play();
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
    private void TogglePauseMenu(InputAction.CallbackContext context)
    {
        bool isMenuActive = !pauseCanvas.activeSelf;
        pauseCanvas.SetActive(isMenuActive);

        if (isMenuActive)
        {
            Vector3 forward = new Vector3(vrCamera.forward.x, 0, vrCamera.forward.z).normalized;
            pauseCanvas.transform.position = vrCamera.position + (forward * spawnDistance);
            pauseCanvas.transform.LookAt(new Vector3(vrCamera.position.x, pauseCanvas.transform.position.y, vrCamera.position.z));
            pauseCanvas.transform.Rotate(0, 180, 0);

            if (gazeTracker != null) gazeTracker.PauseTracking();
            if (presentationTimer != null) presentationTimer.PauseTimer();
        }
        else
        {
            if (gazeTracker != null) gazeTracker.ResumeTracking();
            if (presentationTimer != null) presentationTimer.ResumeTimer();
        }
    }

    public void ExitToLobby()
    {
        // 1. Tắt menu Pause
        pauseCanvas.SetActive(false);

        // 2. Dọn dẹp hệ thống Micro & Xóa file ghi âm khỏi RAM
        if (activeMicSource != null) activeMicSource.Stop();
        Microphone.End(hardwareMicName);
        isRecording = false;

        if (recordedClip != null)
        {
            Destroy(recordedClip);
            recordedClip = null;
        }

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