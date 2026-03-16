using Assets.CustomPdfViewer.Scripts;
using UnityEngine;
public class PositionChanger : MonoBehaviour
{
    public GameModeManager GameModeManager;
    public ModeManager modeManager;
    public CustomPdfViewerUI pdfViewer1;
    public CustomPdfViewerUI pdfViewer2;

    public Transform normalRoom;
    public Transform defenseRoom;

    public void GoToMainRoom()
    {
        Debug.Log("Đang dịch chuyển VRPlayer vào phòng chính...");

        // Tìm object có tên chính xác là "VRPlayer"
        GameObject player = GameObject.Find("VR Player");

        if (player != null)
        {
            if (modeManager.selectedMode == "Normal")
            {
                pdfViewer2.gameObject.SetActive(true);
                pdfViewer2.LoadPDF(GameModeManager.selectedPdfPath, true);
                // Dịch chuyển tức thời đến tọa độ x=-3.5, y=-1, z=2
                player.transform.position = normalRoom.position;
            }
            else
            {
                pdfViewer1.gameObject.SetActive(true);
                pdfViewer1.LoadPDF(GameModeManager.selectedPdfPath,true);
                // Dịch chuyển tức thời đến tọa độ x=-3.5, y=-1, z=2
                player.transform.position = defenseRoom.position;
            }

            player.transform.rotation = Quaternion.Euler(0f, -90f, 0f); // Quay mặt về phía bảng trình chiếu
            Debug.Log("Dịch chuyển thành công!");
        }
        else
        {
            // Báo lỗi đỏ chót dưới Console nếu gõ sai tên
            Debug.LogError("❌ Không tìm thấy object nào tên là VRPlayer cả! Bạn check lại tên bên Hierarchy nha.");
        }
    }
}
