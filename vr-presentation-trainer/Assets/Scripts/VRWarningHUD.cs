using UnityEngine;
using TMPro;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class VRWarningHUD : MonoBehaviour
{
    public TextMeshProUGUI warningText;
    public float fadeSpeed = 3f;
    public float displayDuration = 2f;

    public static VRWarningHUD Instance { get; private set; }

    private CanvasGroup canvasGroup;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f; 
    }

    public void ShowWarning(string message, Color textColor)
    {
        if (warningText == null) 
        {
            Debug.LogError("❌ [VRWarningHUD]: Bạn chưa gán 'Warning Text' trong Inspector của WarningHUD_Canvas!");
            return;
        }
        
        // Đảm bảo Object được bật lên
        gameObject.SetActive(true);
        
        warningText.text = message;
        warningText.color = textColor;

        // Đảm bảo HUD luôn hiện đúng tầm mắt VR
        Camera cam = Camera.main;
        if (cam != null)
        {
            Debug.Log("🎯 [VRWarningHUD]: Đang đưa HUD tới Camera: " + cam.name);
            // Gắn HUD vào làm con của Camera để nó luôn di chuyển theo đầu
            transform.SetParent(cam.transform);
            
            // Đặt vị trí cục bộ: trước mặt 1.5m, hơi thấp xuống một chút (-0.2f) để không che khuất tầm nhìn chính
            transform.localPosition = new Vector3(0f, -0.2f, 1.5f);
            
            // Quay mặt chữ về phía người chơi (0,0,0 vì đã là con của Camera)
            transform.localRotation = Quaternion.identity;
        }

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