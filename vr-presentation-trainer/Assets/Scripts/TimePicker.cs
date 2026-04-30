using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TimePicker : MonoBehaviour
{
    public TMP_Dropdown minutePicker;
    public TMP_Dropdown secondPicker;

    [Header("Start Button")]
    public Button startButton;

    private void Start()
    {
        if (startButton != null)
            startButton.interactable = false;
        // Lắng nghe sự kiện đổi dropdown
        if (minutePicker != null)
            minutePicker.onValueChanged.AddListener(_ => UpdateStartButtonState());
        if (secondPicker != null)
            secondPicker.onValueChanged.AddListener(_ => UpdateStartButtonState());

        // Check trạng thái ban đầu
        UpdateStartButtonState();
    }


    public float GetTimeInSeconds()
    {
        int minutes = 0;
        int seconds = 0;

        if (minutePicker != null)
            int.TryParse(minutePicker.options[minutePicker.value].text, out minutes);

        if (secondPicker != null)
            int.TryParse(secondPicker.options[secondPicker.value].text, out seconds);

        Debug.Log($"[TimePicker] Selected Time: {minutes} minutes and {seconds} seconds");

        return minutes * 60f + seconds;
    }

    private void UpdateStartButtonState()
    {
        if (startButton != null)
            startButton.interactable = GetTimeInSeconds() > 0f;
    }
}