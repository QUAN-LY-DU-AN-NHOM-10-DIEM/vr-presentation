using SFB;
using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class AnalyzeResponse
{
    public string title;
    public string description;
    public string context_text;
}

public class GameModeManager : MonoBehaviour
{
    [Header("UI Fields (Để đổ chữ vào)")]
    public TMP_InputField titleInputField;
    public TMP_InputField contextInputField;

    [Header("UI State & Loading")]
    public GameObject loadingPanel; // Kéo thả cái Panel Loading vào đây
    public Button analyzeButton;
    public TMP_Text loadingText;    // (Tùy chọn) Chữ hiện trạng thái "Đang tải file...", "AI đang đọc..."

    [Header("API Config")]
    public string backendUrl = "https://d251-2a09-bac5-55fd-101e-00-19b-fe.ngrok-free.app/api/v1/upload-context";

    [Header("Player Controls")]
    // Dùng MonoBehaviour để bạn có thể kéo BẤT KỲ script di chuyển nào vào đây
    public MonoBehaviour playerMovementScript;

    // Hàm gọi khi bắt đầu nhấp vào ô gõ chữ
    public void LockPlayerMovement()
    {
        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = false;
            Debug.Log("🔒 Đã khóa di chuyển để gõ chữ");
        }
    }

    // Hàm gọi khi gõ xong hoặc click ra ngoài
    public void UnlockPlayerMovement()
    {
        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = true;
            Debug.Log("🔓 Đã mở lại di chuyển");
        }
    }

    public string selectedPdfPath = "";
    private string selectedTxtPath = "";

    void Start()
    {
        // Đảm bảo lúc mới vào thì giấu cái màn hình Loading đi
        if (loadingPanel != null) loadingPanel.SetActive(false);
    }

    public void OpenFilePicker()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        // --- WINDOWS PCVR ---
        var extensions = new[] { new ExtensionFilter("PDF Files", "pdf") };
        string[] paths = StandaloneFileBrowser.OpenFilePanel("Chọn file Slide PDF", "", extensions, false);

        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            selectedPdfPath = paths[0];
            Debug.Log("💻 [PCVR] Đã chọn PDF: " + selectedPdfPath);
        }
#else
        string[] fileTypes = { "application/pdf" };
        NativeFilePicker.PickFile((path) =>
        {
            if (path != null)
            {
                selectedPdfPath = path;
            }
        }, fileTypes);
#endif
    }

    public void OpenScriptPicker()
    {
        string fileType = NativeFilePicker.ConvertExtensionToFileType("txt");
        string[] fileTypes = { fileType };
        NativeFilePicker.PickFile((path) =>
        {
            if (path != null)
            {
                selectedTxtPath = path;
                if (contextInputField != null) contextInputField.text = File.ReadAllText(path);
            }
        }, fileTypes);
    }

    public void StartAnalysis()
    {
        if (string.IsNullOrEmpty(selectedPdfPath))
        {
            Debug.LogError("Chưa chọn file PDF! Bắt buộc phải có file Slide.");
            return;
        }

        // Gọi hàm Async không làm đứng hình VR
        _ = UploadUsingManualWebRequestAsync();
    }

    private async Task UploadUsingManualWebRequestAsync()
    {
        // 1. BẬT LOADING (Chạy trên Main Thread)
        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (analyzeButton != null) analyzeButton.interactable = false;
        if (loadingText != null) loadingText.text = "Đang upload file để phân tích...";

        bool isSuccess = false;
        string jsonResult = "";
        string errorMsg = "";

        // 2. CHẠY MẠNG TRÊN LUỒNG CHẠY NGẦM (Tránh giật lag kính VR)
        await Task.Run(() =>
        {
            try
            {
                string boundary = "----------" + DateTime.Now.Ticks.ToString("x");
                ServicePointManager.ServerCertificateValidationCallback = MyRemoteCertificateValidationCallback;

                HttpWebRequest webrequest = (HttpWebRequest)WebRequest.Create(backendUrl);
                webrequest.ContentType = "multipart/form-data; boundary=" + boundary;
                webrequest.Method = "POST";
                webrequest.Timeout = 3000000; // Đợi 2 phút
                webrequest.Headers.Add("ngrok-skip-browser-warning", "69420");

                // Dùng MemoryStream để nặn file, giúp tự động tính ContentLength (Ngrok rất thích điều này)
                using (MemoryStream ms = new MemoryStream())
                {
                    // Đóng gói PDF
                    WriteFileToMemory(ms, "slide_file", selectedPdfPath, "application/pdf", boundary);

                    // Đóng gói Txt (nếu có)
                    if (!string.IsNullOrEmpty(selectedTxtPath))
                    {
                        WriteFileToMemory(ms, "script_file", selectedTxtPath, "text/plain", boundary);
                    }

                    // Chốt sổ Boundary cuối cùng
                    byte[] endBoundaryBytes = Encoding.ASCII.GetBytes("--" + boundary + "--\r\n");
                    ms.Write(endBoundaryBytes, 0, endBoundaryBytes.Length);

                    // Cực kỳ quan trọng: Báo cáo dung lượng chuẩn xác cho Ngrok
                    webrequest.ContentLength = ms.Length;

                    // Đổ dữ liệu lên mây
                    using (Stream requestStream = webrequest.GetRequestStream())
                    {
                        ms.Position = 0;
                        ms.CopyTo(requestStream);
                    }
                }

                // 3. HỨNG KẾT QUẢ TỪ PYTHON
                using (WebResponse response = webrequest.GetResponse())
                using (Stream s = response.GetResponseStream())
                using (StreamReader sr = new StreamReader(s))
                {
                    jsonResult = sr.ReadToEnd();
                    isSuccess = true;
                }
            }
            catch (WebException e)
            {
                if (e.Response != null)
                {
                    using (StreamReader r = new StreamReader(e.Response.GetResponseStream()))
                    {
                        errorMsg = r.ReadToEnd();
                    }
                }
                else errorMsg = e.Message;
            }
            catch (Exception e)
            {
                errorMsg = e.Message;
            }
        });

        // 4. XỬ LÝ KẾT QUẢ VÀ CẬP NHẬT UI (Quay lại Main Thread)
        if (isSuccess)
        {
            Debug.Log($"✅ THÀNH CÔNG: {jsonResult}");

            AnalyzeResponse responseData = JsonUtility.FromJson<AnalyzeResponse>(jsonResult);
            if (titleInputField != null) titleInputField.text = responseData.title;
            if (contextInputField != null) contextInputField.text = responseData.description;

            if (loadingText != null) loadingText.text = "Phân tích xong! Mời user kiểm tra và vào phòng.";
            await Task.Delay(1500); // Đợi 1.5s cho user đọc rồi tắt panel
        }
        else
        {
            Debug.LogError($"❌ Lỗi: {errorMsg}");
            if (loadingText != null) loadingText.text = "Có lỗi! Hãy kiểm tra Console.";
            await Task.Delay(3000);
        }

        // TẮT LOADING
        if (loadingPanel != null) loadingPanel.SetActive(false);
        if (analyzeButton != null) analyzeButton.interactable = true;
        analyzeButton.GetComponentInChildren<TMP_Text>().text = "Regenerate";
    }

    // --- CÁC HÀM HỖ TRỢ BÊN DƯỚI ---

    // Hàm hỗ trợ tự động băm file thành Byte theo cấu trúc HTTP chuẩn
    private void WriteFileToMemory(MemoryStream ms, string fieldName, string filePath, string contentType, string boundary)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("--").Append(boundary).Append("\r\n");
        sb.Append("Content-Disposition: form-data; name=\"").Append(fieldName);
        sb.Append("\"; filename=\"").Append(Path.GetFileName(filePath)).Append("\"\r\n");
        sb.Append("Content-Type: ").Append(contentType).Append("\r\n\r\n");

        byte[] headerBytes = Encoding.UTF8.GetBytes(sb.ToString());
        ms.Write(headerBytes, 0, headerBytes.Length);

        byte[] fileBytes = File.ReadAllBytes(filePath);
        ms.Write(fileBytes, 0, fileBytes.Length);

        byte[] newlineBytes = Encoding.ASCII.GetBytes("\r\n");
        ms.Write(newlineBytes, 0, newlineBytes.Length);
    }

    public bool MyRemoteCertificateValidationCallback(System.Object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
    {
        bool isOk = true;
        // If there are errors in the certificate chain, look at each error to determine the cause.
        if (sslPolicyErrors != SslPolicyErrors.None)
        {
            for (int i = 0; i < chain.ChainStatus.Length; i++)
            {
                if (chain.ChainStatus[i].Status != X509ChainStatusFlags.RevocationStatusUnknown)
                {
                    chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                    chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 1, 0);
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
                    bool chainIsValid = chain.Build((X509Certificate2)certificate);
                    if (!chainIsValid)
                    {
                        isOk = false;
                    }
                }
            }
        }
        return isOk;
    }
}

public class BypassCertificate : UnityEngine.Networking.CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        return true; // Luôn luôn trả về true (nhắm mắt cho qua)
    }
}