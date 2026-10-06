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
    private const string LegacyChildName = "Objectives";

    private const float TitleHeight = 34f;
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
    }

    private void OnDisable()
    {
        if (QuestManager.Instance == null) return;

        QuestManager.Instance.QuestStarted -= HandleStarted;
        QuestManager.Instance.QuestProgressed -= HandleProgressed;
        QuestManager.Instance.QuestCompleted -= HandleCompleted;
    }

    private void Update()
    {
        if (holdTimer > 0f) holdTimer -= Time.unscaledDeltaTime;

        bool shouldShow = displayed != null && (!displayedCompleted || holdTimer > 0f);
        float target = shouldShow ? 1f : 0f;

        if (!Mathf.Approximately(canvasGroup.alpha, target))
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, fadeSpeed * Time.unscaledDeltaTime);

        if (displayed != null && !shouldShow && canvasGroup.alpha <= 0f) Clear();
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
            row.Label.text = $"{objective.Text} ({displayed.GetProgress(objective.Id)}/{objective.Required})";
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