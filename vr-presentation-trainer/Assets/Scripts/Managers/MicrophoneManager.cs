using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Quản lý phần cứng Microphone, ghi âm và lưu trữ file âm thanh (.wav).
/// Tách biệt logic xử lý âm thanh thô khỏi UI.
/// </summary>
public class MicrophoneManager : MonoBehaviour
{
    public static MicrophoneManager Instance { get; private set; }

    [Header("Cấu hình Mic")]
    public int sampleRate = 44100;
    public int maxRecordingTime = 600; // 10 phút mỗi lần thu

    private string hardwareMicName;
    private bool isRecording = false;
    private AudioClip tempRecordingClip;
    private List<float> allAudioChunks = new List<float>();

    public bool IsRecording => isRecording;
    public string CurrentMicName => hardwareMicName;
    public AudioClip CurrentClip => tempRecordingClip;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeMic();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeMic()
    {
        if (Microphone.devices.Length > 0)
        {
            hardwareMicName = Microphone.devices[0];
            Debug.Log($"🎙️ Đã tìm thấy Mic: {hardwareMicName}");
        }
        else
        {
            Debug.LogWarning("⚠️ Không tìm thấy thiết bị Microphone!");
        }
    }

    /// <summary>
    /// Bắt đầu thu âm vào một clip tạm.
    /// </summary>
    public void StartRecording()
    {
        if (string.IsNullOrEmpty(hardwareMicName)) return;
        if (isRecording) return;

        tempRecordingClip = Microphone.Start(hardwareMicName, false, maxRecordingTime, sampleRate);
        isRecording = true;
        Debug.Log("🎙️ Bắt đầu ghi âm...");
    }

    /// <summary>
    /// Tạm dừng thu âm và lưu dữ liệu vào bộ nhớ đệm (RAM).
    /// </summary>
    public void StopAndCache()
    {
        if (!isRecording) return;

        int lastPos = Microphone.GetPosition(hardwareMicName);
        Microphone.End(hardwareMicName);
        isRecording = false;

        if (lastPos > 0 && tempRecordingClip != null)
        {
            float[] chunkData = new float[lastPos * tempRecordingClip.channels];
            tempRecordingClip.GetData(chunkData, 0);
            allAudioChunks.AddRange(chunkData);
        }

        if (tempRecordingClip != null)
        {
            // Destroy(tempRecordingClip); // Không destroy nếu SpeechAnalyzer vẫn đang dùng
            tempRecordingClip = null;
        }
        Debug.Log("⏸ Đã dừng và lưu vào cache.");
    }

    /// <summary>
    /// Xóa toàn bộ dữ liệu âm thanh trong bộ nhớ đệm.
    /// </summary>
    public void ClearCache()
    {
        allAudioChunks.Clear();
        if (isRecording)
        {
            Microphone.End(hardwareMicName);
            isRecording = false;
        }
        Debug.Log("🗑️ Đã xóa bộ nhớ đệm âm thanh.");
    }

    /// <summary>
    /// Xuất toàn bộ dữ liệu trong cache ra file WAV.
    /// </summary>
    public string SaveCacheToWav(string fileName)
    {
        if (allAudioChunks.Count == 0) return null;

        AudioClip finalClip = AudioClip.Create("StitchedAudio", allAudioChunks.Count, 1, sampleRate, false);
        finalClip.SetData(allAudioChunks.ToArray(), 0);

        string savedPath = WavUtility.Save(fileName, finalClip);
        
        Destroy(finalClip);
        allAudioChunks.Clear(); // Lưu xong thì xóa luôn cho nhẹ RAM
        
        return savedPath;
    }

    /// <summary>
    /// Lưu ngay lập tức Clip hiện tại (dùng cho Q&A).
    /// </summary>
    public string SaveCurrentRecordingToWav(string fileName)
    {
        if (!isRecording || tempRecordingClip == null) return null;

        int lastPos = Microphone.GetPosition(hardwareMicName);
        Microphone.End(hardwareMicName);
        isRecording = false;

        if (lastPos <= 0) return null;

        float[] chunkData = new float[lastPos * tempRecordingClip.channels];
        tempRecordingClip.GetData(chunkData, 0);

        AudioClip finalClip = AudioClip.Create(fileName, chunkData.Length, 1, sampleRate, false);
        finalClip.SetData(chunkData, 0);

        string savedPath = WavUtility.Save(fileName, finalClip);
        
        Destroy(finalClip);
        return savedPath;
    }
}
