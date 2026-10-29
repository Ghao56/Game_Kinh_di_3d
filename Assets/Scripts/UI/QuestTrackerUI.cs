using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// HUD góc trên-phải: tiêu đề quest + danh sách mục tiêu dạng "text (cur/req)".
/// Mục tiêu hoàn thành thì tô xám kèm vạch gạch ngang. Có nền đen mờ phía sau để
/// đọc được khi đứng trong vùng sáng. Mỗi lúc chỉ hiện quest bắt đầu gần nhất.
[RequireComponent(typeof(CanvasGroup))]
public class QuestTrackerUI : MonoBehaviour
{
    private const string TitleChildName = "Title";
    private const string RowsChildName = "Rows";
    private const string DebtChildName = "DebtLine";
    private const string DebtDividerChildName = "DebtDivider";
    private const string LegacyChildName = "Objectives";

    private const float TitleHeight = 34f;
    private const float DebtHeight = 30f;
    private const float CheckWidth = 28f;
    private const float LabelLeftPadding = 32f;
    private const float LabelRightPadding = 6f;

    private class ObjectiveRow
    {
        public RectTransform Root;
        public Text Check;
        public Text Label;
        public Image Strike;
    }

    [Header("Tham chiếu")]
    [SerializeField] private Text titleText;
    [SerializeField] private RectTransform rowsRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image backgroundImage;

    [Header("Nền")]
    [Tooltip("Màu nền phía sau bảng quest.")]
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.55f);

    [Header("Màu chữ")]
    [SerializeField] private Color titleColor = new Color(0.95f, 0.92f, 0.82f, 1f);
    [SerializeField] private Color normalColor = new Color(0.88f, 0.86f, 0.78f, 1f);
    [SerializeField] private Color completedColor = new Color(0.54f, 0.54f, 0.54f, 1f);

    [Header("Gạch ngang")]
    [Tooltip("Màu vạch gạch ngang mục tiêu đã hoàn thành.")]
    [SerializeField] private Color strikeColor = new Color(0.7f, 0.7f, 0.7f, 0.9f);

    [Min(0.5f)]
    [Tooltip("Độ dày vạch gạch ngang.")]
    [SerializeField] private float strikeThickness = 2f;

    [Header("Bố cục")]
    [Min(100f)]
    [Tooltip("Bề ngang bảng quest.")]
    [SerializeField] private float panelWidth = 460f;

    [SerializeField]
    [Tooltip("Khoảng cách từ góc trên-phải màn hình.")]
    private Vector2 panelOffset = new Vector2(-24f, -24f);

    [Min(10f)]
    [Tooltip("Chiều cao một dòng mục tiêu.")]
    [SerializeField] private float rowHeight = 30f;

    [Min(0f)]
    [Tooltip("Khoảng cách giữa các dòng mục tiêu.")]
    [SerializeField] private float rowSpacing = 4f;

    [Header("Hiển thị")]
    [Min(0.1f)]
    [Tooltip("Tốc độ đổi alpha mỗi giây (không phụ thuộc Time.timeScale).")]
    [SerializeField] private float fadeSpeed = 5f;

    [Min(0f)]
    [Tooltip("Giữ HUD bao lâu sau khi xong rồi mới mờ dần.")]
    [SerializeField] private float completedHoldDuration = 1.5f;

    private readonly List<ObjectiveRow> rows = new List<ObjectiveRow>();

    // Dòng nợ persistent (mục 5 concept: "số nợ ban đầu hiển thị rõ"):quest
    // loot_truck chỉ bật từ S3 nên số nợ cần hiển ngay từ S0 trong chính bảng
    // quest thay vì một HUD riêng. Nợ xong thì dòng tự ẩn.
    private Text debtText;       // nhãn trái "NỢ CÒN LẠI"
    private Text debtAmountText; // số tiền phải (đổi màu theo tiến độ trả)
    private Image debtDivider;   // kẻ mảnh phân cách với phần quest bên dưới

    private QuestRuntime displayed;
    private bool displayedCompleted;
    private float holdTimer;

    private void Awake()
    {
        canvasGroup = canvasGroup != null ? canvasGroup : GetComponent<CanvasGroup>();

        EnsureBackground();
        EnsureLayout();
        EnsureTitle();
        EnsureRows();
        RemoveLegacyText();

        if (titleText == null || rowsRoot == null)
        {
            Debug.LogError($"[{nameof(QuestTrackerUI)}] Thiếu Title hoặc Rows, không dựng được HUD.", this);
            enabled = false;
            return;
        }

        EnsureDebtLine();
        UpdateDebtLine();

        FitToTopRight();

        titleText.color = titleColor;
        backgroundImage.color = backgroundColor;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void OnEnable()
    {
        if (QuestManager.Instance == null)
        {
            Debug.LogWarning($"[{nameof(QuestTrackerUI)}] Không có QuestManager trong scene, HUD sẽ không cập nhật.", this);
            return;
        }

        QuestManager.Instance.QuestStarted += HandleStarted;
        QuestManager.Instance.QuestProgressed += HandleProgressed;
        QuestManager.Instance.QuestCompleted += HandleCompleted;

        DebtTracker.OnPaidChanged += HandlePaidChanged;
    }

    private void OnDisable()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.QuestStarted -= HandleStarted;
            QuestManager.Instance.QuestProgressed -= HandleProgressed;
            QuestManager.Instance.QuestCompleted -= HandleCompleted;
        }

        // Unsub TIỀN phải chạy trong mọi trường hợp: event là static, quên unsub
        // là NRE lúc tiền đổi sau khi HUD này đã destroy theo scene.
        // (-= một handler chưa từng += là an toàn, không cần kiểm tra null —
        // và C# cấm đọc event này từ ngoài DebtTracker.)
        DebtTracker.OnPaidChanged -= HandlePaidChanged;
    }

    private void Update()
    {
        if (holdTimer > 0f) holdTimer -= Time.unscaledDeltaTime;

        bool debtVisible = debtText != null && debtText.gameObject.activeSelf;
        bool shouldShow = debtVisible || (displayed != null && (!displayedCompleted || holdTimer > 0f));
        float target = shouldShow ? 1f : 0f;

        if (!Mathf.Approximately(canvasGroup.alpha, target))
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, fadeSpeed * Time.unscaledDeltaTime);

        if (displayed != null && !shouldShow && canvasGroup.alpha <= 0f) Clear();
    }

    // ------------------------------------------------------------ dòng nợ persistent

    private void HandlePaidChanged(int _) => UpdateDebtLine();

    private void UpdateDebtLine()
    {
        if (debtText == null) return;

        int total = DebtTracker.TotalDebtMillions;
        int paid = DebtTracker.DebtPaidMillions;
        int bonus = DebtTracker.BonusMillions;

        bool wasActive = debtText.gameObject.activeSelf;

        // Ba trạng thái: còn nợ → hoặc đã đủ nợ mà còn tiền dư → hiện số tiền dư.
        // Cả hai đều 0 (trả đúng bằng nợ, không chất thêm) thì hàng nhường chỗ
        // cho bảng quest.
        bool debtLeft = paid < total;
        bool active = debtLeft || bonus > 0;

        // Hết nợ: cả hàng + kẻ phân cách nhường chỗ cho bảng quest
        // (phần thắng sẽ tới màn kết "BẠN ĐÃ TRẢ HẾT NỢ").
        debtText.gameObject.SetActive(active);
        if (debtDivider != null) debtDivider.gameObject.SetActive(active);

        if (!active)
        {
            ForceLayoutOnce();
            return;
        }

        if (!debtLeft && bonus > 0)
        {
            // Chế độ tiền dư: nhãn + số xanh lá để khác rõ với lúc còn nợ.
            debtText.text = "TI\u1EC0N D\u01AF";
            debtText.color = new Color(0.5f, 0.95f, 0.55f, 1f);
            debtAmountText.color = new Color(0.5f, 0.95f, 0.55f, 1f);
            debtAmountText.text = $"+{bonus} TRI\u1EC6U";
        }
        else
        {
            debtText.text = "N\u1EE2 C\u00D2N L\u1EA0I";
            debtText.color = new Color(0.92f, 0.86f, 0.74f, 1f);

            // Số tiền đổi màu dần: nợ còn nguyên = đỏ, gần trả xong = vàng ấm —
            // người chơi "cảm" được đang tiến gần mục tiêu mà không cần số phần trăm.
            float paidRatio = total > 0 ? Mathf.Clamp01(paid / (float)total) : 0f;
            debtAmountText.color = Color.Lerp(
                new Color(0.95f, 0.4f, 0.35f, 1f),
                new Color(1f, 0.85f, 0.5f, 1f),
                paidRatio);
            debtAmountText.text = $"{DebtTracker.RemainingDebtMillions} TRI\u1EC6U";
        }

        if (wasActive != active) ForceLayoutOnce();
    }

    // Bảng bên ngoài dùng ContentSizeFitter (Preferred): bật/tắt con đổi tổng
    // cao nhưng Unity chỉ sắp lại ở model layout frame kế — chủ động ép ngay
    // để nhảy số không bị lệch 1 frame.
    private void ForceLayoutOnce()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
    }

    // Hàng nợ = "NỢ CÒN LẠI" (trái) + số tiền (phải) + kẻ mảnh, nằm TRÊN tiêu đề
    // quest trong cùng bảng (cùng VerticalLayoutGroup nên thẳng lề, cùng nền).
    private void EnsureDebtLine()
    {
        if (debtText != null) return;

        // ---- hàng chính: nhãn trái + số tiền phải
        Transform existingRow = transform.Find(DebtChildName);
        GameObject row = existingRow != null
            ? existingRow.gameObject
            : new GameObject(DebtChildName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(transform, false);

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(2, 6, 0, 4);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        // Cấu hình này phải chạy TRƯỚC khi con được thêm: Unity chỉ build đã
        // kích hoạt khi cần nhấn layout frame tiếp theo.

        GameObject labelGO = CreateTextObject("Label", (RectTransform)row.transform);
        debtText = labelGO.GetComponent<Text>();
        debtText.text = "N\u1EE2 C\u00D2N L\u1EA0I";
        debtText.fontSize = 20;
        debtText.fontStyle = FontStyle.Bold;
        // không wrap để tiêu đề nhãn không bị ngắt dòng trong layout ngang.
        debtText.horizontalOverflow = HorizontalWrapMode.Overflow;
        debtText.verticalOverflow = VerticalWrapMode.Overflow;
        SetPreferredWidth(labelGO, 130f);

        GameObject amountGO = CreateTextObject("Amount", (RectTransform)row.transform);
        debtAmountText = amountGO.GetComponent<Text>();
        debtAmountText.text = string.Empty;
        debtAmountText.fontSize = 26;
        debtAmountText.fontStyle = FontStyle.Bold;
        debtAmountText.alignment = TextAnchor.MiddleRight; // số tiền khít lề phải
        LayoutElement amountElement = GetOrCreateLayoutElement(amountGO);
        amountElement.flexibleWidth = 1f; // chiếm hết phần còn → nhãn trái, số phải
        amountElement.ignoreLayout = false;

        SetPreferredHeight(row, DebtHeight);

        // ---- kẻ mảnh phân cách dòng nợ với quest bên dưới
        Transform existingDivider = transform.Find(DebtDividerChildName);
        GameObject divider = existingDivider != null
            ? existingDivider.gameObject
            : new GameObject(DebtDividerChildName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        divider.transform.SetParent(transform, false);

        Image dividerImage = divider.GetComponent<Image>();
        dividerImage.raycastTarget = false;
        dividerImage.color = new Color(0.9f, 0.75f, 0.5f, 0.35f);
        debtDivider = dividerImage;

        LayoutElement dividerElement = GetOrCreateLayoutElement(divider);
        dividerElement.preferredHeight = 2f;
        dividerElement.minHeight = 2f;
        dividerElement.flexibleWidth = 1f; // kẻ căng hết bề rộng bảng

        // Thứ tự trong VerticalLayoutGroup: nợ (top) → kẻ → tiêu đề quest → objectives.
        row.transform.SetAsFirstSibling();
        divider.transform.SetSiblingIndex(1);

        row.SetActive(false);
        divider.SetActive(false); // ẩn tới khi UpdateDebtLine cập nhật số
    }

    private static void SetPreferredWidth(GameObject go, float width)
    {
        LayoutElement element = GetOrCreateLayoutElement(go);
        element.minWidth = width;
        element.preferredWidth = width;
        element.flexibleWidth = 0f;
    }

    private static LayoutElement GetOrCreateLayoutElement(GameObject go)
    {
        LayoutElement element = go.GetComponent<LayoutElement>();
        if (element == null) element = go.AddComponent<LayoutElement>();
        return element;
    }

    private void HandleStarted(QuestRuntime runtime)
    {
        displayed = runtime;
        displayedCompleted = false;
        holdTimer = 0f;
        Rebuild();
    }

    private void HandleProgressed(QuestRuntime runtime)
    {
        if (displayed != runtime) return;
        Rebuild();
    }

    private void HandleCompleted(QuestRuntime runtime)
    {
        if (displayed != runtime) return;

        displayedCompleted = true;
        holdTimer = completedHoldDuration;
        Rebuild();
    }

    private void Rebuild()
    {
        if (displayed == null) return;

        titleText.text = displayed.Definition.DisplayTitle;

        IReadOnlyList<QuestObjective> objectives = displayed.Objectives;
        int count = objectives.Count;
        EnsureRowCount(count);

        for (int i = 0; i < count; i++)
        {
            QuestObjective objective = objectives[i];
            ObjectiveRow row = rows[i];

            if (objective == null)
            {
                row.Root.gameObject.SetActive(false);
                continue;
            }

            row.Root.gameObject.SetActive(true);

            bool done = displayed.IsObjectiveComplete(objective.Id);
            row.Check.text = done ? "[x]" : "[ ]";
            row.Check.color = done ? completedColor : normalColor;
            row.Label.text = $"{objective.Text} ({displayed.GetProgress(objective.Id)}/{objective.Required}{objective.ProgressSuffix})";
            row.Label.color = done ? completedColor : normalColor;
            row.Strike.color = strikeColor;
            row.Strike.gameObject.SetActive(done);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
        LayoutRebuilder.ForceRebuildLayoutImmediate(rowsRoot);

        for (int i = 0; i < count; i++)
        {
            ObjectiveRow row = rows[i];
            if (!row.Root.gameObject.activeSelf || !row.Strike.gameObject.activeSelf) continue;

            float width = Mathf.Min(row.Label.preferredWidth, row.Label.rectTransform.rect.width);
            RectTransform strikeRt = (RectTransform)row.Strike.transform;
            strikeRt.sizeDelta = new Vector2(width, strikeThickness);
        }
    }

    private void Clear()
    {
        displayed = null;
        displayedCompleted = false;
        holdTimer = 0f;

        titleText.text = string.Empty;
        for (int i = 0; i < rows.Count; i++) rows[i].Root.gameObject.SetActive(false);
    }

    private void EnsureBackground()
    {
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();
        if (backgroundImage == null) backgroundImage = gameObject.AddComponent<Image>();
        backgroundImage.raycastTarget = false;
    }

    private void EnsureLayout()
    {
        VerticalLayoutGroup layout = GetComponent<VerticalLayoutGroup>();
        if (layout == null) layout = gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 10, 12);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void EnsureTitle()
    {
        if (titleText == null)
        {
            Transform child = transform.Find(TitleChildName);
            if (child != null) titleText = child.GetComponent<Text>();
        }

        if (titleText == null)
        {
            GameObject titleGO = CreateTextObject(TitleChildName, (RectTransform)transform);
            titleText = titleGO.GetComponent<Text>();
        }

        titleText.font = GetFont();
        titleText.fontSize = 26;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
        titleText.verticalOverflow = VerticalWrapMode.Overflow;
        titleText.raycastTarget = false;

        SetPreferredHeight(titleText.gameObject, TitleHeight);
    }

    private void EnsureRows()
    {
        if (rowsRoot == null)
        {
            Transform child = transform.Find(RowsChildName);
            if (child != null) rowsRoot = child as RectTransform;
        }

        if (rowsRoot == null)
        {
            GameObject rowsGO = new GameObject(RowsChildName, typeof(RectTransform));
            rowsGO.transform.SetParent(transform, false);
            rowsRoot = (RectTransform)rowsGO.transform;
        }

        rowsRoot.anchorMin = new Vector2(0f, 1f);
        rowsRoot.anchorMax = new Vector2(1f, 1f);
        rowsRoot.pivot = new Vector2(0.5f, 1f);
        rowsRoot.anchoredPosition = Vector2.zero;

        LayoutElement legacyElement = rowsRoot.GetComponent<LayoutElement>();
        if (legacyElement != null) DestroyObject(legacyElement.gameObject);

        VerticalLayoutGroup rowsLayout = rowsRoot.GetComponent<VerticalLayoutGroup>();
        if (rowsLayout == null) rowsLayout = rowsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        rowsLayout.padding = new RectOffset(0, 0, 0, 0);
        rowsLayout.spacing = rowSpacing;
        rowsLayout.childAlignment = TextAnchor.UpperLeft;
        rowsLayout.childControlWidth = true;
        rowsLayout.childControlHeight = true;
        rowsLayout.childForceExpandWidth = true;
        rowsLayout.childForceExpandHeight = false;
    }

    private void RemoveLegacyText()
    {
        Transform legacy = transform.Find(LegacyChildName);
        if (legacy == null) return;

        DestroyObject(legacy.gameObject);
    }

    private void FitToTopRight()
    {
        RectTransform rootRt = (RectTransform)transform;
        rootRt.anchorMin = new Vector2(1f, 1f);
        rootRt.anchorMax = new Vector2(1f, 1f);
        rootRt.pivot = new Vector2(1f, 1f);
        rootRt.anchoredPosition = panelOffset;
        rootRt.sizeDelta = new Vector2(panelWidth, rootRt.sizeDelta.y);
    }

    private void EnsureRowCount(int count)
    {
        while (rows.Count > count)
        {
            int last = rows.Count - 1;
            DestroyObject(rows[last].Root.gameObject);
            rows.RemoveAt(last);
        }

        while (rows.Count < count) CreateRow();
    }

    private ObjectiveRow CreateRow()
    {
        GameObject rowGO = new GameObject("Row", typeof(RectTransform));
        rowGO.transform.SetParent(rowsRoot, false);
        RectTransform rowRt = (RectTransform)rowGO.transform;

        LayoutElement rowElement = rowGO.AddComponent<LayoutElement>();
        rowElement.minHeight = rowHeight;
        rowElement.preferredHeight = rowHeight;
        rowElement.flexibleWidth = 1f;

        Text check = CreateTextObject("Check", rowRt).GetComponent<Text>();
        RectTransform checkRt = check.rectTransform;
        checkRt.anchorMin = Vector2.zero;
        checkRt.anchorMax = new Vector2(0f, 1f);
        checkRt.pivot = new Vector2(0f, 0.5f);
        checkRt.offsetMin = Vector2.zero;
        checkRt.offsetMax = new Vector2(CheckWidth, 0f);
        check.alignment = TextAnchor.MiddleLeft;
        check.horizontalOverflow = HorizontalWrapMode.Overflow;
        check.verticalOverflow = VerticalWrapMode.Overflow;

        Text label = CreateTextObject("Label", rowRt).GetComponent<Text>();
        RectTransform labelRt = label.rectTransform;
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(LabelLeftPadding, 0f);
        labelRt.offsetMax = new Vector2(-LabelRightPadding, 0f);
        label.fontSize = 22;
        label.alignment = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.lineSpacing = 1.1f;

        GameObject strikeGO = new GameObject("Strike", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        strikeGO.transform.SetParent(labelRt, false);
        RectTransform strikeRt = (RectTransform)strikeGO.transform;
        strikeRt.anchorMin = new Vector2(0f, 0.5f);
        strikeRt.anchorMax = new Vector2(0f, 0.5f);
        strikeRt.pivot = new Vector2(0f, 0.5f);
        strikeRt.anchoredPosition = Vector2.zero;
        strikeRt.sizeDelta = new Vector2(0f, strikeThickness);
        Image strike = strikeGO.GetComponent<Image>();
        strike.raycastTarget = false;

        ObjectiveRow row = new ObjectiveRow { Root = rowRt, Check = check, Label = label, Strike = strike };
        rows.Add(row);
        return row;
    }

    private static GameObject CreateTextObject(string name, RectTransform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);

        Text text = go.GetComponent<Text>();
        text.font = GetFont();
        text.fontSize = 22;
        text.color = Color.white;
        text.raycastTarget = false;
        return go;
    }

    private static void SetPreferredHeight(GameObject go, float height)
    {
        LayoutElement element = go.GetComponent<LayoutElement>();
        if (element == null) element = go.AddComponent<LayoutElement>();
        element.ignoreLayout = false;
        element.layoutPriority = 1;
        element.minHeight = height;
        element.preferredHeight = height;
        element.flexibleHeight = 0f;
    }

    private static void DestroyObject(GameObject go)
    {
        if (go == null) return;

        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    private static Font GetFont()
    {
        Font font = null;
        try
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        catch (System.Exception)
        {
            font = null;
        }

        return font;
    }
}