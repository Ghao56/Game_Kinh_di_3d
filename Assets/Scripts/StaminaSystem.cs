using UnityEngine;

public class StaminaSystem : MonoBehaviour
{
    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float drainPerSecond = 20f;
    [SerializeField] private float regenPerSecond = 15f;

    [Tooltip("Số giây chờ sau khi ngừng chạy rồi mới bắt đầu hồi.")]
    [SerializeField] private float regenDelay = 1f;

    [Tooltip("Sau khi cạn hẳn (về 0), phải hồi tới mức này (0..1) mới được chạy nhanh lại.")]
    [SerializeField, Range(0f, 1f)] private float recoverThreshold = 0.25f;

    private float current;
    private float regenTimer;
    private bool exhausted;

    public float Normalized => maxStamina <= 0f ? 0f : current / maxStamina;
    public bool CanSprint => !exhausted && current > 0f;

    private void Awake()
    {
        current = maxStamina;
    }

    // Gọi đúng một lần mỗi frame từ ThirdPersonController.
    public void Tick(bool isSprinting, float deltaTime)
    {
        if (isSprinting)
        {
            current = Mathf.Max(0f, current - drainPerSecond * deltaTime);
            regenTimer = regenDelay;

            if (current <= 0f)
            {
                exhausted = true;
            }

            return;
        }

        if (regenTimer > 0f)
        {
            regenTimer -= deltaTime;
            return;
        }

        current = Mathf.Min(maxStamina, current + regenPerSecond * deltaTime);

        if (exhausted && Normalized >= recoverThreshold)
        {
            exhausted = false;
        }
    }
}