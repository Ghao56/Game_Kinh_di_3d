using System;
using UnityEngine;

/// Nguồn sự thật về TIỀN của lượt chơi: tổng nợ + tổng giá trị đã chất lên xe.
///
/// Tiến độ quest `loot_truck/chat_do` bị kẹp ở required (50) nên không biết được
/// "tiền dư" nếu người chơi chất vượt nợ. DebtTracker cộng GIÁ TRỊ THÔ không kẹp
/// mỗi lần chất đồ — HUD nợ, màn kết (tiền dư) và GhostEscalation (mốc leo thang)
/// đều đọc từ đây thay vì tự đếm.
///
/// Class tĩnh không cần đặt trong scene; dữ liệu tự về 0 khi Game scene load lại
/// (cơ chế static reset, giống NoiseSystem).
public static class DebtTracker
{
    /// Giá nợ mặc định: trùng với Quest_LootTruck (50 triệu). Chỉ dùng khi scene
    /// chưa có QuestManager/quest trong registry (vào menu...).
    public const int DefaultDebtMillions = 50;

    /// Tổng nợ của lượt hiện tại (triệu đồng). Đọc từ quest loot_truck nếu có.
    public static int TotalDebtMillions => ResolveTotalDebt();

    /// Tổng giá trị (triệu) đã chất lên xe, KHÔNG kẹp ở 50.
    public static int UncappedPaidMillions { get; private set; }

    /// Bung khi có món đồ được chất lên xe (kể cả khi đã đủ nợ).
    public static event Action<int> OnPaidChanged;

    private const string DebtQuestId = "loot_truck";
    private const string DebtObjectiveId = "chat_do";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        UncappedPaidMillions = 0;
        OnPaidChanged = null;
        warnedNoRegistry = false;
    }

    /// Scene mới load → QuestManager của scene cũ đã destroy, tiến độ về 0.
    public static void ResetRun() => UncappedPaidMillions = 0;

    /// Cộng tiền thô. TruckLoadZone gọi MỘT lần cho mỗi món (chất lại món bị nhặt
    /// ra khỏi xe không được cộng, chống ăn gian — giống tiến độ quest).
    public static void AddPaid(int millions)
    {
        if (millions <= 0) return;

        UncappedPaidMillions += millions;
        OnPaidChanged?.Invoke(UncappedPaidMillions);
    }

    /// Trừ đối xứng với AddPaid: món đang nằm trên xe bị nhặt ra (Reclaim) hoặc
    /// hủy. Khớp đúng hành vi quest hiện có (ReportProgress -amount) để thanh
    /// "tiền trên xe" của HUD/màn kết không lệch với tiến độ nhiệm vụ.
    public static void SubtractPaid(int millions)
    {
        if (millions <= 0) return;

        int before = UncappedPaidMillions;
        UncappedPaidMillions = Mathf.Max(0, before - millions);
        if (UncappedPaidMillions != before) OnPaidChanged?.Invoke(UncappedPaidMillions);
    }

    /// Số tiền đã tính vào tiến độ nợ (kẹp ở tổng nợ — dùng cho HUD).
    public static int DebtPaidMillions => Mathf.Min(UncappedPaidMillions, TotalDebtMillions);

    /// Số nợ còn thiếu. 0 = đã trả hết (dù chưa chất xong mọi món).
    public static int RemainingDebtMillions => Mathf.Max(0, TotalDebtMillions - UncappedPaidMillions);

    /// Tiền dư nếu trả xong nợ (0 nếu chưa đủ).
    public static int BonusMillions =>
        UncappedPaidMillions > TotalDebtMillions ? UncappedPaidMillions - TotalDebtMillions : 0;

    private static int ResolveTotalDebt()
    {
        QuestManager manager = QuestManager.Instance;
        if (manager == null) return DefaultWithWarning();

        if (manager.TryGetRequired(DebtQuestId, DebtObjectiveId, out int required))
            return required;

        return DefaultWithWarning();
    }

    /// Báo một lần duy nhất khi phải dùng nợ mặc định (không spam mỗi frame HUD hỏi).
    private static int DefaultWithWarning()
    {
        if (!warnedNoRegistry)
        {
            warnedNoRegistry = true;
            Debug.LogWarning("[DebtTracker] Không đọc được 'loot_truck/chat_do' trong registry — dùng nợ mặc định 50M.");
        }

        return DefaultDebtMillions;
    }

    private static bool warnedNoRegistry;
}
