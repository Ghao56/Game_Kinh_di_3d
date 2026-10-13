using System;
using System.Collections.Generic;
using UnityEngine;

/// Bus tín hiệu tĩnh giữa các bước của flow và vật trong scene.
/// Giống NoiseSystem: không cần đặt object, không DontDestroyOnLoad.
public static class FlowBus
{
    public static event Action<string> OnSignal;

    /// Số subscriber hiện tại — dùng cho test/debug.
    public static int SubscriberCount => OnSignal?.GetInvocationList().Length ?? 0;

    private const int MaxRecent = 32;

    private static readonly List<string> recent = new List<string>();

    /// Tín hiệu gần đây, mới nhất ở cuối. Dùng cho WasRaised và HUD debug.
    public static IReadOnlyList<string> Recent => recent;

    /// Xoá event cũ khi bắt đầu play để không giữ reference của run trước
    /// khi Unity tắt "Reload Domain".
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnSignal = null;
        recent.Clear();
    }

    public static void Raise(string signal)
    {
        if (string.IsNullOrEmpty(signal)) return;

        recent.Add(signal);
        if (recent.Count > MaxRecent) recent.RemoveRange(0, recent.Count - MaxRecent);

        OnSignal?.Invoke(signal);
    }

    /// Tín hiệu này đã được phát trong run hiện tại chưa (giữ 32 tín hiệu gần nhất).
    public static bool WasRaised(string signal)
    {
        if (string.IsNullOrEmpty(signal)) return false;
        return recent.Contains(signal);
    }
}
