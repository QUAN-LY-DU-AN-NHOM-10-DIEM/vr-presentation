using Assets.CustomPdfViewer.Scripts;
using UnityEngine;
using UnityEngine.InputSystem;

public class PageChangeListener : MonoBehaviour
{
    public InputActionReference nextSlideInput;
    public InputActionReference previousSlideInput;

    private CustomPdfViewerUI pdfViewer;

    private void Awake()
    {
        // Tự động tìm script PDF Viewer nằm trên cùng một Object
        pdfViewer = GetComponent<CustomPdfViewerUI>();
    }

    private void OnEnable()
    {
        // Khi bảng PDF bật lên, bắt đầu lắng nghe controller
        if (nextSlideInput != null)
            nextSlideInput.action.performed += OnNextPressed;

        if (previousSlideInput != null)
            previousSlideInput.action.performed += OnPreviousPressed;
    }

    private void OnDisable()
    {
        // Khi bảng PDF bị ẩn đi, NGƯNG lắng nghe để tránh lỗi
        if (nextSlideInput != null)
            nextSlideInput.action.performed -= OnNextPressed;

        if (previousSlideInput != null)
            previousSlideInput.action.performed -= OnPreviousPressed;
    }

    private void OnNextPressed(InputAction.CallbackContext context)
    {
        pdfViewer.NextPage();
    }

    private void OnPreviousPressed(InputAction.CallbackContext context)
    {
        pdfViewer.PreviousPage();
    }
}
