using UnityEngine;

// Một mục nằm trong hotbar. Mục có thể chiếm 1 hoặc nhiều ô LIÊN TIẾP.
// Mọi ô mà mục chiếm đều trỏ về CÙNG một HotbarEntry, nhờ vậy vật phẩm
// nhiều ô không bao giờ bị đăng ký/nhân đôi thành hai vật khác nhau.
public class HotbarEntry
{
    // Id kho (Salt/Alcohol/loot_...) và tên hiển thị.
    public string ItemId;
    public string DisplayName;

    // Ô bắt đầu (0-based) và số ô liên tiếp mà mục này chiếm.
    public int SlotSize = 1;
    public int StartSlot;

    // Chỉ một trong hai loại được gán:
    // - Throwable: vật phẩm ném được (muối, rượu...).
    // - Loot: vật phẩm loot thật đang được giữ trong tay/ba lô.
    public IThrowable Throwable;
    public LootItem Loot;

    public bool IsThrowable => Throwable != null;
    public bool IsLoot => Loot != null;

    // Ô cuối cùng (inclusive).
    public int EndSlot => StartSlot + SlotSize - 1;

    public int Count
    {
        get
        {
            if (IsThrowable)
            {
                // Interface không dùng được phép so sánh null của UnityEngine.Object,
                // nên kiểm tra riêng để tránh MissingReferenceException nếu object bị huỷ.
                if (Throwable is Object unityObject && unityObject == null)
                    return 0;

                return Throwable.GetCount();
            }

            return Loot != null ? 1 : 0;
        }
    }

    public string Name
    {
        get
        {
            if (!string.IsNullOrEmpty(DisplayName))
                return DisplayName;
            if (!string.IsNullOrEmpty(ItemId))
                return ItemId;
            return "?";
        }
    }

    public bool Occupies(int slot)
    {
        return slot >= StartSlot && slot < StartSlot + SlotSize;
    }

    public bool IsUsable => SlotSize > 0 && Count > 0;
}
