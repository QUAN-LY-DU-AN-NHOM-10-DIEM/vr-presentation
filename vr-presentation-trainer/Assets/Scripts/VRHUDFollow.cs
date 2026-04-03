using UnityEngine;

public class VRHUDHardLock : MonoBehaviour
{
    public Transform mainCamera;
    public float distance = 1.0f; // Khoảng cách từ mắt tới UI

    // Dùng LateUpdate để fix lỗi rung giật (jitter) trong VR
    void LateUpdate()
    {
        if (mainCamera == null) return;

        // Ép vị trí nằm ngay trước mặt
        transform.position = mainCamera.position + mainCamera.forward * distance;

        // Ép góc xoay y hệt camera
        transform.rotation = mainCamera.rotation;
    }
}