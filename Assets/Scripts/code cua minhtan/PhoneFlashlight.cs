using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem; // Dùng Input System mới thay cho UnityEngine.Input

public class PhoneFlashlight : MonoBehaviour
{
    [Header("Gắn đèn vào đây")]
    [SerializeField] private Light flashlight;

    [Header("Trạng thái mặc định")]
    [SerializeField] private bool isFlashlightOn = false;

    void Start()
    {
        if (flashlight == null)
        {
            Debug.LogWarning($"[{nameof(PhoneFlashlight)}] Chưa gán 'flashlight' trong Inspector!", this);
            return;
        }

        flashlight.enabled = isFlashlightOn;
    }

    void Update()
    {
        if (flashlight == null) return;

        // Mouse.current có thể null nếu build không có chuột kết nối (hiếm khi xảy ra trên PC)
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            // Chặn toggle khi đang click vào UI (nút bấm, menu...)
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            ToggleFlashlight();
        }
    }

    void ToggleFlashlight()
    {
        isFlashlightOn = !isFlashlightOn;
        flashlight.enabled = isFlashlightOn;
    }
}