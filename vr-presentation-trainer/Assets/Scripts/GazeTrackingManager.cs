using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using TMPro;

[System.Serializable]
public class TargetFocusData
{
    public string targetName;
    public string displayName;
    public float timeLooked;
    public float viewPercentage;
    public int neglectCount;
}

[System.Serializable]
public class PresentationEvaluationReport
{
    public string sessionDate;
    public float totalSessionTime;

    [Header("Đánh giá Tương tác mắt")]
    public float totalAudienceFocusTime;
    public float interactionPercentage;
    public string interactionGrade;

    public List<TargetFocusData> targetDetails = new List<TargetFocusData>();
}

class TargetTrackingState
{
    public float totalLookTime = 0f;
    public float continuousIgnoredTime = 0f;
    public int ignoreCount = 0;
}

[System.Serializable]
public class TargetNameMapping
{
    public string objectName;
    public string displayName;
}

public class GazeTrackingManager : MonoBehaviour
{
    [Header("Vision Settings (Tầm nhìn)")]
    [Tooltip("Góc nhìn của mắt (độ). Mắt người thường chú ý tốt ở góc 60 độ (mỗi bên 30 độ).")]
    [Range(10f, 120f)]
    public float fieldOfViewAngle = 60f;

    [Tooltip("Tầm nhìn xa tối đa (mét)")]
    public float viewDistance = 15f;

    public float checkInterval = 0.1f;
    public float maxIgnoreTimeLimit = 45f;

    [Header("Room Setup")]
    public Transform activeRoomParent;

    [Header("UI & Display Settings")]
    public List<TargetNameMapping> customDisplayNames = new List<TargetNameMapping>();

    private bool isTracking = false;
    private float timer = 0f;
    public float totalPresentationTime = 0f;
    public float interactionPercentage = 0f;

    private Dictionary<string, TargetTrackingState> trackingStates = new Dictionary<string, TargetTrackingState>();
    private Dictionary<string, Collider> targetColliders = new Dictionary<string, Collider>();

    [Header("Room Type")]
    public RoomType currentRoomType = RoomType.NormalClass;

    [Header("Gaze Warning Thresholds")]
    public float continuousIgnoreWarningThreshold = 10f; // Hạ xuống 10s để test
    public float judgeIgnoreWarningThreshold = 10f;      // Hạ xuống 10s để test

    [Header("Judge Tracking (Optional)")]
    public string judgeLeftName = "EyeTrackingTargetLeft";
    public string judgeMiddleName = "EyeTrackingTargetMiddle";
    public string judgeRightName = "EyeTrackingTargetRight";

    private bool isWarningActive = false;
    private List<string> currentWarningTargets = new List<string>();

    public enum RoomType
    {
        NormalClass,
        DefenseRoom
    }

    private string GetDisplayName(string objName)
    {
        foreach (var mapping in customDisplayNames)
        {
            if (mapping.objectName == objName) return mapping.displayName;
        }
        return objName;
    }

    void Update()
    {
        if (!isTracking) return;

        totalPresentationTime += Time.deltaTime;
        timer += Time.deltaTime;

        if (timer >= checkInterval)
        {
            PerformVisionCheck();
            UpdateAdviceUI();
            timer = 0f;
        }
    }

    void PerformVisionCheck()
    {
        // Luôn lấy Camera chính để làm gốc tọa độ và hướng nhìn (đặc biệt quan trọng trong VR)
        Transform eyeTransform = Camera.main != null ? Camera.main.transform : this.transform;
        
        string currentTargetName = "Không gian trống";
        float smallestAngle = fieldOfViewAngle / 2f;
        Vector3 gazeForwardPoint = eyeTransform.position + eyeTransform.forward * viewDistance;
        Vector3 eyePos = eyeTransform.position;
        Vector3 eyeForward = eyeTransform.forward;

        foreach (var kvp in targetColliders)
        {
            Collider col = kvp.Value;
            if (col == null || !col.gameObject.activeInHierarchy) continue;

            Vector3 closestPointOnTarget = col.ClosestPoint(gazeForwardPoint);
            Vector3 directionToTarget = (closestPointOnTarget - eyePos).normalized;
            float angleToTarget = Vector3.Angle(eyeForward, directionToTarget);
            float distanceToTarget = Vector3.Distance(eyePos, closestPointOnTarget);

            if (angleToTarget < smallestAngle && distanceToTarget <= viewDistance)
            {
                smallestAngle = angleToTarget;
                currentTargetName = kvp.Key;
            }
        }

        if (totalPresentationTime % 1.0f < 0.1f)
        {
            Debug.Log("[Gaze Debug] Đang nhìn vào: " + (string.IsNullOrEmpty(currentTargetName) ? "Không có gì (-)" : currentTargetName));
        }

        foreach (var kvp in trackingStates)
        {
            if (kvp.Key == currentTargetName)
            {
                kvp.Value.totalLookTime += checkInterval;
                kvp.Value.continuousIgnoredTime = 0f;
            }
            else
            {
                kvp.Value.continuousIgnoredTime += checkInterval;
                
                // Log để debug: Chỉ hiện nếu bị bỏ rơi > 2s để tránh rác console
                if (kvp.Value.continuousIgnoredTime > 2f && totalPresentationTime % 2.0f < 0.1f)
                {
                    Debug.Log("🔍 [Gaze Status]: '" + kvp.Key + "' đã bị bỏ rơi " + kvp.Value.continuousIgnoredTime.ToString("F1") + "s");
                }

                if (kvp.Value.continuousIgnoredTime >= maxIgnoreTimeLimit)
                {
                    kvp.Value.ignoreCount++;
                    kvp.Value.continuousIgnoredTime = 0f;
                }
            }
        }
    }

    private void UpdateAdviceUI()
    {
        if (trackingStates.Count == 0) return;
        if (totalPresentationTime < 2f) return;

        if (isWarningActive)
        {
            if (currentRoomType == RoomType.DefenseRoom)
            {
                List<string> ignoredJudges = new List<string>();
                List<string> ignoredJudgeNames = new List<string>();

                // Kiểm tra 3 vị trí đích danh
                if (IsJudgeIgnored(judgeLeftName)) { ignoredJudges.Add("trái"); ignoredJudgeNames.Add(judgeLeftName); }
                if (IsJudgeIgnored(judgeMiddleName)) { ignoredJudges.Add("giữa"); ignoredJudgeNames.Add(judgeMiddleName); }
                if (IsJudgeIgnored(judgeRightName)) { ignoredJudges.Add("phải"); ignoredJudgeNames.Add(judgeRightName); }

                // Nếu không tìm thấy giám khảo đích danh, kiểm tra toàn bộ targets trong phòng
                if (ignoredJudges.Count == 0)
                {
                    foreach (var kvp in trackingStates)
                    {
                        if (kvp.Value.continuousIgnoredTime >= judgeIgnoreWarningThreshold)
                        {
                            ignoredJudges.Add(GetDisplayName(kvp.Key));
                            ignoredJudgeNames.Add(kvp.Key);
                        }
                    }
                }

                if (ignoredJudges.Count > 0)
                {
                    string positions = string.Join("/", ignoredJudges);
                    string msg = "Bạn đang không nhìn vào giám khảo ở " + positions + ", hãy nhìn vào giám khảo!";
                    if (VRWarningHUD.Instance != null) VRWarningHUD.Instance.ShowWarning(msg, Color.yellow);
                    currentWarningTargets = ignoredJudgeNames;
                }
                else
                {
                    DismissWarning();
                }
                return;
            }

            if (HasUserLookedAtWarningTargets()) DismissWarning();
            return;
        }

        if (currentRoomType == RoomType.DefenseRoom)
        {
            List<string> ignoredJudges = new List<string>();
            List<string> ignoredJudgeNames = new List<string>();

            if (IsJudgeIgnored(judgeLeftName)) { ignoredJudges.Add("trái"); ignoredJudgeNames.Add(judgeLeftName); }
            if (IsJudgeIgnored(judgeMiddleName)) { ignoredJudges.Add("giữa"); ignoredJudgeNames.Add(judgeMiddleName); }
            if (IsJudgeIgnored(judgeRightName)) { ignoredJudges.Add("phải"); ignoredJudgeNames.Add(judgeRightName); }

            // Fallback: Nếu không tìm thấy 3 tên mặc định, lấy tất cả target bị bỏ rơi
            if (ignoredJudges.Count == 0)
            {
                foreach (var kvp in trackingStates)
                {
                    if (kvp.Value.continuousIgnoredTime >= judgeIgnoreWarningThreshold)
                    {
                        ignoredJudges.Add(GetDisplayName(kvp.Key));
                        ignoredJudgeNames.Add(kvp.Key);
                    }
                }
            }

            if (ignoredJudges.Count > 0)
            {
                if (VRWarningHUD.Instance != null)
                {
                    string positions = string.Join("/", ignoredJudges);
                    string msg = "Bạn đang không nhìn vào giám khảo ở " + positions + ", hãy nhìn vào giám khảo!";
                    Debug.Log("🔔 [Gaze Alert - Defense]: Đang hiện cảnh báo lên HUD");
                    VRWarningHUD.Instance.ShowWarning(msg, Color.yellow);
                    isWarningActive = true;
                    currentWarningTargets = ignoredJudgeNames;
                }
                else
                {
                    Debug.LogWarning("⚠️ [Gaze Logic]: Cần hiện cảnh báo nhưng không tìm thấy VRWarningHUD.Instance trong Scene!");
                }
                return;
            }
        }

        if (currentRoomType == RoomType.NormalClass)
        {
            List<string> ignoredNpcs = new List<string>();
            foreach (var kvp in trackingStates)
            {
                if (kvp.Value.continuousIgnoredTime >= continuousIgnoreWarningThreshold)
                    ignoredNpcs.Add(kvp.Key);
            }

            if (ignoredNpcs.Count > 0)
            {
                if (VRWarningHUD.Instance != null)
                {
                    Debug.Log("⚠️ [Gaze Logic]: Đã phát hiện " + ignoredNpcs.Count + " NPC bị bỏ rơi quá 10s!");
                    string msg = "Bạn đang chưa quan sát đều cả lớp, hãy chú ý quan sát nhé!";
                    Debug.Log("🔔 [Gaze Alert - Normal]: Đang hiện cảnh báo lên HUD");
                    VRWarningHUD.Instance.ShowWarning(msg, Color.yellow);
                    isWarningActive = true;
                    currentWarningTargets = ignoredNpcs;
                }
                else
                {
                    Debug.LogWarning("⚠️ [Gaze Logic]: Cần hiện cảnh báo nhưng không tìm thấy VRWarningHUD.Instance trong Scene!");
                }
                return;
            }
        }
    }

    private bool IsJudgeIgnored(string judgeName)
    {
        if (string.IsNullOrEmpty(judgeName)) return false;
        if (!trackingStates.ContainsKey(judgeName)) return false;
        return trackingStates[judgeName].continuousIgnoredTime >= judgeIgnoreWarningThreshold;
    }

    private bool HasUserLookedAtWarningTargets()
    {
        if (currentWarningTargets.Count == 0) return false;
        foreach (string targetName in currentWarningTargets)
        {
            if (trackingStates.ContainsKey(targetName))
            {
                if (trackingStates[targetName].continuousIgnoredTime < 0.5f) return true;
            }
        }
        return false;
    }

    private void DismissWarning()
    {
        isWarningActive = false;
        currentWarningTargets.Clear();
    }

    public void StartTracking(Transform roomParent, RoomType roomType)
    {
        this.activeRoomParent = roomParent;
        this.currentRoomType = roomType;

        if (activeRoomParent == null) return;

        isTracking = true;
        totalPresentationTime = 0f;
        trackingStates.Clear();
        targetColliders.Clear();
        isWarningActive = false;

        Transform[] allChildren = activeRoomParent.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in allChildren)
        {
            // Kiểm tra theo Tag HOẶC theo tên (nếu lỡ quên gắn tag)
            bool isTarget = child.CompareTag("NPC_Face") || 
                            child.name.ToLower().Contains("target") || 
                            child.name.ToLower().Contains("npc") || 
                            child.name.ToLower().Contains("face");

            if (isTarget)
            {
                // Chỉ lấy những cái có Collider (vì cần Raycast/Distance check)
                var col = child.GetComponent<Collider>();
                if (col != null)
                {
                    trackingStates[child.name] = new TargetTrackingState();
                    targetColliders[child.name] = col;
                }
            }
        }
        Debug.Log("👀 GazeTracking: Bắt đầu tracking tại " + activeRoomParent.name + " (" + trackingStates.Count + " targets)");
    }

    public void PauseTracking() => isTracking = false;

    public void ResumeTracking()
    {
        if (trackingStates.Count > 0) isTracking = true;
    }

    public void StopAndExportTracking()
    {
        isTracking = false;
        SaveReportToJSON();
        Debug.Log("🛑 GazeTracking: Đã dừng tracking và lưu báo cáo.");
    }

    public PresentationEvaluationReport GetCurrentReport()
    {
        PresentationEvaluationReport report = new PresentationEvaluationReport();
        report.sessionDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        report.totalSessionTime = totalPresentationTime;
        report.totalAudienceFocusTime = 0f;

        foreach (var kvp in trackingStates)
        {
            report.totalAudienceFocusTime += kvp.Value.totalLookTime;
            float percent = (totalPresentationTime > 0) ? (kvp.Value.totalLookTime / totalPresentationTime) * 100f : 0f;

            report.targetDetails.Add(new TargetFocusData
            {
                targetName = kvp.Key,
                displayName = GetDisplayName(kvp.Key),
                timeLooked = kvp.Value.totalLookTime,
                viewPercentage = percent,
                neglectCount = kvp.Value.ignoreCount
            });
        }

        report.interactionPercentage = (totalPresentationTime > 0) ? (report.totalAudienceFocusTime / totalPresentationTime) * 100f : 0f;
        this.interactionPercentage = report.interactionPercentage;

        if (report.interactionPercentage < 30f) report.interactionGrade = "Kém";
        else if (report.interactionPercentage <= 70f) report.interactionGrade = "Khá";
        else report.interactionGrade = "Tốt";

        return report;
    }

    private void SaveReportToJSON()
    {
        if (totalPresentationTime <= 0) return;
        PresentationEvaluationReport report = GetCurrentReport();
        string fileName = "EyeContact_Latest_Report.json";
        string filePath = Path.Combine(Application.persistentDataPath, fileName);
        File.WriteAllText(filePath, JsonUtility.ToJson(report, true));
    }
}