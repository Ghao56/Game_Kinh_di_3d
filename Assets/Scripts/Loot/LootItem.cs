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

    void Reset()
    {
        // Khi gắn mới trong editor: auto đặt tag Item
        if (!gameObject.CompareTag("Item"))
            gameObject.tag = "Item";
    }
}
