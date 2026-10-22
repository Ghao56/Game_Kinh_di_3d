using System;
using System.Collections.Generic;
using UnityEngine;

// Túi đồ bằng ô (slot). Mặc định 6 ô. Đặt trên Player.
// Sử dụng static Instance để PlayerGrab / vùng an toàn gọi được mà không cần kéo reference.
public class PlayerLootBag : MonoBehaviour
{
    public static PlayerLootBag Instance { get; private set; }

    [Header("Cấu hình túi")]
    [SerializeField] int capacity = 6;

    [Header("Trạng thái")]
    [SerializeField] int usedSlots;
    [SerializeField] int bankedMillions; // tiền đã về an toàn

    readonly List<LootItem> carried = new();

    // UI hoặc khác cần cập nhật khi túi đổi
    public event Action OnChanged;

    public int UsedSlots => usedSlots;
    public int Capacity => capacity;
    public int CarriedMillions { get { int s = 0; foreach (var i in carried) s += i.valueInMillions; return s; } }
    public int BankedMillions => bankedMillions;
    public IReadOnlyList<LootItem> Carried => carried;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Đủ ô trống để nhặt đồ size này không?
    public bool CanTake(int slotCost) => usedSlots + slotCost <= capacity;

    // Thêm item vào túi. Trả false nếu thiếu ô.
    public bool TryTake(LootItem item)
    {
        if (item == null || !CanTake(item.slotCost)) return false;

        carried.Add(item);
        usedSlots += item.slotCost;
        OnChanged?.Invoke();
        return true;
    }

    // Xoá món khỏi túi (khi lấy ra cầm tay hoặc khi đã chất lên xe). Trả false nếu không có.
    public bool Remove(LootItem item)
    {
        if (item == null || !carried.Remove(item)) return false;

        usedSlots = Mathf.Max(0, usedSlots - item.slotCost);
        OnChanged?.Invoke();
        return true;
    }

    // Bị ma bắt / bỏ chạy dang dở: mất toàn bộ đồ đang mang (tiền đã bank giữ nguyên)
    public void LoseAll()
    {
        carried.Clear();
        usedSlots = 0;
        OnChanged?.Invoke();
    }

    // Về vùng an toàn: cộng tiền, xóa túi. Trả về số tiền vừa nhận.
    public int CashOut()
    {
        int payout = CarriedMillions;
        bankedMillions += payout;
        carried.Clear();
        usedSlots = 0;
        OnChanged?.Invoke();
        return payout;
    }
}
