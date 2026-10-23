using UnityEngine;

// Gắn lên vật phẩm loot (tag "Item"). Nhặt sẽ tự vào túi đồ của player.
// Ponx: value đơn vị "triệu", size = số ô chiếm trong túi (1..3).
public class LootItem : MonoBehaviour
{
    [Header("Item info")]
    public string itemName = "Đồ cổ";
    [Tooltip("Giá trị (triệu đồng) khi mang về an toàn")]
    public int valueInMillions = 1;
    [Range(1, 3)]
    public int slotCost = 1;

    [Header("Nhận diện khi chất lên xe tải")]
    [Tooltip("Khóa duy nhất của món đồ (ví dụ loot_clock). Để trống thì ô chất đồ từ chối món này.")]
    public string itemId = string.Empty;

    /// Khóa nhận diện (TruckLoadZone / PlayerGrab đọc). Rượu/muối không có món này nên không chất được.
    public string ItemId => itemId;

    private void Awake()
    {
        // Mọi món loot tự có glow khi ngắm + nhãn giá tiền trên đầu.
        if (GetComponent<LootValueLabel>() == null)
            gameObject.AddComponent<LootValueLabel>();
    }

    void Reset()
    {
        // Khi gắn mới trong editor: auto đặt tag Item
        if (!gameObject.CompareTag("Item"))
            gameObject.tag = "Item";
    }
}
