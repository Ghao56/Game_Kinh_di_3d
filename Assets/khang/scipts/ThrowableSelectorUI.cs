using System.Text;
using UnityEngine;
using UnityEngine.UI;

// Gắn script này vào 1 object Text (UI > Legacy > Text) trong Canvas.
// Hiển thị các mục đang có trong hotbar 4 ô, đánh dấu ">" ở mục đang chọn.
// Tự cập nhật mỗi khi người chơi đổi vật phẩm bằng phím 1-4.
public class ThrowableSelectorUI : MonoBehaviour
{
    public ThrowableSelector selector;

    // Để trống thì script tự lấy Text nằm trên cùng object này.
    public Text label;

    void Start()
    {
        if (label == null)
            label = GetComponent<Text>();

        if (selector == null || label == null)
        {
            Debug.LogWarning("ThrowableSelectorUI: chưa gán Selector hoặc Text.");
            enabled = false;
            return;
        }

        selector.OnSelectionChanged += Refresh;
        selector.OnHotbarChanged += Refresh;

        Refresh(); // vẽ ngay lần đầu, không đợi người chơi bấm phím
    }

    void OnDestroy()
    {
        if (selector != null)
        {
            selector.OnSelectionChanged -= Refresh;
            selector.OnHotbarChanged -= Refresh;
        }
    }

    void Refresh()
    {
        StringBuilder sb = new StringBuilder();

        HotbarEntry selected = selector.SelectedEntry;

        // Duyệt đủ 4 ô nhưng mỗi mục chỉ in một dòng (ở ô bắt đầu).
        for (int i = 0; i < selector.SlotCount; i++)
        {
            HotbarEntry entry = selector.GetEntryAt(i);
            if (entry == null || i != entry.StartSlot)
                continue;

            string mark = ReferenceEquals(entry, selected) ? "> " : "  ";
            string slots = entry.SlotSize > 1 ? $" [{i + 1}-{entry.EndSlot + 1}]" : $" [{i + 1}]";
            string count = entry.Count > 1 ? $" x{entry.Count}" : "";
            sb.AppendLine($"{mark}{slots} {entry.Name}{count}");
        }

        sb.Append($"({selector.throwKey} để ném)");

        label.text = sb.ToString();
    }
}
