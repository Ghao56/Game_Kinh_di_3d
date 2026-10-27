using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Restyle panel Settings theo phong cách Dark Survival Horror v3:
/// - BỎ ghots.png; nền là video menu + một lớp phủ tối nhẹ full-screen
/// - Cột đen bán trong suốt phía sau cài đặt -> chữ dễ đọc
/// - Trục căn trái đồng nhất, nhãn chuẩn (MASTER VOLUME / SOUND EFFECTS...)
/// - Slider đỏ trầm #B8322A + hiển thị % bên phải
/// - Gắn vào root của SettingsPanel.prefab, không đụng wiring của SettingsMenu.
/// </summary>
[DefaultExecutionOrder(-50)]
public class SettingsUIRestyler : MonoBehaviour
{
    private static readonly Color TextMain    = new Color(0.90f, 0.88f, 0.86f, 1f);
    private static readonly Color TextSub     = new Color(0.66f, 0.64f, 0.62f, 1f);
    private static readonly Color AccentRed   = new Color(0.722f, 0.196f, 0.165f, 1f); // #B8322A
    private static readonly Color BgTintLight = new Color(0.62f, 0.62f, 0.65f, 0.42f); // dim nhẹ
    private static readonly Color ColumnDark  = new Color(0.03f, 0.03f, 0.04f, 0.84f); // panel trung tâm
    private static readonly Color ControlDark = new Color(0.13f, 0.13f, 0.15f, 1f);
    private static readonly Color TrackDark   = new Color(0.30f, 0.29f, 0.32f, 1f);
    private static readonly Color HandleLight = new Color(0.90f, 0.88f, 0.86f, 1f);

    private void Awake()
    {
        Canvas canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null) return;

        Transform root = canvas.transform;

        StyleBackground(root);
        MakeVeilPanel(root);
        MakeColumnPanel(root);
        StyleSliders(root);
        StyleDropdowns(root);
        StyleToggle(root);
        StyleBackButton(root);
        CreateHeaders(root);
    }

    // LoadSettings của SettingsMenu chạy ở Start (SetValueWithoutNotify không bắn event),
    // nên % ban đầu chỉ đúng sau khi Start xong -> refresh sau 1 frame.
    private IEnumerator Start()
    {
        yield return null;
        RefreshAllValues();
    }

    private static void RefreshAllValues()
    {
        Canvas canvas = null;
        var sm = UnityEngine.Object.FindFirstObjectByType<SettingsUIRestyler>(UnityEngine.FindObjectsInactive.Include);
        if (sm != null) canvas = sm.GetComponentInChildren<Canvas>(true);
        if (canvas == null) return;

        foreach (var s in canvas.GetComponentsInChildren<Slider>(true))
        {
            Transform valueT = s.transform.Find("ValueLabel");
            if (valueT == null) continue;
            var v = valueT.GetComponent<TMP_Text>();
            if (v != null) UpdateValueText(s, v);
        }
    }

    private static string LabelOf(string controlName)
    {
        string n = controlName.Trim();
        if (n.StartsWith("Master")) return "MASTER VOLUME";
        if (n.StartsWith("Music")) return "MUSIC VOLUME";
        if (n.StartsWith("Sfx")) return "SOUND EFFECTS";
        if (n.StartsWith("Sensitivity")) return "MOUSE SENSITIVITY";
        if (n.StartsWith("Back")) return "BACK";
        return n.ToUpperInvariant();
    }

    private static float YOf(string controlName)
    {
        string n = controlName.Trim();
        if (n.StartsWith("SETTINGS")) return 420f;
        if (n.StartsWith("AUDIO")) return 320f;
        if (n.StartsWith("Master")) return 235f;
        if (n.StartsWith("Music")) return 160f;
        if (n.StartsWith("Sfx")) return 85f;
        if (n.StartsWith("DISPLAY")) return -8f;
        if (n.StartsWith("Fullscreen")) return -105f;
        if (n.StartsWith("Resolution")) return -210f;
        if (n.StartsWith("Quality")) return -315f;
        if (n.StartsWith("Sensitivity")) return -415f;
        if (n.StartsWith("Back")) return -485f;
        return 0f;
    }

    // ───────────────────────── Nền + panel giữa ─────────────────────────

    private void StyleBackground(Transform root)
    {
        // Bỏ hẳn ghots.png theo yêu cầu: video menu là nền, không cần image nữa
        foreach (Transform child in root)
        {
            if (child != null && child.name != null && child.name.Trim() == "SettingsPanel")
            {
                child.gameObject.SetActive(false);
                return;
            }
        }
    }

    private void MakeVeilPanel(Transform root)
    {
        // Lớp phủ tối nhẹ phủ toàn màn: giảm độ sáng video, không che không khí
        if (root.Find("VeilPanel") != null) return;

        GameObject go = new GameObject("VeilPanel");
        go.transform.SetParent(root, false);
        go.transform.SetSiblingIndex(0); // đứng trước ColumnPanel, sau cancel order

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.42f);
        img.raycastTarget = false;
    }

    private void MakeColumnPanel(Transform root)
    {
        // Panel đen bán trong suốt sau cột cài đặt -> chữ dễ đọc
        if (root.Find("ColumnPanel") != null) return;

        GameObject go = new GameObject("ColumnPanel");
        go.transform.SetParent(root, false);
        go.transform.SetSiblingIndex(1); // trên VeilPanel, dưới mọi control

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(760f, 1020f);
        rt.anchoredPosition = new Vector2(0f, -25f);

        Image img = go.AddComponent<Image>();
        img.color = ColumnDark;
        img.raycastTarget = false;
    }

    // ───────────────────────── Sliders ─────────────────────────

    private void StyleSliders(Transform root)
    {
        Slider[] sliders = root.GetComponentsInChildren<Slider>(true);
        foreach (Slider s in sliders)
        {
            // Kích thước + trục căn giữa ngang, kéo dài 460
            RectTransform rt = s.transform as RectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(460f, 30f);
            rt.anchoredPosition = new Vector2(0f, YOf(s.name));

            // Nhãn bên trái phía trên
            TMP_Text label = s.transform.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = LabelOf(s.name);
                label.fontSize = 28f;
                label.color = TextMain;
                var lrt = label.rectTransform;
                lrt.anchorMin = new Vector2(0f, 1f);
                lrt.anchorMax = new Vector2(0f, 1f);
                lrt.pivot = new Vector2(0f, 0f);
                lrt.anchoredPosition = new Vector2(4f, 16f);
                lrt.sizeDelta = new Vector2(340f, 34f);
                label.alignment = TextAlignmentOptions.Left;
                label.raycastTarget = false;
            }
            else if (s.name.Trim().StartsWith("Sensitivity"))
            {
                // Sensitivity chưa có label con -> tạo như một con của slider,
                // dùng đúng anchor top-left như các label khác
                Transform madeT = s.transform.Find("SensitivityLabel");
                GameObject mgo = madeT != null ? madeT.gameObject : new GameObject("SensitivityLabel");
                if (madeT == null) mgo.transform.SetParent(s.transform, false);
                TMP_Text made = mgo.GetComponent<TMP_Text>();
                if (made == null) made = mgo.AddComponent<TextMeshProUGUI>();
                made.text = LabelOf(s.name);
                made.fontSize = 28f;
                made.color = TextMain;
                made.alignment = TextAlignmentOptions.Left;
                made.raycastTarget = false;
                var mrt = made.rectTransform;
                mrt.anchorMin = new Vector2(0f, 1f);
                mrt.anchorMax = new Vector2(0f, 1f);
                mrt.pivot = new Vector2(0f, 0f);
                mrt.anchoredPosition = new Vector2(4f, 16f);
                mrt.sizeDelta = new Vector2(340f, 34f);
            }

            // Giá trị % bên phải
            Transform valueT = s.transform.Find("ValueLabel");
            GameObject vgo = valueT != null ? valueT.gameObject : new GameObject("ValueLabel");
            if (valueT == null) vgo.transform.SetParent(s.transform, false);
            TMP_Text value = vgo.GetComponent<TMP_Text>();
            if (value == null) value = vgo.AddComponent<TextMeshProUGUI>();
            value.fontSize = 24f;
            value.color = TextSub;
            value.alignment = TextAlignmentOptions.Right;
            value.raycastTarget = false;
            var vrt = value.rectTransform;
            vrt.anchorMin = new Vector2(1f, 1f);
            vrt.anchorMax = new Vector2(1f, 1f);
            vrt.pivot = new Vector2(1f, 0f);
            vrt.anchoredPosition = new Vector2(-4f, 16f);
            vrt.sizeDelta = new Vector2(120f, 34f);
            UpdateValueText(s, value);

            s.onValueChanged.AddListener(_ => UpdateValueText(s, value));

            // Màu track / fill / handle
            foreach (Transform c in s.transform)
            {
                string cn = c.name;
                if (cn == "Background")
                {
                    Image b = c.GetComponent<Image>();
                    if (b != null) b.color = TrackDark;
                }
                else if (cn == "Fill Area")
                {
                    Image f = c.GetComponentInChildren<Image>(true);
                    if (f != null) f.color = AccentRed;
                }
                else if (cn == "Handle Slide Area")
                {
                    Image h = c.GetComponentInChildren<Image>(true);
                    if (h != null) h.color = HandleLight;
                }
            }
        }
    }

    private static void UpdateValueText(Slider s, TMP_Text value)
    {
        // Hiển thị % theo giá trị chuẩn hoá 0..1, làm tròn 5%
        float pct = Mathf.Round(s.normalizedValue * 100f / 5f) * 5f;
        value.text = pct + "%";
    }

    // ───────────────────────── Dropdowns ─────────────────────────

    private void StyleDropdowns(Transform root)
    {
        var dropdowns = root.GetComponentsInChildren<TMP_Dropdown>(true);
        foreach (TMP_Dropdown d in dropdowns)
        {
            RectTransform rt = d.transform as RectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(460f, 44f);
            rt.anchoredPosition = new Vector2(0f, YOf(d.name));

            Image img = d.GetComponent<Image>();
            if (img != null) img.color = ControlDark;

            TMP_Text caption = d.captionText;
            if (caption != null)
            {
                caption.fontSize = 26f;
                caption.color = TextMain;
                caption.alignment = TextAlignmentOptions.Left;
                var crt = caption.rectTransform;
                if (crt.anchorMin.x > 0f)
                {
                    // với Label cân giữa, kéo về căn trái
                    crt.anchorMin = new Vector2(0f, 0f);
                    crt.anchorMax = new Vector2(1f, 1f);
                    crt.pivot = new Vector2(0.5f, 0.5f);
                    crt.offsetMin = new Vector2(14f, crt.offsetMin.y);
                    crt.offsetMax = new Vector2(-40f, crt.offsetMax.y);
                }
            }

            // Header chữ nhỏ phía trên dropdown (RESOLUTION / GRAPHICS QUALITY)
            Transform headT = d.transform.Find("Header");
            GameObject hgo = headT != null ? headT.gameObject : new GameObject("Header");
            if (headT == null) hgo.transform.SetParent(d.transform.parent, false);
            TMP_Text head = hgo.GetComponent<TMP_Text>();
            if (head == null) head = hgo.AddComponent<TextMeshProUGUI>();
            head.text = d.name.Trim().StartsWith("Resolution") ? "RESOLUTION" : "GRAPHICS QUALITY";
            head.fontSize = 22f;
            head.color = TextSub;
            head.raycastTarget = false;
            var hrt = head.rectTransform;
            hrt.anchorMin = new Vector2(0.5f, 0.5f);
            hrt.anchorMax = new Vector2(0.5f, 0.5f);
            hrt.pivot = new Vector2(0f, 0f);
            hrt.anchoredPosition = new Vector2(-230f, YOf(d.name) + 30f);
            hrt.sizeDelta = new Vector2(460f, 26f);
            head.alignment = TextAlignmentOptions.Left;

            Image arrow = d.transform.Find("Arrow") != null
                ? d.transform.Find("Arrow").GetComponent<Image>() : null;
            if (arrow != null) arrow.color = TextSub;

            Transform template = d.transform.Find("Template");
            if (template != null)
            {
                Image t = template.GetComponent<Image>();
                if (t != null) t.color = ControlDark;

                TMP_Text itemLabel = template.GetComponentInChildren<TMP_Text>(true);
                if (itemLabel != null) { itemLabel.fontSize = 24f; itemLabel.color = TextMain; }

                Transform item = template.Find("Viewport/Content/Item");
                if (item != null)
                {
                    Transform itemBgT = item.Find("Item Background");
                    if (itemBgT != null)
                    {
                        Image itemBg = itemBgT.GetComponent<Image>();
                        if (itemBg != null) itemBg.color = new Color(0.34f, 0.12f, 0.10f, 1f);
                    }
                }

                Transform sb = template.Find("Scrollbar");
                if (sb != null)
                {
                    Image sbHandle = sb.GetComponentInChildren<Image>(true);
                    if (sbHandle != null) sbHandle.color = TrackDark;
                }
            }
        }
    }

    // ───────────────────────── Toggle Fullscreen ─────────────────────────

    private void StyleToggle(Transform root)
    {
        Toggle toggle = null;
        foreach (var t in root.GetComponentsInChildren<Toggle>(true)) { toggle = t; break; }
        if (toggle == null) return;

        // skip dropdown item toggles
        if (toggle.name.Trim().StartsWith("Item")) return;

        RectTransform rt = toggle.transform as RectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(460f, 40f);
        rt.anchoredPosition = new Vector2(0f, YOf(toggle.name));

        Transform bg = toggle.transform.Find("Background");
        if (bg != null)
        {
            Image b = bg.GetComponent<Image>();
            if (b != null)
            {
                b.color = TrackDark;
                var brt = bg.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0f, 0.5f);
                brt.anchorMax = new Vector2(0f, 0.5f);
                brt.pivot = new Vector2(0f, 0.5f);
                brt.anchoredPosition = new Vector2(4f, 0f);
                brt.sizeDelta = new Vector2(30f, 30f);
            }
            Transform check = bg.Find("Checkmark");
            if (check != null)
            {
                Image ck = check.GetComponent<Image>();
                if (ck != null) ck.color = AccentRed;
            }
        }

        Transform labelT = toggle.transform.Find("Label");
        if (labelT != null)
        {
            Text legacy = labelT.GetComponent<Text>();
            if (legacy != null)
            {
                legacy.fontSize = 28;
                legacy.color = TextMain;
                legacy.alignment = TextAnchor.MiddleLeft;
                var lrt = labelT.GetComponent<RectTransform>();
                lrt.anchorMin = new Vector2(0f, 0f);
                lrt.anchorMax = new Vector2(1f, 1f);
                lrt.offsetMin = new Vector2(46f, 0f);
                lrt.offsetMax = new Vector2(-4f, 0f);
            }
            TMP_Text tmp = labelT.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                tmp.fontSize = 28f;
                tmp.color = TextMain;
                tmp.alignment = TextAlignmentOptions.Left;
            }
        }

            // Header FULLSCREEN phía trên checkbox (trục căn trái)
            Transform headT = toggle.transform.parent.Find("FullscreenHeader");
            if (headT == null)
            {
                TMP_Text head = MakeText(toggle.transform.parent, "FullscreenHeader", "FULLSCREEN", 22f, TextSub,
                    Vector2.zero, FontStyles.Normal, TextAlignmentOptions.Left, 340f);
                head.rectTransform.pivot = new Vector2(0f, 0.5f);
                head.rectTransform.anchoredPosition = new Vector2(-228f, YOf(toggle.name) + 36f);
            }
    }

    // ───────────────────────── Nút Back ─────────────────────────

    private void StyleBackButton(Transform root)
    {
        Transform backT = root.Find("BackButton");
        if (backT == null) return;

        RectTransform rt = backT as RectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(260f, 54f);
        rt.anchoredPosition = new Vector2(0f, YOf(backT.name));

        Image img = backT.GetComponent<Image>();
        if (img != null) img.color = ControlDark;

        TMP_Text text = backT.GetComponentInChildren<TMP_Text>(true);
        if (text != null) { text.fontSize = 30f; text.color = TextMain; text.text = "BACK"; }

        Button btn = backT.GetComponent<Button>();
        if (btn != null)
        {
            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(0.40f, 0.20f, 0.18f, 1f);
            cb.pressedColor = new Color(0.62f, 0.28f, 0.24f, 1f);
            cb.fadeDuration = 0.1f;
            btn.colors = cb;
        }
    }

    // ───────────────────────── Tiêu đề + header nhóm ─────────────────────────

    private void CreateHeaders(Transform root)
    {
        TMP_Text title = MakeText(root, "SETTINGS", "SETTINGS", 64f, TextMain, new Vector2(0f, 420f), FontStyles.Bold);
        TMP_Text audio = MakeText(root, "AUDIO", "AUDIO", 30f, AccentRed, new Vector2(0f, 320f), FontStyles.Bold,
            TextAlignmentOptions.Left, 460f);
        TMP_Text display = MakeText(root, "DISPLAY", "DISPLAY", 30f, AccentRed, new Vector2(0f, -8f), FontStyles.Bold,
            TextAlignmentOptions.Left, 460f);
        // Kéo 2 header về trục căn trái (pivot = mép trái)
        audio.rectTransform.pivot = new Vector2(0f, 0.5f);
        audio.rectTransform.anchoredPosition = new Vector2(-228f, 320f);
        display.rectTransform.pivot = new Vector2(0f, 0.5f);
        display.rectTransform.anchoredPosition = new Vector2(-228f, -8f);
    }

    private TMP_Text MakeText(Transform parent, string name, string text, float fontSize, Color color,
        Vector2 position, FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.Center,
        float width = 900f)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        if (existing == null) go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, fontSize * 1.6f);
        rt.anchoredPosition = position;

        TMP_Text tmp = go.GetComponent<TMP_Text>();
        if (tmp == null) tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        return tmp;
    }
}
