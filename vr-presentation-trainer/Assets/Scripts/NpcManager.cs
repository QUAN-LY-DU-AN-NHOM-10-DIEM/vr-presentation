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
        Debug.Log($"Queue ready! Press Space to call NPCs. ({npcQueue.Count} remaining)");
    }

    private void Update()
    {
        if (!isActive) return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            // NPC trước đó ngồi xuống
            if (currentNPC != null)
            {
                Animator prevAnimator = currentNPC.GetComponent<Animator>();
                if (prevAnimator != null)
                {
                    prevAnimator.SetTrigger("SitDown");
                }
                Debug.Log($"{currentNPC.name} sat down.");
            }

            // Lấy NPC tiếp theo đứng dậy
            currentNPC = GetNextNPC();
            if (currentNPC != null)
            {
                Animator animator = currentNPC.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.SetTrigger("StandUp");
                }
                Debug.Log($"{currentNPC.name} stood up! ({npcQueue.Count} remaining)");
            }
            else
            {
                // Hết queue
                isActive = false;
                currentNPC = null;
                Debug.Log("All NPCs are done!");
            }
        }
    }

    public Transform GetNextNPC()
    {
        if (npcQueue.Count > 0)
            return npcQueue.Dequeue();

        return null;
    }
}