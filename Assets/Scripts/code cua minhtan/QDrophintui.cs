using UnityEngine;
using UnityEngine.UI;

// Hint nút bỏ đồ trên màn hình: "[Q] Bỏ đồ" khi cầm loot hoặc đang chọn
// muối/rượu trên hotbar. PlayerGrab mỗi frame đẩy chữ qua Show().
// KHÔNG tự tạo Canvas runtime: object để trong scene từ đầu, tạo bằng menu
// Tools > "Tạo Canvas [Q] Bỏ đồ" (hoặc tự dựng Canvas + child Text + script
// này trong Scene view) - người chơi chỉnh vị trí thoải mái bằng RectTransform.

public class QDropHintUI : MonoBehaviour
{
    public static QDropHintUI Instance { get; private set; }

    [Tooltip("Nhãn hiển thị. Để trống tự lấy child đầu có Text.")]
    [SerializeField] private Text label;

    private string currentText;

    private void Awake()
    {
        if (label == null) label = GetComponentInChildren<Text>(true);
    }

    private void OnEnable() => Instance = this;

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    // text rỗng/null = ẩn. Trùng chữ cũ -> giữ nguyên (không churn UI mỗi frame).
    public void Show(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            currentText = null;
            if (label != null && label.enabled) label.enabled = false;
            return;
        }

        if (label == null) return;

        if (currentText == text && label.enabled) return;

        label.text = text;
        label.enabled = true;
        currentText = text;
    }
}
