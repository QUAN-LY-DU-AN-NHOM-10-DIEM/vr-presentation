using UnityEngine;
using TMPro;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class VRWarningHUD : MonoBehaviour
{
    public TextMeshProUGUI warningText;
    public float fadeSpeed = 3f;
    public float displayDuration = 2f;

    private CanvasGroup canvasGroup;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f; // Ẩn hoàn toàn chữ khi mới vào game
    }

    public void ShowWarning(string message, Color textColor)
    {
        warningText.text = message;
        warningText.color = textColor;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeInOutRoutine());
    }

    public void HideWarningEarly()
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeOut());
    }

    private IEnumerator FadeInOutRoutine()
    {
        while (canvasGroup.alpha < 1f)
        {
            canvasGroup.alpha += Time.deltaTime * fadeSpeed;
            yield return null;
        }
        yield return new WaitForSeconds(displayDuration);
        yield return StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        while (canvasGroup.alpha > 0f)
        {
            canvasGroup.alpha -= Time.deltaTime * fadeSpeed;
            yield return null;
        }
    }
}