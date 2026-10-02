using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Crosshair kiểu CS:GO đặt tại tâm màn hình — trùng với hướng ray của Interactor.
/// Tự tạo ở runtime nếu chưa có trong scene: gắn vào canvas chứa "Interact Prompt",
/// là sibling đầu tiên (vẽ dưới cùng) nên overlay của NoteReader che phủ khi mở note.
/// Nếu muốn tinh chỉnh, kéo script lên một object dưới Canvas trong Inspector
/// (bootstrap sẽ bỏ qua vì đã có instance).
/// </summary>
public class CrosshairUI : MonoBehaviour
{
    [Header("Giao diện")]
    [SerializeField] private Color color = new Color(0.2f, 1f, 0.35f, 1f);
    [SerializeField] private int thickness = 2;
    [SerializeField] private int armLength = 5;
    [SerializeField] private int centerGap = 3;
    [SerializeField] private bool showDot = true;
    [SerializeField] private int dotSize = 2;

    [Header("Canvas")]
    [Tooltip("Để trống: tự tìm canvas chứa 'Interact Prompt'.")]
    [SerializeField] private Canvas canvas;

    private static bool bootstrapped;
    private static Texture2D whiteTexture;
    private static Sprite whiteSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (bootstrapped) return;
        bootstrapped = true;

        if (FindFirstObjectByType<CrosshairUI>() != null) return;

        new GameObject("CrosshairUI", typeof(CrosshairUI));
    }

    private void Awake()
    {
        if (canvas == null) canvas = FindGameplayCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("[CrosshairUI] Không tìm thấy canvas Overlay.", this);
            enabled = false;
            return;
        }

        transform.SetParent(canvas.transform, false);
        BuildCrosshair();
        transform.SetAsFirstSibling();
    }

    private void BuildCrosshair()
    {
        if (whiteSprite == null)
        {
            whiteTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            whiteTexture.name = "CrosshairWhite";
            whiteTexture.filterMode = FilterMode.Point;
            whiteTexture.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
            whiteTexture.Apply();
            whiteSprite = Sprite.Create(whiteTexture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 1f);
        }

        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect == null)
            rootRect = gameObject.AddComponent<RectTransform>();

        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = Vector2.zero;
        rootRect.anchoredPosition = Vector2.zero;

        int offset = centerGap + (showDot ? dotSize / 2 + 1 : 0);

        AddBar(rootRect, "Up", thickness, armLength, new Vector2(0f, offset + armLength * 0.5f));
        AddBar(rootRect, "Down", thickness, armLength, new Vector2(0f, -offset - armLength * 0.5f));
        AddBar(rootRect, "Left", armLength, thickness, new Vector2(-offset - armLength * 0.5f, 0f));
        AddBar(rootRect, "Right", armLength, thickness, new Vector2(offset + armLength * 0.5f, 0f));

        if (showDot)
            AddBar(rootRect, "Dot", dotSize, dotSize, Vector2.zero);
    }

    private void AddBar(Transform parent, string name, float width, float height, Vector2 offset)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = offset;

        Image img = go.GetComponent<Image>();
        img.sprite = whiteSprite;
        img.color = color;
        img.raycastTarget = false;
    }

    private static Canvas FindGameplayCanvas()
    {
        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Canvas c in canvases)
        {
            if (c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            if (FindDescendant(c.transform, "Interact Prompt") != null)
                return c;
        }
        foreach (Canvas c in canvases)
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                return c;
        }
        return null;
    }

    private static Transform FindDescendant(Transform parent, string name)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
                return child;
        }
        return null;
    }
}