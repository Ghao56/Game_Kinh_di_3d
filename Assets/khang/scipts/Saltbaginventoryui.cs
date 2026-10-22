using UnityEngine;
using UnityEngine.UI;
using System.Text;
using System.Collections.Generic;

// Gắn vào 1 object Text trong Canvas. Runtime bọc thêm nền bo góc "BagPanel"
// với tiêu đề "TÚI ĐỒ" + danh sách "<tên> ×số lượng" (1 dòng/vật), dựa kếp
// cùng Canvas với SlotsHotbarUI.
public class InventoryUI : MonoBehaviour
{
    public PlayerInventory inventory;

    public ThrowableSelector selector; // tra tên đẹp hơn nếu ngoài bảng mặc định

    [Header("VỊ TRÍ / KÍCH THƯỚC PANEL")]
    public float beRongPanel = 320f;
    public float caoToiThieu = 170f;
    public Vector2 viTriDuPhong = new Vector2(-611f, 4f); // vị trí mặc định đã bake trong scene
    [Tooltip("Khoảng cách giữa mép phải panel và mép trái khối slot hotbar")]
    public float khoangCachHotbar = 16f;
    [Tooltip("Chiều cao nền cũ của hotbar dưới đáy (strip tên trong SlotsHotbarUI)")]
    public float stripTen = 36f;
    public float doCaoNenHotbar = 22f;
    [Tooltip("LềText trong panel, 4 phía")]
    public float leText = 16f;

    [Header("HIỆU ỨNG")]
    [Range(0.1f, 20f)] public float tocDoTruot = 12f; // trượt theo hotbar
    [Range(0.1f, 20f)] public float tocDoMo = 6f; // mờ dần khi mở phone

    [Header("MÀU")]
    public Color mauNen = new Color(0.07f, 0.09f, 0.14f, 0.95f);
    public Color mauVien = new Color(0.45f, 0.65f, 0.95f, 0.4f);

    private Text label;

    private RectTransform panelRect;

    private SlotsHotbarUI khungSlot; // đặt panel bên trái hotbar theo số slot thật

    private CanvasGroup group;

    private GTA_PhoneController gtaPhone;

    private PhoneUIManager phoneManager;

    private static readonly Dictionary<string, string> tenMacDinh = new()
    {
        { "Salt", "Muối" },
        { "Alcohol", "Chai rượu" },
        { "Wine", "Chai rượu" },
    };

    void Start()
    {
        label = GetComponent<Text>();

        // Tìm khung hotbar TRƯỚC để BagPanel biết canvas nào để núp vào
        if (khungSlot == null)
            khungSlot = Object.FindFirstObjectByType<SlotsHotbarUI>();

        TaoNenPanel();
        LamDepChu();

        if (inventory == null)
            inventory = Object.FindFirstObjectByType<PlayerInventory>();

        if (selector == null)
            selector = Object.FindFirstObjectByType<ThrowableSelector>();

        if (gtaPhone == null)
            gtaPhone = Object.FindFirstObjectByType<GTA_PhoneController>();

        if (phoneManager == null)
            phoneManager = Object.FindFirstObjectByType<PhoneUIManager>();

        XayBangTen();

        if (inventory != null)
            inventory.OnInventoryChanged += Refresh;

        Refresh();
    }

    void OnDestroy()
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= Refresh;
    }

    void TaoNenPanel()
    {
        // Gỡ auto-size của Text gốc trước mọi lối thoát: nó ghi đè sizeDelta
        // mỗi frame, đánh nhau với offsetMin/offsetMax đặt dưới.
        foreach (var fitter in GetComponents<ContentSizeFitter>())
            Destroy(fitter);
        foreach (var le in GetComponents<LayoutElement>())
            Destroy(le);

        if (transform.parent != null && transform.parent.name == "BagPanel")
        {
            // BagPanel bake sẵn trong scene: dùng chính nó làm panel, ép mọi Image con phủ FULL panel
            panelRect = transform.parent as RectTransform;
            group = transform.parent.GetComponent<CanvasGroup>();

            foreach (var imgCon in transform.parent.GetComponentsInChildren<Image>(true))
                if (imgCon.transform != transform.parent)
                {
                    var irt = (RectTransform)imgCon.transform;
                    irt.anchorMin = Vector2.zero;
                    irt.anchorMax = Vector2.one;
                    irt.offsetMin = Vector2.zero;
                    irt.offsetMax = Vector2.zero;
                }
            return;
        }

        var rt = (RectTransform)transform;

        var panelGO = new GameObject("BagPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));

        // Cùng canvas hotbar ("Canvas/Inventory") để 2 khối luôn cùng scale/tọa độ
        var canvas = khungSlot != null
            ? khungSlot.GetComponentInParent<Canvas>()
            : GetComponentInParent<Canvas>();
        panelGO.transform.SetParent(canvas != null ? canvas.transform : null, false);

        var prt = (RectTransform)panelGO.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f);
        prt.pivot = new Vector2(1f, 0f); // mép PHẢI panel là điểm neo, lòi sang trái hotbar
        prt.anchoredPosition = viTriDuPhong;
        prt.localScale = Vector3.one;
        prt.sizeDelta = new Vector2(beRongPanel, caoToiThieu);

        panelRect = prt;
        group = panelGO.GetComponent<CanvasGroup>();

        var img = panelGO.GetComponent<Image>();
        img.sprite = RoundedSpriteUtil.Get();
        img.type = Image.Type.Sliced;
        img.color = mauNen;
        img.raycastTarget = false;

        var border = panelGO.AddComponent<Outline>();
        border.effectColor = mauVien;
        border.effectDistance = new Vector2(2f, -2f);

        rt.SetParent(panelGO.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(leText, leText);
        rt.offsetMax = new Vector2(-leText, -leText);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    void LamDepChu()
    {
        label.supportRichText = true;
        label.fontSize = 22;
        label.lineSpacing = 1.25f;
        label.alignment = TextAnchor.UpperLeft;
        label.color = Color.white;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Truncate; // chữ không văng ra ngoài khung
        label.text = "";

        var shadow = label.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);

        var outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
        outline.effectDistance = new Vector2(2f, 0f);
    }

    void XayBangTen()
    {
        if (selector != null)
            foreach (var m in selector.throwables)
                if (m is IThrowable t && !string.IsNullOrEmpty(t.ItemId))
                    tenMacDinh[PlayerInventory.CanonicalId(t.ItemId)] = t.DisplayName;
    }

    string Ten(string id)
    {
        string key = PlayerInventory.CanonicalId(id);
        return tenMacDinh.TryGetValue(key, out var ten) ? ten : key;
    }

    void Refresh()
    {
        if (label == null || inventory == null)
            return;

        int lines = 3; // tiêu đề + viền ngang + dòng hướng dẫn
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b><size=24><color=#4fc3ff>TÚI ĐỒ</color></size></b>");
        sb.AppendLine("<color=#5a7ba8>――――――――――――</color>");

        bool co = false;

        foreach (var pair in inventory.GetAllItems())
        {
            if (pair.Value > 0)
            {
                sb.AppendLine($"• <b>{Ten(pair.Key)}</b>   <color=#b8c4da>×{pair.Value}</color>");
                co = true;
                lines++;
            }
        }

        if (!co)
        {
            sb.AppendLine("<color=#8b95aa>Trống</color>");
            lines++;
        }

        string phimNem = selector != null ? selector.throwKey.ToString() : "F";
        sb.Append($"<color=#8a97b0><size=16>[1-5] đổi đồ • [{phimNem}] ném</size></color>");

        label.text = sb.ToString();

        if (panelRect != null)
            panelRect.sizeDelta = new Vector2(beRongPanel, Mathf.Max(caoToiThieu, lines * 33f + 52f));
    }

    void Update()
    {
        if (group == null)
            return;

        // Phone mở -> mờ khung túi đi, nhường chỗ phone
        bool phoneOpen = (gtaPhone != null && gtaPhone.IsOpen) || (phoneManager != null && phoneManager.IsPhoneOpen);
        group.alpha = Mathf.MoveTowards(group.alpha, phoneOpen ? 0f : 1f, Time.unscaledDeltaTime * tocDoMo);
    }
}
