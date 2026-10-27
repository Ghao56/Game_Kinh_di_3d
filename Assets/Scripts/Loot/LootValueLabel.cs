using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// Món loot: người chơi ngắm vào thì bật glow outline (màu vàng) như interactable
/// và hiện giá trị trên đầu vật (chữ xanh dương, luôn vẽ đè lên mọi vật).
/// Cấu trúc: 1 GameObject "Label" (con của món loot) chứa 3 label con:
///   - "Name"   : tên món (nếu bật showName)
///   - "Value"  : giá tiền "xM VND" (luôn hiện)
///   - "Weight" : số ô trong túi slotCost (nếu bật showWeight)
/// Child "Label" được tạo & hiện ngay trong edit để chỉnh; khi chạy game thì ẩn,
/// ngắm vào mới hiện. Sửa style ở MỘT món thì mọi LootValueLabel trong scene đổi theo.
[ExecuteAlways]
public class LootValueLabel : MonoBehaviour
{
    private const string LabelRootName = "Label";
    private const string NameLabelName = "Name";
    private const string ValueLabelName = "Value";
    private const string WeightLabelName = "Weight";
    private const string OverlayMaterialPath = "LootLabelOverlay";
    private const float weightFontScale = 0.6f; // chữ Weight nhỏ hơn ~60%

    [Header("Giá tiền (áp cho tất cả label trong scene)")]
    [Tooltip("Màu dòng chủ đạo (= màu giá, band 0 < M < 10M). Bỏ value band màu thường. Đặt để giữ tương thích cũ.")]
    [SerializeField] private Color labelColor = new Color(0.25f, 1f, 0.3f, 1f);
    [Tooltip("Đổi màu giá theo độ hiếm (M = valueInMillions): <5 trắng, 5-9 xanh dương, 10-19 tím, ≥20 vàng.")]
    [SerializeField] private bool useRarityValueColor = true;
    [SerializeField] private Color valueCommonColor = new Color(0.949f, 0.949f, 0.949f);  // F2F2F2 trắng
    [SerializeField] private Color valueRareColor = new Color(0.231f, 0.510f, 0.965f);   // 3B82F6 xanh dương
    [SerializeField] private Color valueLegendaryColor = new Color(0.545f, 0.361f, 0.965f); // 8B5CF6 tím
    [SerializeField] private Color valueEpicColor = new Color(0.918f, 0.702f, 0.031f);   // EAB308 vàng (cực hiếm)
    [SerializeField] private Color nameColor = new Color(0.949f, 0.949f, 0.949f, 1f);     // F2F2F2 trắng
    [SerializeField] private Color weightColor = new Color(0.71f, 0.71f, 0.71f, 1f);      // B5B5B5 xám
    [SerializeField] private float fontSize = 42f;
    [Tooltip("Độ rộng khung chữ (đơn vị world trước khi nhân Scale).")]
    [SerializeField] private float labelWidth = 22f;
    [Tooltip("Scale tổng của nhãn. 0.1 ≈ chữ cao ~0.4m; tăng/giảm tùy ý.")]
    [SerializeField] private float labelScale = 0.1f;
    [Tooltip("Vị trí chữ so với vật (local): 0,0,0 = ngay giữa vật.")]
    [SerializeField] private Vector3 labelOffset = Vector3.zero;
    [Tooltip("Khoảng cách dòng (đơn vị world trước khi nhân Scale) giữa Value và Name/Weight.")]
    [SerializeField] private float lineGap = 4.5f;

    [Header("Hiển thị")]
    [SerializeField] private bool showName = true;
    [Tooltip("Số ô chiếm trong túi (slotCost) hiển thị dưới dạng Weight.")]
    [SerializeField] private bool showWeight = true;
    [SerializeField] private float aimDistance = 6f;

    [Header("Glow")]
    [SerializeField] private Color glowColor = new Color(1f, 0.8f, 0.15f, 1f);

    private LootItem loot;
    private InteractableGlow glow;
    private Transform labelRoot;
    private TextMeshPro nameLabel;
    private TextMeshPro valueLabel;
    private TextMeshPro weightLabel;
    private Camera cam;
    private bool shown;
    private bool styleDirty;
    private static Material overlayMaterial;
    private static bool overlayLoaded;
    private static bool syncing;

    private void OnEnable()
    {
        EnsureLabel();
        ApplyStyle();

        if (!Application.isPlaying) return;
        if (labelRoot == null) return;

        labelRoot.gameObject.SetActive(false);

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
            labelRoot.gameObject.SetActive(aimed);
            glow.SetHighlighted(aimed);
        }

        if (aimed) labelRoot.rotation = cam.transform.rotation;
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
        if (labelRoot != null && nameLabel != null && valueLabel != null && weightLabel != null)
            return;

        labelRoot = EnsureChild(LabelRootName, transform);
        nameLabel = EnsureChildLabel(NameLabelName, labelRoot);
        valueLabel = EnsureChildLabel(ValueLabelName, labelRoot);
        weightLabel = EnsureChildLabel(WeightLabelName, labelRoot);

        loot = GetComponent<LootItem>();
        foreach (TextMeshPro tmp in new[] { nameLabel, valueLabel, weightLabel })
        {
            if (tmp.font == null) tmp.font = TMP_Settings.defaultFontAsset;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.richText = false;
            // Không-wrap: chữ luôn 1 dòng, độ rộng do FitWidth() chỉnh theo nội dung.
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            AssignOverlayMaterial(tmp);
        }

        UpdateText();

#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    // Tạo/tra GameObject con của root, thêm TextMeshPro nếu chưa có.
    private Transform EnsureChild(string childName, Transform parent)
    {
        Transform t = parent.Find(childName);
        if (t == null)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            t = go.transform;
        }
        return t;
    }

    private TextMeshPro EnsureChildLabel(string childName, Transform parent)
    {
        Transform t = EnsureChild(childName, parent);
        var tmp = t.GetComponent<TextMeshPro>();
        if (tmp == null) tmp = t.gameObject.AddComponent<TextMeshPro>();
        return tmp;
    }

    // Nội dung của banner: Name (nếu bật), giá tiền Step (Value, luôn) và Weight = slotCost (nếu bật).
    private void UpdateText()
    {
        if (nameLabel == null || valueLabel == null || weightLabel == null) return;
        if (loot == null) loot = GetComponent<LootItem>();

        string name = (loot != null && !string.IsNullOrEmpty(loot.itemName))
            ? loot.itemName
            : "Unknown";
        int value = loot != null ? loot.valueInMillions : 0;
        int weight = loot != null ? loot.slotCost : 1;

        nameLabel.text = name;
        valueLabel.text = value + "M VND";
        weightLabel.text = "Weight: " + weight;

        // Sau khi đổi chữ -> tự chỉnh độ rộng khung theo chữ để luôn 1 dòng.
        FitWidth(nameLabel);
        FitWidth(valueLabel);
        FitWidth(weightLabel);
    }

    // Canh độ rộng khung của label vừa khít nội dung (chỉ 1 dòng, không wrap).
    private void FitWidth(TextMeshPro tmp)
    {
        if (tmp == null) return;

        tmp.ForceMeshUpdate();
        tmp.rectTransform.sizeDelta =
            new Vector2(Mathf.Max(1f, tmp.preferredWidth + 2f), Mathf.Max(1f, labelWidth));
    }

    // Chữ vẽ đè lên mọi vật + viền: material overlay TMP (ZTest Always, OUTLINE_ON).
    private void AssignOverlayMaterial(TextMeshPro tmp)
    {
        if (!overlayLoaded)
        {
            overlayMaterial = Resources.Load<Material>(OverlayMaterialPath);
            overlayLoaded = true;
        }

        if (overlayMaterial != null) tmp.fontSharedMaterial = overlayMaterial;
    }

    private void ApplyStyle()
    {
        if (labelRoot == null || nameLabel == null || valueLabel == null || weightLabel == null) return;

        labelRoot.localPosition = labelOffset;
        labelRoot.localScale = Vector3.one * Mathf.Max(0.01f, labelScale);

        // Value: dòng giữa. Name: trên. Weight: dưới.
        valueLabel.transform.localPosition = Vector3.zero;
        nameLabel.transform.localPosition = new Vector3(0f, lineGap, 0f);
        weightLabel.transform.localPosition = new Vector3(0f, -lineGap, 0f);

        // Màu riêng từng dòng: giá theo hiếm, tên trắng, weight xám.
        foreach (TextMeshPro tmp in new[] { nameLabel, valueLabel, weightLabel })
        {
            tmp.color = tmp == weightLabel ? weightColor
                      : tmp == nameLabel ? nameColor
                      : ValueColorByRarity();
            // Weight nhỏ hơn ~60% so với các dòng còn lại.
            tmp.fontSize = tmp == weightLabel ? fontSize * weightFontScale : fontSize;
        }

        // Bật/tắt từng dòng theo lựa chọn (khi chạy game cả cụm lại được ẩn cho đến khi ngắm).
        nameLabel.gameObject.SetActive(showName);
        weightLabel.gameObject.SetActive(showWeight);

        // SizeDelta bị fontSize thay đổi ảnh hưởng -> fit lại chiều rộng theo chữ.
        FitWidth(nameLabel);
        FitWidth(valueLabel);
        FitWidth(weightLabel);
    }

    // Màu dòng giá: bật useRarityValueColor thì phân band theo valueInMillions,
    // không thì dùng labelColor (tương thích setting cũ).
    private Color ValueColorByRarity()
    {
        if (!useRarityValueColor) return labelColor;
        if (loot == null) loot = GetComponent<LootItem>();
        int value = loot != null ? loot.valueInMillions : 0;

        if (value >= 20) return valueEpicColor;      // cực hiếm: vàng
        if (value >= 10) return valueLegendaryColor; // quý: tím
        if (value >= 5) return valueRareColor;       // hiếm: xanh dương
        return valueCommonColor;                     // thường: trắng
    }

    private void OnValidate()
    {
        labelScale = Mathf.Max(0.01f, labelScale);
        fontSize = Mathf.Max(1f, fontSize);
        labelWidth = Mathf.Max(1f, labelWidth);
        lineGap = Mathf.Max(0.1f, lineGap);
        aimDistance = Mathf.Max(1f, aimDistance);

        ScheduleApply();
    }

    // Unity cấm đụng RectTransform (set_sizeDelta → SendMessage) ngay trong OnValidate,
    // nên dồn EnsureLabel/UpdateText/ApplyStyle/SyncOthers sang cuối frame.
    private void ScheduleApply()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // Edit mode: chạy 1 lần sau khi OnValidate xong hẳn.
            EditorApplication.delayCall -= ApplyScheduled;
            EditorApplication.delayCall += ApplyScheduled;
            return;
        }
#endif
        // Play mode: xử lý ở Update kế tiếp.
        styleDirty = true;
    }

    private void ApplyScheduled()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall -= ApplyScheduled;
        if (this == null) return; // object đã bị xóa trước khi delayCall chạy
#endif
        styleDirty = false;

        EnsureLabel();
        UpdateText();
        ApplyStyle();
        SyncOthers();
    }

    private void Update()
    {
        if (styleDirty) ApplyScheduled();
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
            other.useRarityValueColor = useRarityValueColor;
            other.valueCommonColor = valueCommonColor;
            other.valueRareColor = valueRareColor;
            other.valueLegendaryColor = valueLegendaryColor;
            other.valueEpicColor = valueEpicColor;
            other.nameColor = nameColor;
            other.weightColor = weightColor;
            other.fontSize = fontSize;
            other.labelWidth = labelWidth;
            other.labelScale = labelScale;
            other.labelOffset = labelOffset;
            other.lineGap = lineGap;
            other.aimDistance = aimDistance;
            other.glowColor = glowColor;
            other.showName = showName;
            other.showWeight = showWeight;

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
