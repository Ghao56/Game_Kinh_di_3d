using UnityEngine;

public class DarknessAnxiety : MonoBehaviour
{
    [SerializeField] Transform player;
    [SerializeField] float darkThreshold = 0.15f;    // dưới mức sáng này = đang trong bóng tối
    [SerializeField] float checkRadius = 15f;
    [SerializeField] float anxietyPerSecond = 1.5f;  // hạ thấp để bóng tối chỉ gây áp lực nhẹ
    [SerializeField] float rampUpTime = 5f;          // ở trong tối càng lâu càng tăng nhanh
    [SerializeField] float maxMultiplier = 2f;
    [SerializeField] float checkInterval = 0.2f;

    [Header("Ánh sáng mặt trời (Directional Light)")]
    [SerializeField] bool countSunlight = true;      // ngoài trời không bị che = sáng
    [SerializeField] float sunRayDistance = 100f;

    Light[] lights;
    float nextCheck, nextRefresh, darkTimer;
    bool inDark;

    void Update()
    {
        if (player == null || AnxietyManager.Instance == null) return;

        if (Time.time >= nextRefresh)
        {
            lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            nextRefresh = Time.time + 2f;
        }

        if (Time.time >= nextCheck)
        {
            inDark = GetLightLevel() < darkThreshold;
            nextCheck = Time.time + checkInterval;
        }

        if (inDark)
        {
            darkTimer += Time.deltaTime;
            float mult = Mathf.Lerp(1f, maxMultiplier, darkTimer / rampUpTime);
            AnxietyManager.Instance.Add(anxietyPerSecond * mult * Time.deltaTime);
        }
        else darkTimer = 0f;
    }

    /// Ghi đè hàm này nếu muốn dùng hệ thống ánh sáng riêng của bạn
    protected virtual float GetLightLevel()
    {
        float total = 0f;
        Vector3 p = player.position + Vector3.up;

        foreach (var l in lights)
        {
            if (l == null || !l.enabled || !l.gameObject.activeInHierarchy) continue;

            if (l.type == LightType.Directional)
            {
                // Sáng nếu không có vật nào che giữa người chơi và mặt trời
                if (countSunlight &&
                    !Physics.Raycast(p, -l.transform.forward, sunRayDistance, ~0, QueryTriggerInteraction.Ignore))
                    total += l.intensity;
                continue;
            }

            float d = Vector3.Distance(p, l.transform.position);
            if (d > l.range || d > checkRadius) continue;
            float f = 1f - d / l.range;
            total += l.intensity * f * f;
        }
        return total;
    }
}