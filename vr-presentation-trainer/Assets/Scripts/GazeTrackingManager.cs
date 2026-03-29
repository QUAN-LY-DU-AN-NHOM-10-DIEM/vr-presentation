using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

[System.Serializable]
public class NPCFocusData
{
    public string npcName;
    public float timeLooked;
    public int ignoreCountOver3Mins; // Đếm số lần bị bơ quá 3 phút
}

[System.Serializable]
public class PresentationEvaluationReport
{
    public string sessionDate;
    public float totalSessionTime;
    public float slideFocusTime;
    public float awayFocusTime;
    public float totalAudienceFocusTime;

    public int totalNPCsInRoom;
    public int npcsLookedAt;
    public float audienceCoveragePercent;

    public List<NPCFocusData> npcDetails = new List<NPCFocusData>();
}

class NPCTrackingState
{
    public float totalLookTime = 0f;
    public float continuousIgnoredTime = 0f;
    public int ignoreCount = 0;
}

public class GazeTrackingManager : MonoBehaviour
{
    [Header("Tracking Settings")]
    public LayerMask trackingLayerMask;
    public float checkInterval = 0.1f;
    public float maxDistance = 100f;
    public float minimumLookTimeThreshold = 1.0f;

    [Header("Room Setup")]
    // ĐÂY CHÍNH LÀ CÁI BIẾN MÀ UNITY ĐANG TÌM KIẾM NÈ:
    public Transform activeClassroomParent;
    public float maxIgnoreTimeLimit = 180f; // 3 phút = 180 giây

    private bool isTracking = false;
    private float timer = 0f;
    private string lastTargetName = "";

    private float totalSlideTime = 0f;
    private float totalAwayTime = 0f;

    private Dictionary<string, NPCTrackingState> npcTrackingStates = new Dictionary<string, NPCTrackingState>();

    void Update()
    {
        if (!isTracking) return;

        timer += Time.deltaTime;
        if (timer >= checkInterval)
        {
            PerformRaycast();
            timer = 0f;
        }
    }

    void PerformRaycast()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;

        string currentTargetName = "Không gian trống";
        string currentTag = "Looking_Away";

        if (Physics.Raycast(ray, out hit, maxDistance, trackingLayerMask))
        {
            currentTag = hit.collider.tag;
            currentTargetName = hit.collider.gameObject.name;

            if (currentTag == "Slide_Screen") totalSlideTime += checkInterval;
            else if (currentTag != "NPC_Face") totalAwayTime += checkInterval;
        }
        else
        {
            totalAwayTime += checkInterval;
        }

        foreach (var kvp in npcTrackingStates)
        {
            if (kvp.Key == currentTargetName && currentTag == "NPC_Face")
            {
                kvp.Value.totalLookTime += checkInterval;
                kvp.Value.continuousIgnoredTime = 0f;
            }
            else
            {
                kvp.Value.continuousIgnoredTime += checkInterval;
                if (kvp.Value.continuousIgnoredTime >= maxIgnoreTimeLimit)
                {
                    kvp.Value.ignoreCount++;
                    kvp.Value.continuousIgnoredTime = 0f;
                    Debug.Log($"[Cảnh báo] Khán giả {kvp.Key} đã bị bỏ lơ quá lâu! Số lần lỗi: {kvp.Value.ignoreCount}");
                }
            }
        }

        if (currentTargetName != lastTargetName)
        {
            Debug.Log($"[Eye-Tracking] Đã chuyển hướng nhìn sang: {currentTargetName}");
            lastTargetName = currentTargetName;
        }
    }

    public void StartTracking()
    {
        if (activeClassroomParent == null)
        {
            Debug.LogError("Chưa gán activeClassroomParent! Hệ thống Eye Tracking không biết phải quét phòng nào.");
            return;
        }

        totalSlideTime = 0f;
        totalAwayTime = 0f;
        npcTrackingStates.Clear();
        lastTargetName = "";

        Transform[] allChildren = activeClassroomParent.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in allChildren)
        {
            if (child.CompareTag("NPC_Face"))
            {
                if (!npcTrackingStates.ContainsKey(child.name))
                {
                    npcTrackingStates.Add(child.name, new NPCTrackingState());
                }
            }
        }

        isTracking = true;
        Debug.Log($"[Eye-Tracking] BẮT ĐẦU. Đã nhận diện được {npcTrackingStates.Count} khán giả trong phòng.");
    }

    public void PauseTracking() => isTracking = false;
    public void ResumeTracking() => isTracking = true;

    public void StopAndExportTracking()
    {
        isTracking = false;
        SaveReportToJSON();
    }

    private void SaveReportToJSON()
    {
        PresentationEvaluationReport report = new PresentationEvaluationReport();
        report.sessionDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        report.slideFocusTime = totalSlideTime;
        report.awayFocusTime = totalAwayTime;
        report.totalNPCsInRoom = npcTrackingStates.Count;
        report.npcsLookedAt = 0;
        report.totalAudienceFocusTime = 0f;

        foreach (var kvp in npcTrackingStates)
        {
            report.totalAudienceFocusTime += kvp.Value.totalLookTime;

            if (kvp.Value.totalLookTime >= minimumLookTimeThreshold)
            {
                report.npcsLookedAt++;
            }

            report.npcDetails.Add(new NPCFocusData
            {
                npcName = kvp.Key,
                timeLooked = kvp.Value.totalLookTime,
                ignoreCountOver3Mins = kvp.Value.ignoreCount
            });
        }

        report.totalSessionTime = report.totalAudienceFocusTime + report.slideFocusTime + report.awayFocusTime;
        if (report.totalNPCsInRoom > 0)
        {
            report.audienceCoveragePercent = ((float)report.npcsLookedAt / report.totalNPCsInRoom) * 100f;
        }

        string fileName = "EyeContact_LatestEvaluation.json";
        string filePath = Path.Combine(Application.persistentDataPath, fileName);

        string jsonOutput = JsonUtility.ToJson(report, true);
        File.WriteAllText(filePath, jsonOutput);

        Debug.Log($"[Eye-Tracking] KẾT THÚC. Đã lưu báo cáo tại: {filePath}");
    }
}