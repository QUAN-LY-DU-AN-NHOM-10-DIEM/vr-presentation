using Assets.CustomPdfViewer.Scripts;
using UnityEngine;

public class PositionChanger : MonoBehaviour
{
    public GameObject player;
    public GameModeManager GameModeManager;
    public ModeManager modeManager;
    public PauseMenuManager pauseManager;
    public CustomPdfViewerUI pdfViewer1;
    public AudioSource defenseMicSource;
    public CustomPdfViewerUI pdfViewer2;
    public AudioSource normalMicSource;

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

    public void GoToMainRoom()
    {
        Debug.Log("Đang dịch chuyển VRPlayer vào phòng chính...");

        if (player != null)
        {
            if (modeManager.selectedMode == "Normal")
            {
                pdfViewer2.gameObject.SetActive(true);
                pdfViewer2.LoadPDF(GameModeManager.selectedPdfPath, true);
                pauseManager.activeMicSource = normalMicSource;

                // Dịch chuyển tức thời đến tọa độ phòng Normal
                player.transform.position = normalRoom.position;

                // Gán đúng cái Group chứa NPC vào để GazeTracker quét
                if (gazeTracker != null)
                {
                    gazeTracker.activeClassroomParent = normalNPCGroup;
                    gazeTracker.StartTracking();
                }
            }
            else
            {
                pdfViewer1.gameObject.SetActive(true);
                pdfViewer1.LoadPDF(GameModeManager.selectedPdfPath, true);
                pauseManager.activeMicSource = defenseMicSource;

                // Dịch chuyển tức thời đến tọa độ phòng Defense
                player.transform.position = defenseRoom.position;

                // Gán đúng cái Group chứa NPC vào để GazeTracker quét
                if (gazeTracker != null)
                {
                    gazeTracker.activeClassroomParent = defenseNPCGroup;
                    gazeTracker.StartTracking();
                }
            }

            player.transform.rotation = Quaternion.Euler(0f, -90f, 0f); // Quay mặt về phía bảng trình chiếu
            Debug.Log("Dịch chuyển thành công và đã tự động bật Eye Tracking!");
        }
        else
        {
            Debug.LogError("❌ Không tìm thấy object nào tên là VRPlayer cả! Bạn check lại tên bên Hierarchy nha.");
        }
    }
}