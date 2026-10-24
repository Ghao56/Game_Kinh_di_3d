using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Gắn script này vào 1 object trong Canvas (Canvas nào cũng được).
// Runtime tự dựng THANH HOTBAR dưới giữa màn hình: các slot vuông bo góc,
// nền tối mờ, số phím góc trên, icon chấm màu, badge số lượng góc dưới,
// slot đang chọn sáng lên. Code tạo sprite nên không phụ thuộc asset ngoài.
// Khi phone mở -> cả thanh mờ dần về 0.
//
// Hotbar có ĐÚNG 4 ô. Dữ liệu lấy trực tiếp từ ThrowableSelector:
// - Ô trống: hiện Hand.png.
// - Ô có vật: hiện màu/nhãn của đúng HotbarEntry đang chiếm ô đó.
// Vật chiếm nhiều ô (loot slotCost > 1) hiện cùng một entry ở mọi ô nó chiếm.
public class SlotsHotbarUI : MonoBehaviour
{
    public ThrowableSelector selector;

    [Header("LOOK")]
    public int slotSize = 66;

    public int slotSpacing = 10;

    public int slotCount = RequiredSlotCount;

    [Header("ICONS")]
    [Tooltip("Icon cho ô trống. Để trống sẽ tự nạp Resources/UI/Hand hoặc sprite có sẵn trong scene.")]
    public Sprite emptySlotIcon;

    // Hotbar luôn có đúng 4 vị trí vật lý.
    public const int RequiredSlotCount = 4;

    private CanvasGroup group;
    private GameObject[] slotGos;
    private Image[] slotBgs;
    private Image[] iconDots;
    private Text[] keyTexts;
    private Text[] countTexts;
    private Text[] nameTexts;
    private Outline[] slotOutlines;

    // Số slot đang thực sự hiển thị (slot chưa nhặt/hết hàng bị ẩn)
    public int VisibleSlotCount()
    {
        if (slotGos == null)
            return 0;

        int n = 0;
        foreach (var go in slotGos)
            if (go != null && go.activeSelf)
                n++;
        return n;
    }

    private Sprite emptySlotSprite;

    void Start()
    {
        if (selector == null)
            selector = Object.FindFirstObjectByType<ThrowableSelector>();

        emptySlotSprite = emptySlotIcon != null
            ? emptySlotIcon
            : Resources.Load<Sprite>("UI/Hand");

        TatHotbarCu();

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = Object.FindFirstObjectByType<Canvas>();

        if (canvas == null || selector == null)
        {
            Debug.LogWarning($"{nameof(SlotsHotbarUI)}: thiếu Canvas hoặc ThrowableSelector.", this);
            enabled = false;
            return;
        }

        DungHotbar(canvas);

        // Scene bake sẵn dùng sprite Hand.png của project; nếu chưa cấu hình
        // emptySlotIcon và Resources cũng không có thì lấy sprite đang có.
        if (emptySlotSprite == null && iconDots != null)
        {
            foreach (var icon in iconDots)
            {
                if (icon != null && icon.sprite != null)
                {
                    emptySlotSprite = icon.sprite;
                    break;
                }
            }
        }
    }

    // Nhận khối SlotsHotbar bake sẵn trong scene: phủ lại mảng reference theo con "Slot N"
    void NhanLai(RectTransform root)
    {
        group = root.GetComponent<CanvasGroup>();

        var slots = new List<GameObject>();
        foreach (Transform con in root)
            if (con.name.StartsWith("Slot "))
                slots.Add(con.gameObject);

        slotCount = RequiredSlotCount;
        slotGos = new GameObject[slotCount];
        slotBgs = new Image[slotCount];
        iconDots = new Image[slotCount];
        keyTexts = new Text[slotCount];
        countTexts = new Text[slotCount];
        nameTexts = new Text[slotCount];
        slotOutlines = new Outline[slotCount];

        foreach (var go in slots)
        {
            if (!int.TryParse(go.name.Substring(5), out int so) || so < 1 || so > slotCount)
            {
                // Không cho scene cũ có slot thứ 5 lọt vào hotbar 4 ô.
                go.SetActive(false);
                continue;
            }

            int i = so - 1;
            slotGos[i] = go;
            slotBgs[i] = go.GetComponent<Image>();
            slotOutlines[i] = go.GetComponent<Outline>();

            Transform icon = go.transform.Find("Icon");
            Transform key = go.transform.Find("KeyText");
            Transform count = go.transform.Find("CountText");
            Transform label = go.transform.Find("Name");

            iconDots[i] = icon != null ? icon.GetComponent<Image>() : null;
            keyTexts[i] = key != null ? key.GetComponent<Text>() : null;
            countTexts[i] = count != null ? count.GetComponent<Text>() : null;
            nameTexts[i] = label != null ? label.GetComponent<Text>() : null;

            // Bảo đảm đúng số phím 1..4 dù scene cũ có ghi gì.
            if (keyTexts[i] != null)
                keyTexts[i].text = (i + 1).ToString();
        }
    }

    // Ô xám cũ ở góc phải (các Text dạng "2. Rượu: 1" + nền Image) tắt hẳn cho chỗ dư đất
    void TatHotbarCu()
    {
        var old = Object.FindFirstObjectByType<HotbarUI>();
        if (old == null)
            return;

        old.enabled = false;

        if (old.slotTexts != null)
            foreach (var t in old.slotTexts)
                if (t != null)
                    t.text = "";

        foreach (var img in old.GetComponentsInChildren<Image>(true))
            img.enabled = false;
    }

    void DungHotbar(Canvas canvas)
    {
        // Dựng dưới Canvas/Inventory (nếu có) để cả nhóm UI gom về một chỗ
        Transform cha = canvas.transform.Find("Inventory");
        if (cha == null)
            cha = canvas.transform;

        // Bake sẵn trong scene rồi: nhận lại, KHÔNG dựng lần nữa
        var co = cha.Find("SlotsHotbar");
        if (co != null && co.GetComponent<CanvasGroup>() != null)
        {
            NhanLai(co.GetComponent<RectTransform>());
            return;
        }

        slotCount = RequiredSlotCount;

        var root = new GameObject("SlotsHotbar", typeof(RectTransform), typeof(CanvasGroup), typeof(HorizontalLayoutGroup));
        root.transform.SetParent(cha, false);

        var rt = (RectTransform)root.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 22f);
        // Cao thêm 36px: strip dưới dành cho nhãn TÊN của từng slot
        rt.sizeDelta = new Vector2(slotCount * slotSize + (slotCount - 1) * slotSpacing, slotSize + 36f);

        group = root.GetComponent<CanvasGroup>();

        var layout = root.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = slotSpacing;
        layout.childAlignment = TextAnchor.UpperCenter; // slot dồn lên trên cùng, strip dưới để nhãn tên
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        slotGos = new GameObject[slotCount];
        slotBgs = new Image[slotCount];
        iconDots = new Image[slotCount];
        keyTexts = new Text[slotCount];
        countTexts = new Text[slotCount];
        nameTexts = new Text[slotCount];
        slotOutlines = new Outline[slotCount];

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        for (int i = 0; i < slotCount; i++)
        {
            var go = new GameObject($"Slot {i + 1}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(root.transform, false);

            var slt = (RectTransform)go.transform;
            slt.sizeDelta = new Vector2(slotSize, slotSize);

            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = slotSize;
            le.preferredHeight = slotSize;

            var bg = go.GetComponent<Image>();
            bg.sprite = RoundedSpriteUtil.Get();
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.06f, 0.07f, 0.10f, 0.95f); // tối đậm, nổi rõ trên nền 3D
            bg.raycastTarget = false;

            var ol = go.AddComponent<Outline>();
            ol.effectColor = new Color(0.35f, 0.5f, 0.7f, 0.35f); // viền mảnh mặc định
            ol.effectDistance = new Vector2(1.5f, -1.5f);

            slotGos[i] = go;
            slotBgs[i] = bg;
            slotOutlines[i] = ol;

            // Số phím 1..4 - góc trên trái
            keyTexts[i] = TaoText(go.transform, font, new Vector2(-2f, -6f), new Vector2(0.5f, 0.5f), TextAnchor.UpperLeft);
            keyTexts[i].fontSize = 18;
            keyTexts[i].text = (i + 1).ToString();
            keyTexts[i].gameObject.name = "KeyText";

            // Icon chấm màu theo loại vật phẩm - giữa slot
            iconDots[i] = TaoDot(go.transform);

            // Badge số lượng - góc dưới phải
            countTexts[i] = TaoText(go.transform, font, new Vector2(8f, 8f), new Vector2(0.5f, 0.5f), TextAnchor.LowerRight);
            countTexts[i].fontSize = 20;
            countTexts[i].gameObject.name = "CountText";

            // ===== FIX: nhãn TÊN cho từng slot, con của SLOT (không phải của
            // HorizontalLayoutGroup) + ignoreLayout để layout không xếp nó thành slot thứ 6.
            // Chỉ slot đang chọn mới hiện tên.
            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            nameGo.transform.SetParent(go.transform, false);

            var nle = nameGo.GetComponent<LayoutElement>();
            nle.ignoreLayout = true;

            var nrt = (RectTransform)nameGo.transform;
            nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 0f); // đáy giữa slot
            nrt.pivot = new Vector2(0.5f, 1f); // môi trên của nhãn nằm dưới slot một đoạn
            nrt.anchoredPosition = new Vector2(0f, -14f);
            nrt.sizeDelta = new Vector2(160f, 22f);

            nameTexts[i] = nameGo.GetComponent<Text>();
            nameTexts[i].font = font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
            nameTexts[i].fontSize = 20;
            nameTexts[i].fontStyle = FontStyle.Bold;
            nameTexts[i].alignment = TextAnchor.UpperCenter;
            nameTexts[i].color = new Color(0.85f, 0.94f, 1f);
            nameTexts[i].raycastTarget = false;
            nameTexts[i].supportRichText = true;
            nameTexts[i].horizontalOverflow = HorizontalWrapMode.Overflow; // tên dài không bị cắt
            nameTexts[i].verticalOverflow = VerticalWrapMode.Overflow;
            nameTexts[i].text = "";

            var nameOutline = nameGo.AddComponent<Outline>();
            nameOutline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            nameOutline.effectDistance = new Vector2(2f, 0f);
        }
    }

    Text TaoText(Transform parent, Font font, Vector2 pos, Vector2 anchor, TextAnchor align)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.sizeDelta = new Vector2(slotSize - 12f, slotSize - 12f);
        rt.anchoredPosition = pos;

        var t = go.GetComponent<Text>();
        t.font = font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = 15;
        t.alignment = align;
        t.raycastTarget = false;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        return t;
    }

    Image TaoDot(Transform parent)
    {
        var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(30f, 30f);
        rt.anchoredPosition = new Vector2(0f, 4f);

        var img = go.GetComponent<Image>();
        img.sprite = RoundedSpriteUtil.Circle();
        img.raycastTarget = false;

        return img;
    }

    static Color MauIcon(string itemId)
    {
        switch (PlayerInventory.CanonicalId(itemId))
        {
            case "Salt": return new Color(0.92f, 0.95f, 1f); // trắng muối
            case "Alcohol": return new Color(0.95f, 0.35f, 0.30f); // đỏ rượu
            case "loot_clock": return new Color(0.95f, 0.78f, 0.35f); // vàng đồng hồ
            case "loot_vase": return new Color(0.35f, 0.80f, 0.72f); // xanh men gốm
            case "loot_jewelry_box": return new Color(0.72f, 0.50f, 0.95f); // tím hộp trang sức
            default: return new Color(0.7f, 0.7f, 0.75f);
        }
    }

    void Update()
    {
        if (group == null)
            return;

        // Phone mở -> làm mờ hotbar đi, tránh đè UI phone
        bool phoneOpen = false;
        group.alpha = Mathf.MoveTowards(group.alpha, phoneOpen ? 0f : 1f, Time.unscaledDeltaTime * 6f);
        group.blocksRaycasts = false;
        group.interactable = false;

        if (slotBgs == null || selector == null)
            return;

        HotbarEntry selectedEntry = selector.SelectedEntry;

        for (int i = 0; i < slotCount; i++)
        {
            if (slotGos[i] != null)
                slotGos[i].SetActive(true);

            HotbarEntry entry = selector.GetEntryAt(i);

            if (entry == null)
            {
                // Ô trống: luôn hiện Hand.png, không số lượng, không tên.
                if (iconDots[i] != null)
                {
                    if (emptySlotSprite != null)
                        iconDots[i].sprite = emptySlotSprite;
                    iconDots[i].color = new Color(1f, 1f, 1f, 0.6f);
                    iconDots[i].rectTransform.sizeDelta = new Vector2(40f, 40f);
                    iconDots[i].rectTransform.localScale = Vector3.one;
                }
                if (countTexts[i] != null)
                    countTexts[i].text = "";
                if (nameTexts[i] != null)
                    nameTexts[i].text = "";
                if (slotBgs[i] != null)
                    slotBgs[i].color = new Color(0.06f, 0.07f, 0.10f, 0.95f);
                if (slotOutlines[i] != null)
                    slotOutlines[i].effectColor = new Color(0.35f, 0.5f, 0.7f, 0.35f);
                continue;
            }

            bool selected = ReferenceEquals(selectedEntry, entry);

            if (iconDots[i] != null)
            {
                iconDots[i].sprite = RoundedSpriteUtil.Circle();
                iconDots[i].color = MauIcon(entry.ItemId);
                iconDots[i].rectTransform.sizeDelta = new Vector2(30f, 30f);
                iconDots[i].rectTransform.localScale = selected ? Vector3.one * 1.25f : Vector3.one;
            }

            // Nền đậm alpha 0.95 + viền sáng hơn hẳn khi đang chọn.
            // Vật nhiều ô: mọi ô của nó cùng sáng lên.
            if (slotBgs[i] != null)
            {
                slotBgs[i].color = selected
                    ? new Color(0.13f, 0.23f, 0.35f, 1f) // slot đang chọn: xanh đậm sáng
                    : new Color(0.06f, 0.07f, 0.10f, 0.95f);
            }

            if (slotOutlines[i] != null)
            {
                slotOutlines[i].effectColor = selected
                    ? new Color(0.55f, 0.8f, 1f, 0.95f)
                    : new Color(0.35f, 0.5f, 0.7f, 0.35f);
            }

            // Badge số lượng chỉ đặt ở ô bắt đầu để vật nhiều ô không hiện lặp.
            if (countTexts[i] != null)
            {
                countTexts[i].text = i == entry.StartSlot && entry.Count > 1
                    ? $"<color=#8ed2ff>×{entry.Count}</color>"
                    : "";
            }

            // Nhãn tên chỉ hiện ở ô người chơi vừa bấm (hoặc ô bắt đầu nếu
            // đang chọn bằng code) và chỉ cho mục đang chọn.
            if (nameTexts[i] != null)
            {
                bool nameSlot = selected &&
                    (i == selector.CurrentIndex || selector.CurrentIndex < entry.StartSlot || selector.CurrentIndex > entry.EndSlot);
                nameTexts[i].text = nameSlot ? entry.Name : "";
            }
        }
    }
}
