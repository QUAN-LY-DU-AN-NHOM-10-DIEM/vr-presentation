using SFB;
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý việc chọn file và upload ngữ cảnh (PDF/TXT) ở màn hình sảnh.
/// Đã được refactor để sử dụng ApiManager.
/// </summary>
public class GameModeManager : MonoBehaviour
{
    [Header("UI Fields")]
    public TMP_InputField titleInputField;
    public TMP_InputField contextInputField;
    public TMP_Text pdfFileNameText;
    public TMP_Text scriptFileNameText;

    [Header("UI State & Loading")]
    public GameObject loadingPanel;
    public TMP_Text loadingText;
    public Button analyzeButton;

    private string selectedPdfPath;
    private string selectedTxtPath;

    public void OpenFilePickerPDF()
    {
        var extensions = new[] { new ExtensionFilter("PDF Files", "pdf") };
        string[] paths = StandaloneFileBrowser.OpenFilePanel("Chọn file PDF thuyết trình", "", extensions, false);
        if (paths.Length > 0)
        {
            selectedPdfPath = paths[0];
            pdfFileNameText.text = Path.GetFileName(selectedPdfPath);
        }
    }

    public void OpenFilePickerTXT()
    {
        var extensions = new[] { new ExtensionFilter("Text Files", "txt") };
        string[] paths = StandaloneFileBrowser.OpenFilePanel("Chọn file Script hỗ trợ", "", extensions, false);
        if (paths.Length > 0)
        {
            selectedTxtPath = paths[0];
            scriptFileNameText.text = Path.GetFileName(selectedTxtPath);
        }
    }

    public void OnClickAnalyze()
    {
        if (string.IsNullOrEmpty(selectedPdfPath))
        {
            if (loadingText != null) loadingText.text = "Bạn chưa chọn file PDF!";
            return;
        }
        StartCoroutine(UploadContextRoutine());
    }

    private IEnumerator UploadContextRoutine()
    {
        loadingPanel.SetActive(true);
        analyzeButton.interactable = false;
        loadingText.text = "Đang tải file lên server...";

        bool isDone = false;
        
        yield return ApiManager.Instance.UploadContext(selectedPdfPath, selectedTxtPath, (success, response, error) => {
            if (success)
            {
                ApiManager.Instance.CurrentSessionId = response.session_id;
                titleInputField.text = response.title;
                contextInputField.text = response.description;
                loadingText.text = "Tải lên thành công!";

                // Tự động load PDF vào các màn hình trong phòng thuyết trình
                if (PauseMenuManager.Instance != null)
                {
                    if (PauseMenuManager.Instance.pdf1 != null) 
                        PauseMenuManager.Instance.pdf1.LoadPDF(selectedPdfPath, true);
                    if (PauseMenuManager.Instance.pdf2 != null) 
                        PauseMenuManager.Instance.pdf2.LoadPDF(selectedPdfPath, true);
                }
            }
            else
            {
                loadingText.text = $"Lỗi: {error}";
            }
            isDone = true;
        });

        yield return new WaitUntil(() => isDone);
        yield return new WaitForSeconds(2f);
        
        loadingPanel.SetActive(false);
        analyzeButton.interactable = true;
    }
}