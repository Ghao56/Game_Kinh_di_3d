using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

// Menu tiện ích: tạo sẵn Canvas + Text + QDropHintUI trong scene đang mở để
// người chơi kéo vị trí thoải mái. Chạy 1 lần; đã có thì chỉ chọn object cũ.
public static class QDropHintCreator
{
    [MenuItem("Tools/Tạo Canvas [Q] Bỏ đồ")]
    public static void Create()
    {
        QDropHintUI existing = Object.FindFirstObjectByType<QDropHintUI>();
        if (existing != null)
        {
            Debug.LogWarning($"[QDropHint] Canvas hint đã có trong scene: '{existing.name}'. Chọn nó để chỉnh vị trí.");
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        GameObject canvasGO = new GameObject("QDropHintCanvas", typeof(Canvas), typeof(QDropHintUI));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Tạo Canvas [Q] Bỏ đồ");

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500; // nổi trên UI thường của scene

        GameObject labelGO = new GameObject("QDropHintText", typeof(Text));
        labelGO.transform.SetParent(canvasGO.transform, false);

        Text label = labelGO.GetComponent<Text>();
        Font font = null;
        try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (font == null)
        {
            try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        }
        if (font != null) label.font = font;
        label.fontSize = 26;
        label.color = new Color(1f, 0.95f, 0.85f);
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        label.text = "[Q] Bỏ đồ";

        RectTransform rt = labelGO.GetComponent<RectTransform>();
        // Anchor preset: đáy giữa màn hình, dễ kéo lên/xuống.
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 70f);
        rt.sizeDelta = new Vector2(420f, 44f);

        Selection.activeGameObject = canvasGO;
        Debug.Log(
            "[QDropHint] Đã tạo 'QDropHintCanvas' trong scene. Di chuyển object " +
            "QDropHintText (hoặc chỉnh anchor trong Inspector) để đổi vị trí " +
            "hint; hiệu ứng chỉ hiện khi: cầm loot hoặc chọn muối/rượu hotbar.");
    }
}
