using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gắn vào UI Slider. CHỈ HIỂN THỊ, không giữ logic riêng.
/// - Giá trị thanh = AnxietyManager.Normalized (0..1)
/// - Màu đỏ khi có ít nhất một vật thể/quái từ thresholds đang tồn tại, trắng khi không còn
/// </summary>
public class VisionBar : MonoBehaviour
{
    [Header("Tham chiếu")]
    public Slider slider;
    [Tooltip("Để trống = tự tìm AnxietyManager trong scene")]
    public AnxietyManager manager;

    [Header("Hiển thị")]
    public float valueSmooth = 8f;
    public Color normalColor = Color.white;
    public Color monsterColor = Color.red;
    public float colorSmooth = 10f;

    Image fillImage;

    void Awake()
    {
        if (slider == null) slider = GetComponent<Slider>();
        if (slider == null)
        {
            Debug.LogWarning("VisionBar: không tìm thấy Slider.", this);
            return;
        }

        if (slider.fillRect != null)
            fillImage = slider.fillRect.GetComponent<Image>();
        else
            Debug.LogWarning("VisionBar: Slider chưa có Fill Rect nên không đổi màu được.", this);
    }

    void Start()
    {
        if (slider == null) return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.interactable = false;          // chỉ hiển thị, không cho kéo bằng chuột
        if (fillImage != null) fillImage.color = normalColor;

        FindManager();
        if (manager != null) slider.value = manager.Normalized;
    }

    void Update()
    {
        if (slider == null) return;

        if (manager == null)
        {
            FindManager();
            if (manager == null) return;
        }

        slider.value = Mathf.Lerp(slider.value, manager.Normalized, Time.deltaTime * valueSmooth);

        if (fillImage != null)
        {
            Color target = AnyMonsterActive() ? monsterColor : normalColor;
            fillImage.color = Color.Lerp(fillImage.color, target, Time.deltaTime * colorSmooth);
        }
    }

    void FindManager()
    {
        if (manager != null) return;
        manager = AnxietyManager.Instance;
        if (manager == null) manager = FindFirstObjectByType<AnxietyManager>();
    }

    bool AnyMonsterActive()
    {
        foreach (var t in manager.thresholds)
            if (t.instance != null) return true;
        return false;
    }
}