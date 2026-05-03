using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Quản lý nhóm NPC và điều phối việc đứng lên/ngồi xuống để hỏi câu hỏi (AC4-1).
/// Sử dụng Singleton để truy cập nhanh từ QuestionDialogManager.
/// </summary>
public class NpcManager : MonoBehaviour
{
    public static NpcManager Instance { get; private set; }

    [Header("Cấu hình")]
    public Transform normalHumansParent;
    public Transform defenseHumansParent;
    public ModeManager modeManager;
    public int pickCount = 10;

    private Queue<Transform> npcQueue = new Queue<Transform>();
    private Transform currentNPC = null;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    /// <summary>
    /// Khởi tạo hàng đợi NPC dựa trên phòng hiện tại.
    /// Thường gọi khi kết thúc thuyết trình chính.
    /// </summary>
    public void PrepareNpcQueue()
    {
        npcQueue.Clear();
        currentNPC = null;

        Transform humansParent = (modeManager != null && modeManager.selectedMode == "Defense") ? defenseHumansParent : normalHumansParent;
        if (humansParent == null) return;

        List<Transform> allNPCs = new List<Transform>();
        foreach (Transform child in humansParent) allNPCs.Add(child);

        int count = Mathf.Min(pickCount, allNPCs.Count);
        List<Transform> shuffled = allNPCs.OrderBy(_ => Random.value).ToList();

        for (int i = 0; i < count; i++) npcQueue.Enqueue(shuffled[i]);

        Debug.Log($"[NpcManager] Queue ready! ({npcQueue.Count} NPCs)");
    }

    /// <summary>
    /// Lấy NPC tiếp theo đứng dậy để đặt câu hỏi.
    /// </summary>
    public Transform GetNextNPC()
    {
        // 1. Cho NPC cũ ngồi xuống
        if (currentNPC != null)
        {
            Animator prevAnim = currentNPC.GetComponentInChildren<Animator>();
            if (prevAnim != null) prevAnim.SetTrigger("SitDown");
            else Debug.LogWarning($"[NPC] {currentNPC.name} không có Animator để SitDown!");
        }

        // 2. Lấy NPC mới
        if (npcQueue.Count > 0)
        {
            currentNPC = npcQueue.Dequeue();
            Debug.Log($"[NPC] Đến lượt: {currentNPC.name} đứng lên hỏi.");
            
            Animator anim = currentNPC.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.SetTrigger("StandUp");
            }
            else
            {
                Debug.LogError($"[NPC] {currentNPC.name} không có Animator để StandUp!");
            }
            
            return currentNPC;
        }
        return null;
    }

    /// <summary>
    /// Cho NPC hiện tại ngồi xuống ngay lập tức.
    /// </summary>
    public void ResetCurrentNPC()
    {
        if (currentNPC != null)
        {
            Debug.Log($"[NPC] {currentNPC.name} ngồi xuống sau khi kết thúc trả lời.");
            Animator anim = currentNPC.GetComponentInChildren<Animator>();
            if (anim != null) anim.SetTrigger("SitDown");
            currentNPC = null;
        }
    }
}