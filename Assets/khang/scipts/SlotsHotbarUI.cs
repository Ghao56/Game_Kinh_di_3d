using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Gắn script này vào 1 object trong Canvas (Canvas nào cũng được).
// Runtime tự dựng THANH HOTBAR dưới giữa màn hình: các slot vuông bo góc,
// nền tối mờ, số phím góc trên, icon chấm màu, badge số lượng góc dưới,
// slot đang chọn sáng lên. Code tạo sprite nên không phụ thuộc asset ngoài.
// Khi phone mở -> cả thanh mờ dần về 0.
// Slot chỉ hiện khi vật phẩm ĐÃ NHẬT; giữ nguyên logic selector có sẵn.
public class SlotsHotbarUI : MonoBehaviour
{
    public ThrowableSelector selector;

    public GTA_PhoneController gtaPhone;

    public PhoneUIManager phoneManager;

    [Header("LOOK")]
    public int slotSize = 66;

    public int slotSpacing = 10;

    public int slotCount = 3; // tay: 3 slot chuyên đồ lớn / muối / rượu

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

    void Start()
    {
        if (selector == null)
            selector = Object.FindFirstObjectByType<ThrowableSelector>();

        if (gtaPhone == null)
            gtaPhone = Object.FindFirstObjectByType<GTA_PhoneController>();

        if (phoneManager == null)
            phoneManager = Object.FindFirstObjectByType<PhoneUIManager>();

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
    }

    // Nhận khối SlotsHotbar bake sẵn trong scene: phủ lại mảng reference theo con "Slot N"
    void NhanLai(RectTransform root)
    {
        group = root.GetComponent<CanvasGroup>();

        var slots = new List<GameObject>();
        foreach (Transform con in root)
            if (con.name.StartsWith("Slot "))
                slots.Add(con.gameObject);

        slotCount = slots.Count;
        slotGos = new GameObject[slotCount];
        slotBgs = new Image[slotCount];
        iconDots = new Image[slotCount];
        keyTexts = new Text[slotCount];
        countTexts = new Text[slotCount];
        nameTexts = new Text[slotCount];
        slotOutlines = new Outline[slotCount];

        foreach (var go in slots)
        {
            int i = int.Parse(go.name.Substring(5)) - 1;
            if (i >= slotCount)
                continue;

            slotGos[i] = go;
            slotBgs[i] = go.GetComponent<Image>();
            slotOutlines[i] = go.GetComponent<Outline>();
            iconDots[i] = go.transform.Find("Icon").GetComponent<Image>();
            keyTexts[i] = go.transform.Find("KeyText").GetComponent<Text>();
            countTexts[i] = go.transform.Find("CountText").GetComponent<Text>();
            nameTexts[i] = go.transform.Find("Name").GetComponent<Text>();
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

            // Số phím 1..5 - góc trên trái
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
            default: return new Color(0.7f, 0.7f, 0.75f);
        }
    }

    void Update()
    {
        if (group == null)
            return;

        // Phone mở -> làm mờ hotbar đi, tránh đè UI phone
        bool phoneOpen = (gtaPhone != null && gtaPhone.IsOpen) || (phoneManager != null && phoneManager.IsPhoneOpen);
        group.alpha = Mathf.MoveTowards(group.alpha, phoneOpen ? 0f : 1f, Time.unscaledDeltaTime * 6f);
        group.blocksRaycasts = false;
        group.interactable = false;

        if (slotBgs == null || selector == null)
            return;

        for (int i = 0; i < slotCount; i++)
        {
            IThrowable item = selector.GetItemAt(i);

            if (slotGos[i] != null)
                slotGos[i].SetActive(item != null && item.GetCount() > 0);

            if (item == null || item.GetCount() <= 0)
                continue;

            bool selected = i == selector.CurrentIndex;

            iconDots[i].color = MauIcon(item.ItemId);

            iconDots[i].rectTransform.localScale = selected ? Vector3.one * 1.25f : Vector3.one;

            // Nền đậm alpha 0.95 + viền sáng hơn hẳn khi đang chọn
            slotBgs[i].color = selected
                ? new Color(0.13f, 0.23f, 0.35f, 1f) // slot đang chọn: xanh đậm sáng
                : new Color(0.06f, 0.07f, 0.10f, 0.95f);

            if (slotOutlines[i] != null)
                slotOutlines[i].effectColor = selected
                    ? new Color(0.55f, 0.8f, 1f, 0.95f)
                    : new Color(0.35f, 0.5f, 0.7f, 0.35f);

            countTexts[i].text = item.GetCount() > 1 ? $"<color=#8ed2ff>×{item.GetCount()}</color>" : "";

            // Chỉ nhãn của slot đang chọn hiện tên
            if (nameTexts[i] != null)
                nameTexts[i].text = selected ? item.DisplayName : "";
        }
    }
}
