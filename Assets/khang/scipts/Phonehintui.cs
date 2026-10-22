using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// Gắn script này vào 1 UI object (thường là Canvas).
// Runtime tự tạo 1 nút nhỏ đáy màn hình: "[P] Điện thoại".
// Nút LUÔN LUÔN đọc đúng phím từ GTA_PhoneController lúc chạy - đổi phím trong
// controller thì chữ trên chip tự đổi theo, không phải sửa 2 chỗ nữa.
// Bấm chuột vào nút = mở/tắt điện thoại. Đang mở thì tự ẩn.
public class PhoneHintUI : MonoBehaviour
{
    public GTA_PhoneController gtaPhone;

    public PhoneUIManager phoneManager;

    private GameObject chip;
    private Text chipText;

    void Start()
    {
        if (gtaPhone == null)
            gtaPhone = Object.FindFirstObjectByType<GTA_PhoneController>();

        if (phoneManager == null)
            phoneManager = Object.FindFirstObjectByType<PhoneUIManager>();

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = Object.FindFirstObjectByType<Canvas>();

        if (canvas == null || (gtaPhone == null && phoneManager == null))
        {
            Debug.LogWarning($"{nameof(PhoneHintUI)}: thiếu Canvas hoặc không tìm thấy phone controller còn sống.", this);
            enabled = false;
            return;
        }

        TaoChip(canvas);
    }

    void TaoChip(Canvas canvas)
    {
        chip = new GameObject("PhoneHintChip", typeof(RectTransform), typeof(Image), typeof(Button));
        chip.transform.SetParent(canvas.transform, false);

        var rt = (RectTransform)chip.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-24f, 34f);
        rt.sizeDelta = new Vector2(300f, 42f);

        var img = chip.GetComponent<Image>();
        img.sprite = RoundedSpriteUtil.Get();
        img.type = Image.Type.Sliced;
        img.color = new Color(0.09f, 0.17f, 0.26f, 0.72f); // xanh đêm mờ

        var varGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        varGo.transform.SetParent(chip.transform, false);

        var lrt = (RectTransform)varGo.transform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;

        chipText = varGo.GetComponent<Text>();
        chipText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (chipText.font == null)
            chipText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        chipText.fontSize = 15;
        chipText.fontStyle = FontStyle.Bold;
        chipText.alignment = TextAnchor.MiddleCenter;
        chipText.color = new Color(0.78f, 0.9f, 1f);
        chipText.supportRichText = true;
        chipText.raycastTarget = false;

        // Đọc phím mở điện thoại THẬT từ controller (không lưu chuỗi cứng)
        string phim = Key.P.ToString();
        if (gtaPhone != null) phim = gtaPhone.openKey.ToString();
        else if (phoneManager != null) phim = phoneManager.openKey.ToString();
        chipText.text = $"<color=#ffe27a>[{phim}] Điện thoại</color>";

        chip.GetComponent<Button>().onClick.AddListener(BamChip);
    }

    void BamChip()
    {
        if (gtaPhone != null)
            gtaPhone.TogglePhone();
        else if (phoneManager != null)
            phoneManager.TogglePhoneState();
    }

    void Update()
    {
        if (chip == null)
            return;

        bool open = (gtaPhone != null && gtaPhone.IsOpen) || (phoneManager != null && phoneManager.IsPhoneOpen);
        chip.SetActive(!open);
    }
}
