using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Gắn vào bất kỳ object trong Canvas. Runtime dựng panel "TÚI ĐỒ" bên trái hotbar:
// tiêu đề số ô đã dùng /6, từng dòng đồ loot đang mang (tên - số ô - tiền),
// dòng footer tổng tiền trong túi và tiền đã về an toàn.
// Kiểu dáng giống BagPanel của InventoryUI (nền bo góc RoundedSpriteUtil).
public class LootBagPanelUI : MonoBehaviour
{
    [Header("VỊ TRÍ / KÍCH THƯỚC")]
    public float beRongPanel = 320f;
    public float leText = 16f;

    [Header("HIỆN TÚI THEO NHIỆM VỤ")]
    [Tooltip("Túi chỉ hiện khi quest này đang chạy hoặc đã xong (mở cửa đôi xe).")]
    public string showQuestId = "truck_open";

    [Header("MÀU")]
    public Color mauNen = new Color(0.07f, 0.09f, 0.14f, 0.95f);
    public Color mauVien = new Color(0.45f, 0.65f, 0.95f, 0.4f);

    Text label;
    RectTransform panelRect;

    CanvasGroup group;
    GTA_PhoneController gtaPhone;
    PhoneUIManager phoneManager;

    void Start()
    {
        label = GetComponent<Text>();

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = Object.FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning($"{nameof(LootBagPanelUI)}: không tìm thấy Canvas.", this);
            enabled = false;
            return;
        }

        gtaPhone = Object.FindFirstObjectByType<GTA_PhoneController>();
        phoneManager = Object.FindFirstObjectByType<PhoneUIManager>();

        TaoPanel(canvas);

        if (PlayerLootBag.Instance != null)
            PlayerLootBag.Instance.OnChanged += Refresh;

        Refresh();
    }

    void OnDestroy()
    {
        if (PlayerLootBag.Instance != null)
            PlayerLootBag.Instance.OnChanged -= Refresh;
    }

    void TaoPanel(Canvas canvas)
    {
        // Nền panel bên trái màn hình, cùng phong cách BagPanel cũ
        var root = new GameObject("BagPanelLoot", typeof(RectTransform), typeof(Image), typeof(Outline));
        root.transform.SetParent(canvas.transform, false);

        panelRect = (RectTransform)root.transform;
        // Slot tay (hotbar) nằm dưới giữa: panel nách sang trái
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-320f, 22f);
        panelRect.sizeDelta = new Vector2(beRongPanel, 170f);

        var bg = root.GetComponent<Image>();
        bg.sprite = RoundedSpriteUtil.Get();
        bg.type = Image.Type.Sliced;
        bg.color = mauNen;
        bg.raycastTarget = false;

        root.GetComponent<Outline>().effectColor = mauVien;

        if (root.transform.Find("Text") != null)
            label = root.transform.Find("Text").GetComponent<Text>();
        else
        {
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(root.transform, false);

            var rt = (RectTransform)textGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(leText, leText);
            rt.offsetMax = new Vector2(-leText, -leText);

            label = textGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 20;
            label.color = new Color(0.85f, 0.94f, 1f);
            label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
        }

        group = root.AddComponent<CanvasGroup>();
    }

    void Refresh()
    {
        if (label == null) return;

        var bag = PlayerLootBag.Instance;
        if (bag == null)
        {
            label.text = "TÚI ĐỒ (chưa có PlayerLootBag)";
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b>TÚI ĐỒ {bag.UsedSlots}/{bag.Capacity} ô</b>");

        var seen = new Dictionary<string, (int so, int o)>();
        foreach (var item in bag.Carried)
        {
            string key = item.itemName;
            if (seen.TryGetValue(key, out var prev))
                seen[key] = (prev.so + 1, prev.o);
            else
                seen[key] = (1, item.slotCost);
        }

        foreach (var kv in seen)
            sb.AppendLine($"{kv.Key} ×{kv.Value.so}  ({kv.Value.o} ô)");

        if (seen.Count == 0)
            sb.AppendLine("(trống)");

        sb.AppendLine("<color=#8a97b0><size=16>[R] bỏ đồ vào túi • R+[1-4] lấy ra tay</size></color>");
        sb.AppendLine($"Tiền nếu mang về: <color=#7fd67f>{bag.CarriedMillions}tr</color>");

        label.text = sb.ToString();
    }

    void Update()
    {
        if (group == null) return;

        // Túi chỉ hiện từ khi quest "mở cửa đôi xe" bắt đầu đến hết flow.
        bool questChoPhep = true;
        QuestManager quests = QuestManager.Instance;
        if (quests != null && !string.IsNullOrEmpty(showQuestId))
        {
            questChoPhep = quests.GetState(showQuestId) == QuestState.Active
                        || quests.GetState(showQuestId) == QuestState.Completed;
        }
        else if (quests == null)
        {
            questChoPhep = false; // chưa có QuestManager thì chưa gắn ý nhiệm vụ nào
        }

        // Phone mở -> mờ panel giống SlotsHotbarUI
        bool phoneOpen = (gtaPhone != null && gtaPhone.IsOpen) || (phoneManager != null && phoneManager.IsPhoneOpen);
        float muc = (phoneOpen || !questChoPhep) ? 0f : 1f;
        group.alpha = Mathf.MoveTowards(group.alpha, muc, Time.unscaledDeltaTime * 6f);
        group.blocksRaycasts = false;
        group.interactable = false;
    }
}
