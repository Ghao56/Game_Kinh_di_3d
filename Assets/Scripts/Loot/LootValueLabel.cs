using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// Món loot: người chơi ngắm vào thì bật glow outline (màu vàng) như interactable
/// và hiện giá trị trên đầu vật (chữ xanh dương, luôn vẽ đè lên mọi vật).
/// Child "ValueLabel" được tạo & hiện ngay trong edit để chỉnh; khi chạy game thì ẩn,
/// ngắm vào mới hiện. Sửa style ở MỘT món thì mọi LootValueLabel trong scene đổi theo.
[ExecuteAlways]
public class LootValueLabel : MonoBehaviour
{
    private const string LabelName = "ValueLabel";
    private const string OverlayMaterialPath = "LootLabelOverlay";

    [Header("Giá tiền (áp cho tất cả label trong scene)")]
    [SerializeField] private Color labelColor = new Color(0.25f, 1f, 0.3f, 1f);
    [SerializeField] private float fontSize = 42f;
    [Tooltip("Độ rộng khung chữ (đơn vị world trước khi nhân Scale).")]
    [SerializeField] private float labelWidth = 22f;
    [Tooltip("Scale tổng của nhãn. 0.1 ≈ chữ cao ~0.4m; tăng/giảm tùy ý.")]
    [SerializeField] private float labelScale = 0.1f;
    [Tooltip("Vị trí chữ so với vật (local): 0,0,0 = ngay giữa vật.")]
    [SerializeField] private Vector3 labelOffset = Vector3.zero;

    [Header("Hiển thị")]
    [SerializeField] private float aimDistance = 6f;

    [Header("Glow")]
    [SerializeField] private Color glowColor = new Color(1f, 0.8f, 0.15f, 1f);

    private LootItem loot;
    private InteractableGlow glow;
    private TextMeshPro label;
    private Camera cam;
    private bool shown;
    private static Material overlayMaterial;
    private static bool overlayLoaded;
    private static bool syncing;

    private void OnEnable()
    {
        EnsureLabel();
        ApplyStyle();

        if (!Application.isPlaying) return;
        if (label == null) return;

        label.gameObject.SetActive(false);

        glow = GetComponent<InteractableGlow>();
        if (glow == null) glow = gameObject.AddComponent<InteractableGlow>();
        glow.SetGlowColor(glowColor);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        bool aimed = IsAimed();
        if (aimed != shown)
        {
            shown = aimed;
            label.gameObject.SetActive(aimed);
            glow.SetHighlighted(aimed);
        }

        if (aimed) label.transform.rotation = cam.transform.rotation;
    }

    private bool IsAimed()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (!Physics.Raycast(ray, out RaycastHit hit, aimDistance, ~0, QueryTriggerInteraction.Ignore))
            return false;

        return hit.collider != null && hit.collider.transform.IsChildOf(transform);
    }

    private void EnsureLabel()
    {
        if (label != null) return;

        Transform child = transform.Find(LabelName);
        if (child == null)
        {
            var go = new GameObject(LabelName);
            go.transform.SetParent(transform, false);
            child = go.transform;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

        label = child.GetComponent<TextMeshPro>();
        if (label == null)
        {
            label = child.gameObject.AddComponent<TextMeshPro>();
        }
        if (label.font == null) label.font = TMP_Settings.defaultFontAsset;

        loot = GetComponent<LootItem>();
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.text = (loot != null ? loot.valueInMillions : 0) + "M VND";

        AssignOverlayMaterial();
    }

    // Chữ vẽ đè lên mọi vật + viền: material overlay TMP (ZTest Always, OUTLINE_ON).
    private void AssignOverlayMaterial()
    {
        if (!overlayLoaded)
        {
            overlayMaterial = Resources.Load<Material>(OverlayMaterialPath);
            overlayLoaded = true;
        }

        if (overlayMaterial != null) label.fontSharedMaterial = overlayMaterial;
    }

    private void ApplyStyle()
    {
        if (label == null) return;

        label.transform.localPosition = labelOffset;
        label.transform.localScale = Vector3.one * Mathf.Max(0.01f, labelScale);
        label.rectTransform.sizeDelta = new Vector2(labelWidth, labelWidth);
        label.color = labelColor;
        label.fontSize = fontSize;
    }

    private void OnValidate()
    {
        labelScale = Mathf.Max(0.01f, labelScale);
        fontSize = Mathf.Max(1f, fontSize);
        labelWidth = Mathf.Max(1f, labelWidth);
        aimDistance = Mathf.Max(1f, aimDistance);

        EnsureLabel();
        ApplyStyle();
        SyncOthers();
    }

    // Sửa style ở một label -> mọi label khác trong scene theo y hệt.
    private void SyncOthers()
    {
        if (syncing) return;
        syncing = true;

        LootValueLabel[] all = FindObjectsByType<LootValueLabel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            LootValueLabel other = all[i];
            if (other == null || other == this) continue;

            other.labelColor = labelColor;
            other.fontSize = fontSize;
            other.labelWidth = labelWidth;
            other.labelScale = labelScale;
            other.labelOffset = labelOffset;
            other.aimDistance = aimDistance;
            other.glowColor = glowColor;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                other.EnsureLabel();
                other.ApplyStyle();
                EditorUtility.SetDirty(other);
            }
#endif
        }

        syncing = false;
    }
}
