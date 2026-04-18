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
    public TextMeshProUGUI liveAdviceTextUI;
    public List<TargetNameMapping> customDisplayNames = new List<TargetNameMapping>();

    private bool isTracking = false;
    private float timer = 0f;
    private float totalPresentationTime = 0f;

    private Dictionary<string, TargetTrackingState> trackingStates = new Dictionary<string, TargetTrackingState>();
    private Dictionary<string, Collider> targetColliders = new Dictionary<string, Collider>(); // Lưu trữ Collider để tính toán

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

    // ĐÃ THAY THẾ SPHERECAST BẰNG HỆ THỐNG TÍNH GÓC NHÌN (FOV CONE)
    void PerformVisionCheck()
    {
        string currentTargetName = "Không gian trống";

        // Góc nhỏ nhất tính từ trung tâm mắt (Giúp xác định bạn đang nhìn trực diện vào ai nhất nếu có nhiều người trong tầm nhìn)
        float smallestAngle = fieldOfViewAngle / 2f;

        // Giả lập điểm mà mắt đang nhìn thẳng tới ở tít đằng xa
        Vector3 gazeForwardPoint = transform.position + transform.forward * viewDistance;

        foreach (var kvp in targetColliders)
        {
            Collider col = kvp.Value;
            if (col == null || !col.gameObject.activeInHierarchy) continue;

            // 1. Tìm điểm trên khối Collider nằm gần nhất với tia nhìn thẳng của bạn
            // (Hàm này cực kỳ lợi hại, nó hoạt động hoàn hảo cho cả khối hộp siêu to của Lớp học lẫn khối nhỏ của NPC)
            Vector3 closestPointOnTarget = col.ClosestPoint(gazeForwardPoint);

            // 2. Tính hướng từ Mắt tới cái điểm đó
            Vector3 directionToTarget = (closestPointOnTarget - transform.position).normalized;

            // 3. Tính góc lệch giữa hướng mắt đang nhìn thẳng và hướng tới mục tiêu
            float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);

            // 4. Kiểm tra xem điểm đó có nằm trong "Hình nón tầm nhìn" và trong giới hạn khoảng cách không
            float distanceToTarget = Vector3.Distance(transform.position, closestPointOnTarget);

            if (angleToTarget < smallestAngle && distanceToTarget <= viewDistance)
            {
                // Nếu mục tiêu này nằm gần vị trí trung tâm mắt hơn mục tiêu trước đó
                smallestAngle = angleToTarget;
                currentTargetName = kvp.Key;
            }
        }

        // Cập nhật bộ đếm thời gian
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
        if (liveAdviceTextUI == null || totalPresentationTime < 5f || trackingStates.Count == 0) return;

        float minLookPercent = 100f;
        string leastLookedTarget = "";

        foreach (var kvp in trackingStates)
        {
            float targetPercent = (kvp.Value.totalLookTime / totalPresentationTime) * 100f;
            if (targetPercent < minLookPercent)
            {
                minLookPercent = targetPercent;
                leastLookedTarget = kvp.Key;
            }
        }

        float expectedPercent = 100f / trackingStates.Count;

        if (!string.IsNullOrEmpty(leastLookedTarget) && minLookPercent < (expectedPercent - 10f))
        {
            string niceName = GetDisplayName(leastLookedTarget);
            liveAdviceTextUI.text = $"Gợi ý: Bạn đang ít chú ý đến [{niceName}]. Hãy lia mắt qua đó nhé!";
            liveAdviceTextUI.color = new Color(1f, 0.6f, 0f);
        }
        else
        {
            liveAdviceTextUI.text = "Tốt: Bạn đang phân bổ ánh mắt rất đồng đều!";
            liveAdviceTextUI.color = Color.green;
        }
    }

    public void StartTracking()
    {
        if (activeRoomParent == null) return;

        totalPresentationTime = 0f;
        trackingStates.Clear();
        targetColliders.Clear(); // Xóa list collider cũ

        // Tìm tất cả NPC_Face và lưu luôn cả Collider của chúng để tính toán Toán học
        Transform[] allChildren = activeRoomParent.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in allChildren)
        {
            if (child.CompareTag("NPC_Face"))
            {
                Collider col = child.GetComponent<Collider>();
                if (col != null)
                {
                    if (!trackingStates.ContainsKey(child.name))
                    {
                        trackingStates.Add(child.name, new TargetTrackingState());
                        targetColliders.Add(child.name, col);
                    }
                }
                else
                {
                    Debug.LogWarning($"[Eye-Tracking] Khán giả {child.name} có tag NPC_Face nhưng CHƯA CÓ COLLIDER!");
                }
            }
        }

        isTracking = true;
        Debug.Log($"[Eye-Tracking] BẮT ĐẦU. Góc nhìn (FOV): {fieldOfViewAngle} độ.");
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
    }

    private void SaveReportToJSON()
    {
        if (totalPresentationTime <= 0) return;

        PresentationEvaluationReport report = new PresentationEvaluationReport();
        report.sessionDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        report.totalSessionTime = totalPresentationTime;
        report.totalAudienceFocusTime = 0f;

        foreach (var kvp in trackingStates)
        {
            report.totalAudienceFocusTime += kvp.Value.totalLookTime;
            float percent = (kvp.Value.totalLookTime / totalPresentationTime) * 100f;

            report.targetDetails.Add(new TargetFocusData
            {
                targetName = kvp.Key,
                displayName = GetDisplayName(kvp.Key),
                timeLooked = kvp.Value.totalLookTime,
                viewPercentage = percent,
                neglectCount = kvp.Value.ignoreCount
            });
        }

        report.interactionPercentage = (report.totalAudienceFocusTime / totalPresentationTime) * 100f;
        if (report.interactionPercentage < 30f) report.interactionGrade = "Kém";
        else if (report.interactionPercentage <= 70f) report.interactionGrade = "Khá";
        else report.interactionGrade = "Tốt";

        // Tên file cố định để luôn tự động Override file cũ
        string fileName = "EyeContact_Latest_Report.json";
        string filePath = Path.Combine(Application.persistentDataPath, fileName);

        File.WriteAllText(filePath, JsonUtility.ToJson(report, true));
    }
}