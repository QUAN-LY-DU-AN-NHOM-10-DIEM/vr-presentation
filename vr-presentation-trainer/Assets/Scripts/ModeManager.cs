using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ModeManager : MonoBehaviour
{
    public Outline normalOutline;
    public Outline defenseOutline;
    public TextMeshProUGUI normalText;
    public TextMeshProUGUI defenseText;

    public string selectedMode = "Normal";

    void Start()
    {
        selectedMode = "Normal";

        normalOutline.enabled = true;
        defenseOutline.enabled = false;
        
        normalText.enabled = true;
        defenseText.enabled = false;
    }

    public void SelectNormal()
    {
        selectedMode = "Normal";

        normalOutline.enabled = true;
        defenseOutline.enabled = false;

        normalText.enabled = true;
        defenseText.enabled = false;
    }

    public void SelectDefense()
    {
        selectedMode = "Defense";

        defenseOutline.enabled = true;
        normalOutline.enabled = false;

        defenseText.enabled = true;
        normalText.enabled = false;
    }
}