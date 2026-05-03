using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Model phản hồi khi upload context (PDF/TXT)
/// </summary>
[Serializable]
public class AnalyzeResponse
{
    public string session_id;
    public string title;
    public string description;
    public string context_text;
}

/// <summary>
/// Model phản hồi khi yêu cầu tạo câu hỏi từ AI
/// </summary>
[Serializable]
public class GenerateQuestionResponse
{
    public List<string> questions;
}

/// <summary>
/// Model yêu cầu chấm điểm tổng quát
/// </summary>
[Serializable]
public class EvaluateRequest
{
    public string session_id;
    public int time_management_score;
    public int eye_contact_score;
    public int volume_score;
    public List<float> eye_contact_zones;
    public List<string> eye_contact_zone_names;
    public string eye_contact_advice;
    public int presentation_duration;
    public int target_time;
    public int qa_duration;
    public float quiet_ratio;
    public float loud_ratio;
    public float avg_volume;
}

/// <summary>
/// Manager tập trung xử lý toàn bộ các kết nối API với Backend.
/// Sử dụng Singleton để truy cập từ mọi nơi.
/// </summary>
public class ApiManager : MonoBehaviour
{
    public static ApiManager Instance { get; private set; }

    [Header("Session Data")]
    public string CurrentSessionId = "";
    public string CurrentWavPath = "";

    [Header("Cấu hình Backend")]
    [Tooltip("URL gốc của server (VD: https://abcd.ngrok-free.app/api/v1)")]
    public string backendBaseUrl = "https://your-ngrok-url.ngrok-free.app/api/v1";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Upload file PDF và Script TXT để khởi tạo Session mới.
    /// </summary>
    public IEnumerator UploadContext(string pdfPath, string txtPath, Action<bool, AnalyzeResponse, string> callback)
    {
        WWWForm form = new WWWForm();
        
        if (File.Exists(pdfPath))
            form.AddBinaryData("slide_file", File.ReadAllBytes(pdfPath), Path.GetFileName(pdfPath), "application/pdf");
        
        if (!string.IsNullOrEmpty(txtPath) && File.Exists(txtPath))
            form.AddBinaryData("script_file", File.ReadAllBytes(txtPath), Path.GetFileName(txtPath), "text/plain");

        using (UnityWebRequest request = UnityWebRequest.Post($"{backendBaseUrl}/upload-context", form))
        {
            SetupCommonHeaders(request);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                AnalyzeResponse response = JsonUtility.FromJson<AnalyzeResponse>(request.downloadHandler.text);
                callback?.Invoke(true, response, null);
            }
            else
            {
                callback?.Invoke(false, null, request.error);
            }
        }
    }

    /// <summary>
    /// Gửi file ghi âm thuyết trình để AI phân tích và tạo câu hỏi Q&A.
    /// </summary>
    public IEnumerator GenerateQuestions(string wavPath, string sessionId, string mode, Action<bool, GenerateQuestionResponse, string> callback)
    {
        if (!File.Exists(wavPath))
        {
            callback?.Invoke(false, null, "File âm thanh không tồn tại.");
            yield break;
        }

        WWWForm form = new WWWForm();
        form.AddBinaryData("audio_file", File.ReadAllBytes(wavPath), Path.GetFileName(wavPath), "audio/wav");

        string url = $"{backendBaseUrl}/generate-question?session_id={UnityWebRequest.EscapeURL(sessionId)}&mode={UnityWebRequest.EscapeURL(mode)}";

        using (UnityWebRequest request = UnityWebRequest.Post(url, form))
        {
            SetupCommonHeaders(request);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                GenerateQuestionResponse response = JsonUtility.FromJson<GenerateQuestionResponse>(request.downloadHandler.text);
                callback?.Invoke(true, response, null);
            }
            else
            {
                callback?.Invoke(false, null, request.error);
            }
        }
    }

    /// <summary>
    /// Gửi đống audio câu trả lời và yêu cầu chấm điểm cuối cùng để nhận file PDF.
    /// </summary>
    public IEnumerator EvaluateSession(EvaluateRequest data, Action<bool, byte[], string> callback)
    {
        string jsonPayload = JsonUtility.ToJson(data);
        
        using (UnityWebRequest request = new UnityWebRequest($"{backendBaseUrl}/evaluate", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            SetupCommonHeaders(request);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                callback?.Invoke(true, request.downloadHandler.data, null);
            }
            else
            {
                callback?.Invoke(false, null, request.error);
            }
        }
    }

    /// <summary>
    /// Gửi đồng loạt nhiều file âm thanh câu trả lời (Batch Upload).
    /// </summary>
    public IEnumerator SubmitBatchAudio(string sessionId, List<string> filePaths, Action<bool, string> callback)
    {
        WWWForm form = new WWWForm();
        foreach (string path in filePaths)
        {
            if (File.Exists(path))
            {
                form.AddBinaryData("audio_files", File.ReadAllBytes(path), Path.GetFileName(path), "audio/wav");
            }
        }

        if (string.IsNullOrEmpty(sessionId))
        {
            callback?.Invoke(false, "Lỗi: Session ID bị trống. Bạn cần upload context trước!");
            yield break;
        }

        string url = $"{backendBaseUrl}/submit?session_id={UnityWebRequest.EscapeURL(sessionId)}";

        using (UnityWebRequest request = UnityWebRequest.Post(url, form))
        {
            SetupCommonHeaders(request);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                callback?.Invoke(true, null);
            }
            else
            {
                callback?.Invoke(false, request.error);
            }
        }
    }

    private void SetupCommonHeaders(UnityWebRequest request)
    {
        request.SetRequestHeader("ngrok-skip-browser-warning", "69420");
        request.timeout = 300; // 5 phút timeout cho các file lớn/xử lý AI lâu
        request.certificateHandler = new BypassCertificate();
    }
}

public class BypassCertificate : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData) => true;
}
