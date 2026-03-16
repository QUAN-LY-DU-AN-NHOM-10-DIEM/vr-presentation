using UnityEngine;
using UnityEngine.UI;

public class ModeManager : MonoBehaviour
{
    public Outline normalOutline;
    public Outline defenseOutline;

    public string selectedMode = "Normal";

    void Start()
    {
        selectedMode = "Normal";

        normalOutline.enabled = true;
        defenseOutline.enabled = false;
    }

    public void SelectNormal()
    {
        selectedMode = "Normal";

        normalOutline.enabled = true;
        defenseOutline.enabled = false;
    }

    public void SelectDefense()
    {
        selectedMode = "Defense";

        defenseOutline.enabled = true;
        normalOutline.enabled = false;
    }
}