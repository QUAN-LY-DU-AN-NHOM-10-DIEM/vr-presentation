using Assets.CustomPdfViewer.Scripts;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class PositionChanger : MonoBehaviour
{
    public GameObject player;
    public GameObject PauseMenu;
    public GameModeManager GameModeManager;
    public ModeManager modeManager;
    public PauseMenuManager pauseManager;
    public CustomPdfViewerUI pdfViewer1;
    public CustomPdfViewerUI pdfViewer2;

    // 2 Biến này chỉ dùng để lấy tọa độ (Position)
    public Transform normalRoom;
    public Transform defenseRoom;


    [Header("Eye Tracking Integration")]
    public GazeTrackingManager gazeTracker;

    // THÊM 2 BIẾN NÀY CHỈ ĐỂ CHỨA NHÓM NPC
    [Tooltip("Kéo GameObject tổng chứa NPC phòng Normal vào đây")]
    public Transform normalNPCGroup;
    [Tooltip("Kéo GameObject tổng chứa NPC phòng Defense vào đây")]
    public Transform defenseNPCGroup;

    [Header("UI & Display Settings")]
    public TextMeshProUGUI liveAdviceTextUInormal;
    public TextMeshProUGUI liveAdviceTextUIdefense;

    [Header("Timer")]
    public PresentationTimer presentationTimer;
    public TimePicker timePicker;

    public void Start()
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
            pauseManager.hardwareMicName = Microphone.devices[0];
            Debug.Log("Mic found");
        }
        else
        {
            Debug.Log("No mic found");
        }
        PauseMenu.SetActive(false);
    }

    public void GoToMainRoom()
    {
        Debug.Log("Đang dịch chuyển VRPlayer vào phòng chính...");

        if (player != null)
        {
            if (modeManager.selectedMode == "Normal")
            {
                pdfViewer2.gameObject.SetActive(true);
                pdfViewer2.LoadPDF(GameModeManager.selectedPdfPath, true);
                pdfViewer2.GoToPage(0);

                // Dịch chuyển tức thời đến tọa độ phòng Normal
                player.transform.position = normalRoom.position;

                // Gán đúng cái Group chứa NPC vào để GazeTracker quét
                if (gazeTracker != null)
                {
                    gazeTracker.activeRoomParent = normalNPCGroup;
                    gazeTracker.liveAdviceTextUI = liveAdviceTextUInormal; // Gán đúng UI cho từng phòng
                    gazeTracker.StartTracking();
                }
            }
            else
            {
                pdfViewer1.gameObject.SetActive(true);
                pdfViewer1.LoadPDF(GameModeManager.selectedPdfPath, true);
                pdfViewer1.GoToPage(0);

                // Dịch chuyển tức thời đến tọa độ phòng Defense
                player.transform.position = defenseRoom.position;

                // Gán đúng cái Group chứa NPC vào để GazeTracker quét
                if (gazeTracker != null)
                {
                    gazeTracker.activeRoomParent = defenseNPCGroup;
                    gazeTracker.liveAdviceTextUI = liveAdviceTextUIdefense; // Gán đúng UI cho từng phòng
                    gazeTracker.StartTracking();
                }
            }

            if (presentationTimer != null)
            {
                float timeInSecond = (timePicker != null) ? timePicker.GetTimeInSeconds() : 0f;
                presentationTimer.StartPresentationTimer(timeInSecond);
            }
            PauseMenu.SetActive(true);
            pauseManager.TurnOnMic();
            pauseManager.TurnOnVoiceAnalyzer();

            player.transform.rotation = Quaternion.Euler(0f, -90f, 0f); // Quay mặt về phía bảng trình chiếu
            Debug.Log("Dịch chuyển thành công và đã tự động bật Eye Tracking!");
        }
        else
        {
            Debug.LogError("❌ Không tìm thấy object nào tên là VRPlayer cả! Bạn check lại tên bên Hierarchy nha.");
        }
    }
}