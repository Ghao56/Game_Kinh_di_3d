using UnityEngine;
using System;
using System.Collections.Generic;

// Gắn script này vào object Player.
// Kho đồ DÙNG CHUNG cho mọi loại vật phẩm, phân biệt bằng "itemId" (chuỗi tên).
// Muốn thêm vật phẩm mới (rượu, bom khói...) KHÔNG cần sửa file này -
// chỉ cần dùng đúng itemId khi gọi AddItem/UseItem từ script khác.
public class PlayerInventory : MonoBehaviour
{
    private Dictionary<string, int> items = new Dictionary<string, int>();

    // UI (hoặc bất kỳ script nào khác) đăng ký vào đây để tự cập nhật
    // mỗi khi số lượng bất kỳ vật phẩm nào thay đổi.
    public event Action OnInventoryChanged;

    // Gộp các id trùng về 1 kho duy nhất ("Salt"/"Muối" là 1 vật, "Alcohol"/"Wine"/"Rượu" là 1 vật)
    // để túi đồ không hiện 2 dòng cho cùng 1 thứ.
    private static readonly Dictionary<string, string> aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "salt", "Salt" },        { "muối", "Salt" },  { "muoi", "Salt" },
        { "alcohol", "Alcohol" },  { "wine", "Alcohol" }, { "rượu", "Alcohol" }, { "ruou", "Alcohol" },
    };

    // Trả về id chuẩn của 1 id bất kỳ (ví dụ "Muối" -> "Salt")
    public static string CanonicalId(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return itemId;

        return aliases.TryGetValue(itemId.Trim(), out var canonical) ? canonical : itemId.Trim();
    }

    public void AddItem(string itemId, int amount = 1)
    {
        itemId = CanonicalId(itemId);

        if (!items.ContainsKey(itemId))
            items[itemId] = 0;

        items[itemId] += amount;

        Debug.Log($"Nhặt {itemId}! Hiện có: {items[itemId]}");

        OnInventoryChanged?.Invoke();
    }

    // Trả về true nếu dùng được (còn hàng), false nếu hết
    public bool UseItem(string itemId)
    {
        itemId = CanonicalId(itemId);

        if (!items.ContainsKey(itemId) || items[itemId] <= 0)
            return false;

        items[itemId]--;

        OnInventoryChanged?.Invoke();

        return true;
    }

    public int GetCount(string itemId)
    {
        itemId = CanonicalId(itemId);

        return items.ContainsKey(itemId) ? items[itemId] : 0;
    }

    // UI dùng hàm này để duyệt qua toàn bộ vật phẩm đang có mà không cần biết trước tên
    public IReadOnlyDictionary<string, int> GetAllItems()
    {
        return items;
    }
}