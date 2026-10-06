using System;
using System.Collections.Generic;
using UnityEngine;

/// Một tiếng động trong game: phát ra ở đâu, to cỡ nào, ai phát ra.
/// Enemy AI (viết ở phase khác) chỉ cần subscribe NoiseSystem.OnNoise —
/// không cần biết tiếng đó đến từ bước chân, cửa, hay đồ vật.
public struct NoiseEvent
{
    public Vector3 Position;
    public float Loudness;
    public float Radius;
    public GameObject Source;
    public float Time;
}

/// Bus tiếng động tĩnh: không cần đặt object trong scene, không DontDestroyOnLoad.
public static class NoiseSystem
{
    /// Sự kiện khi có tiếng động. Subscriber tự quyết định có nghe thấy không
    /// (dựa trên khoảng cách, tường chắn, trạng thái của bản thân...).
    public static event Action<NoiseEvent> OnNoise;

    /// Số subscriber hiện tại — dùng cho test/debug.
    public static int SubscriberCount => OnNoise?.GetInvocationList().Length ?? 0;

    private const int MaxRecent = 32;

    private static readonly List<NoiseEvent> recent = new List<NoiseEvent>();
    private static NoiseEvent? lastNoise;

    /// Tiếng động gần nhất (null nếu chưa có gì). Chỉ để debug.
    public static NoiseEvent? LastNoise => lastNoise;

    /// Danh sách tiếng động gần đây, mới nhất ở cuối. Chỉ để vẽ Gizmo.
    public static IReadOnlyList<NoiseEvent> RecentNoises => recent;

    /// Xoá event cũ. Gọi tự động khi bắt đầu play, để không giữ reference
    /// của run trước khi Unity bật "Reload Domain" khi vào Play mode.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnNoise = null;
        recent.Clear();
        lastNoise = null;
    }

    public static void ReportNoise(Vector3 position, float loudness, float radius, GameObject source = null)
    {
        var noise = new NoiseEvent
        {
            Position = position,
            Loudness = Mathf.Max(0f, loudness),
            Radius = Mathf.Max(0f, radius),
            Source = source,
            Time = Time.time
        };

        lastNoise = noise;
        recent.Add(noise);

        if (recent.Count > MaxRecent)
            recent.RemoveRange(0, recent.Count - MaxRecent);

        OnNoise?.Invoke(noise);
    }
}

/// Vẽ vòng tròn bán kính nghe của các tiếng động gần đây. Bật khi debug âm thanh / AI.
public class NoiseDebugView : MonoBehaviour
{
    [Tooltip("Tắt trong build: Gizmos không được vẽ khi chơi, nhưng component thừa thì mất.")]
    [SerializeField] private bool drawGizmos = true;

    [Tooltip("Chỉ vẽ tiếng động mới hơn số giây này.")]
    [SerializeField] private float maxAge = 3f;

    [SerializeField] [Min(1)] private int maxDrawn = 8;

    private void OnDrawGizmos()
    {
        if (!drawGizmos || NoiseSystem.RecentNoises.Count == 0) return;

        int drawn = 0;

        // recent chứa theo thứ tự thời gian, nên duyệt từ cuối về và dừng khi quá già.
        for (int i = NoiseSystem.RecentNoises.Count - 1; i >= 0 && drawn < maxDrawn; i--)
        {
            NoiseEvent noise = NoiseSystem.RecentNoises[i];
            float age = Time.time - noise.Time;
            if (age > maxAge) break;

            float fade = 1f - age / maxAge;
            Gizmos.color = new Color(1f, 0.45f, 0.1f, fade * 0.6f);
            Gizmos.DrawWireSphere(noise.Position, noise.Radius);
            drawn++;
        }
    }
}