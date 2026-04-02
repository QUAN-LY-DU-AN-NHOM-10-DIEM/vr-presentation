using UnityEngine;
using UnityEngine.UI;

public class TutorialGuide : MonoBehaviour
{
    public Sprite[] images;
    public Image displayImage;
    public GameObject nextButton;
    private int currentStepIndex = 0;

    void Start()
    {
        displayImage.sprite = images[currentStepIndex];
    }

    public void OnButtonClick()
    {
        currentStepIndex++;
        if (currentStepIndex < images.Length)
        {
            displayImage.sprite = images[currentStepIndex];

            if (currentStepIndex == images.Length - 1)
            {
                nextButton.SetActive(false);
            }
        }
    }
}
