using TMPro;
using UnityEngine;

public class MiniModeShow : MonoBehaviour
{
    public TextMeshProUGUI normalText;
    public TextMeshProUGUI defenseText;

    // Khai báo ModeManager để lấy biến
    public ModeManager modeManager;

    // Update chạy liên tục để tự check biến
    public void UpdateUI()
    {
        if (modeManager == null) return;

        if (modeManager.selectedMode == "Normal")
        {
            normalText.enabled = true;
            defenseText.enabled = false;
        }
        else if (modeManager.selectedMode == "Defense")
        {
            defenseText.enabled = true;
            normalText.enabled = false;
        }
    }
}