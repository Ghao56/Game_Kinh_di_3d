using UnityEngine;
using UnityEngine.UI;
using System.Text;

// Gắn script này vào 1 object Text (UI > Legacy > Text) trong Canvas.
// Luôn hiện dòng "Inventory" ở trên cùng. Bên dưới, tự động thêm 1 dòng
// cho MỖI loại vật phẩm đang có số lượng > 0 (ví dụ "Salt: 3", "Wine: 1").
// Vật phẩm nào về 0 sẽ tự biến mất khỏi danh sách.
// KHÔNG cần sửa file này khi thêm vật phẩm mới - nó tự đọc từ PlayerInventory.
public class InventoryUI : MonoBehaviour
{
    public PlayerInventory inventory;

    private Text label;

    void Start()
    {
        label = GetComponent<Text>();

        if (inventory != null)
            inventory.OnInventoryChanged += Refresh;

        Refresh();
    }

    void OnDestroy()
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= Refresh;
    }

    void Refresh()
    {
        if (label == null || inventory == null)
            return;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Inventory");

        foreach (var pair in inventory.GetAllItems())
        {
            if (pair.Value > 0)
                sb.AppendLine($"{pair.Key}: {pair.Value}");
        }

        label.text = sb.ToString();
    }
}