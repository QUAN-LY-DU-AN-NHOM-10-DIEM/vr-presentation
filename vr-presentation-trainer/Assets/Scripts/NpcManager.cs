using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

public class NpcManager : MonoBehaviour
{
    [Header("References")]
    public Transform normalHumansParent;
    public Transform defenseHumansParent;
    public ModeManager modeManager;

    [Header("NPC Settings")]
    public int pickCount = 10;

    private Queue<Transform> npcQueue = new Queue<Transform>();
    private bool isActive = false;
    private Transform currentNPC = null;

    public void OnFinishButtonClick()
    {
        npcQueue.Clear();
        currentNPC = null;

        Transform humansParent = modeManager.selectedMode == "Defense" ? defenseHumansParent : normalHumansParent;

        List<Transform> allNPCs = new List<Transform>();
        foreach (Transform child in humansParent)
        {
            allNPCs.Add(child);
        }

        int count = Mathf.Min(pickCount, allNPCs.Count);
        List<Transform> shuffled = allNPCs.OrderBy(_ => Random.value).ToList();

        for (int i = 0; i < count; i++)
        {
            npcQueue.Enqueue(shuffled[i]);
        }

        isActive = true;
        Debug.Log($"Queue ready! ({npcQueue.Count} NPCs remaining)");
    }

    private void Update()
    {
        if (!isActive) return;

        // Giữ lại phím Space để bạn dễ test thủ công trong Editor
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            GetNextNPC();
        }
    }

    // Đã gom toàn bộ logic Animation vào hàm này
    public Transform GetNextNPC()
    {
        // 1. Nếu có NPC đang đứng trước đó, bắt nó ngồi xuống
        if (currentNPC != null)
        {
            Animator prevAnimator = currentNPC.GetComponent<Animator>();
            if (prevAnimator != null)
            {
                prevAnimator.SetTrigger("SitDown");
                Debug.Log($"{currentNPC.name} sat down.");
            }
        }

        // 2. Lấy NPC tiếp theo từ hàng đợi
        if (npcQueue.Count > 0)
        {
            currentNPC = npcQueue.Dequeue();

            // 3. Cho NPC mới đứng lên
            Animator animator = currentNPC.GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetTrigger("StandUp");
                Debug.Log($"{currentNPC.name} stood up! ({npcQueue.Count} remaining)");
            }

            return currentNPC;
        }

        // 4. Nếu hàng đợi đã trống
        isActive = false;
        currentNPC = null;
        Debug.Log("All NPCs are done!");

        return null;
    }
}