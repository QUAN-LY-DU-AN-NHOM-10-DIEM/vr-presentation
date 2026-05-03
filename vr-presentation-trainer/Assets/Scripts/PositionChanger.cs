using Assets.CustomPdfViewer.Scripts;
using TMPro;
using UnityEngine;

/// <summary>
/// Quản lý việc dịch chuyển người chơi giữa các phòng (Lobby, Normal, Defense).
/// Khởi tạo môi trường tương ứng cho từng phòng thuyết trình.
/// </summary>
public class PositionChanger : MonoBehaviour
{
    [Header("Người chơi & UI")]
    public GameObject player;
    public GameObject pauseMenu;
    public GameModeManager gameModeManager;
    public ModeManager modeManager;
    public PauseMenuManager pauseManager;

    [Header("Phòng Thuyết trình")]
    public Transform normalRoom;
    public Transform defenseRoom;
    public Transform normalNPCGroup;
    public Transform defenseNPCGroup;

    [Header("PDF Viewers")]
    public CustomPdfViewerUI pdfViewer1;
    public CustomPdfViewerUI pdfViewer2;

    // Không còn dùng bảng phụ, chuyển sang dùng VRWarningHUD.Instance.ShowWarning()

    public void Start()
    {
        // Ẩn Menu khi mới vào
        if (pauseMenu != null) pauseMenu.SetActive(false);
        
        // Xin quyền Micro trên Android
#if UNITY_ANDROID
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
        }
#endif
    }

    /// <summary>
    /// Dịch chuyển người chơi vào phòng thuyết trình đã chọn và bắt đầu phiên làm việc.
    /// </summary>
    public void GoToMainRoom()
    {
        if (player == null) return;

        bool isNormal = (modeManager.selectedMode == "Normal");
        Transform targetRoom = isNormal ? normalRoom : defenseRoom;
        CustomPdfViewerUI activePdf = isNormal ? pdfViewer2 : pdfViewer1;
        Transform activeNPCs = isNormal ? normalNPCGroup : defenseNPCGroup;

        // 1. Dịch chuyển và xoay người chơi
        player.transform.position = targetRoom.position;
        player.transform.rotation = Quaternion.Euler(0f, -90f, 0f);

        // 2. Setup PDF
        if (activePdf != null)
        {
            activePdf.gameObject.SetActive(true);
            // activePdf.LoadPDF(gameModeManager.selectedPdfPath, true); // Uncomment khi có biến path chuẩn
        }

        // 3. Setup Gaze Tracker
        var gazeTracker = GameController.Instance.gazeTracker;
        if (gazeTracker != null)
        {
            gazeTracker.activeRoomParent = activeNPCs;
            gazeTracker.currentRoomType = isNormal ? GazeTrackingManager.RoomType.NormalClass : GazeTrackingManager.RoomType.DefenseRoom;
            
            // QUAN TRỌNG: Phải gọi hàm này để script quét danh sách NPC và bắt đầu tính thời gian!
            gazeTracker.StartTracking(activeNPCs, isNormal ? GazeTrackingManager.RoomType.NormalClass : GazeTrackingManager.RoomType.DefenseRoom);
        }

        // 4. Bật Menu và kích hoạt GameController
        if (pauseMenu != null) pauseMenu.SetActive(true);
        GameController.Instance.StartPresentation();

        Debug.Log($"✅ Đã vào phòng {(isNormal ? "Normal" : "Defense")} và bắt đầu thuyết trình.");
    }
}