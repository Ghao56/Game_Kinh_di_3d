using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(CanvasGroup))]
public class InteractPromptUI : MonoBehaviour
{
    [SerializeField] private Text promptText;
    [SerializeField] private float fadeSpeed = 5f;
    [Tooltip("Để trống: tự lấy Main Camera.")]
    [SerializeField] private Camera uiCamera;
    [Tooltip("Dịch prompt lên trên so với điểm neo theo pixel (đã nhân scaleFactor).")]
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 40f);
    [Tooltip("Khoảng cách world (m) đẩy điểm neo lên trên đỉnh bounds của collider.")]
    [SerializeField] private float worldOffset = 0.15f;
    [Tooltip("Giữ prompt không bị chạm mép màn hình.")]
    [SerializeField] private float edgePadding = 40f;

    private CanvasGroup canvasGroup;
    private float targetAlpha;
    private bool followWorld;
    private Transform targetRoot;
    private Collider targetCollider;
    private Transform anchorTransform;
    private Canvas canvas;
    private RectTransform selfRect;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        targetAlpha = 0f;

        selfRect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            Debug.LogWarning("[InteractPromptUI] Chỉ hỗ trợ Canvas Screen Space – Overlay.", this);

        if (uiCamera == null) uiCamera = Camera.main;
        if (uiCamera == null)
            Debug.LogError("[InteractPromptUI] Thiếu Camera (gán uiCamera hoặc đánh tag MainCamera).", this);
    }

    public void Show(string text, Transform root, Collider col)
    {
        if (root == null && col == null)
        {
            Hide();
            return;
        }

        followWorld = true;
        if (root != targetRoot)
            anchorTransform = FindAnchor(root);
        targetRoot = root;
        targetCollider = col;
        SetText(text);
        targetAlpha = 1f;
    }

    public void Hide()
    {
        targetAlpha = 0f;
        followWorld = false;
        targetRoot = null;
        targetCollider = null;
        anchorTransform = null;
    }

    private void Update()
    {
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (!followWorld) return;

        if (targetCollider == null && targetRoot == null)
        {
            targetAlpha = 0f;
            return;
        }

        Vector3 anchor;
        if (anchorTransform != null)
        {
            anchor = anchorTransform.position;
        }
        else if (targetCollider != null)
        {
            Bounds bounds = targetCollider.bounds;
            anchor = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) + Vector3.up * worldOffset;
        }
        else
        {
            anchor = targetRoot.position;
        }

        if (uiCamera == null)
        {
            targetAlpha = 0f;
            return;
        }

        Vector3 sp = uiCamera.WorldToScreenPoint(anchor);
        if (sp.z <= 0f)
        {
            targetAlpha = 0f;
            return;
        }

        float scale = canvas != null ? canvas.scaleFactor : 1f;
        Vector2 pos = new Vector2(sp.x, sp.y) + screenOffset * scale;

        Vector2 half = selfRect.rect.size * 0.5f * scale;
        float minX = edgePadding + half.x;
        float maxX = Mathf.Max(minX, Screen.width - edgePadding - half.x);
        float minY = edgePadding + half.y;
        float maxY = Mathf.Max(minY, Screen.height - edgePadding - half.y);
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        selfRect.position = new Vector3(pos.x, pos.y, 0f);
        targetAlpha = 1f;
    }

    private static Transform FindAnchor(Transform root)
    {
        if (root == null) return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != root && child.name == "PromptAnchor")
                return child;
        }
        return null;
    }

    private void SetText(string text)
    {
        if (promptText == null) return;
        if (text == promptText.text) return;
        promptText.text = text;
    }
}