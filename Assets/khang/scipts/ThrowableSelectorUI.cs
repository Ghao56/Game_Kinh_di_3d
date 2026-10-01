using System.Text;
using UnityEngine;
using UnityEngine.UI;

// Gắn script này vào 1 object Text (UI > Legacy > Text) trong Canvas.
// Hiển thị danh sách vật phẩm ném, đánh dấu ">" ở vật phẩm đang chọn.
// Tự cập nhật mỗi khi người chơi đổi vật phẩm bằng phím 1-9.
// Nếu muốn đổi thứ tự phím, chỉ cần kéo lại mảng Throwables trên Player.
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

        Refresh(); // vẽ ngay lần đầu, không đợi người chơi bấm phím
    }

    void OnDestroy()
    {
        if (selector != null)
            selector.OnSelectionChanged -= Refresh;
    }

    void Refresh()
    {
        StringBuilder sb = new StringBuilder();

        int count = Mathf.Min(selector.ThrowableCount, 9);

        for (int i = 0; i < count; i++)
        {
            string mark = i == selector.CurrentIndex ? "> " : "  ";
            sb.AppendLine($"{mark}[{i + 1}] {selector.GetItemName(i)}");
        }

        sb.Append($"({selector.throwKey} để ném)");

        label.text = sb.ToString();
    }
}
