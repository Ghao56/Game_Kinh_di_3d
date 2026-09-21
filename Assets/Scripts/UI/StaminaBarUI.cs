using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class StaminaBarUI : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private StaminaSystem stamina;
    [Tooltip("Image màu trắng, Image Type = Filled, Horizontal.")]
    [SerializeField] private Image fillImage;

    [Header("Fade")]
    [Tooltip("Từ mức này (0..1) trở lên thanh bắt đầu mờ dần; đầy (1.0) thì ẩn hoàn toàn.")]
    [SerializeField, Range(0f, 0.99f)] private float fadeStartRatio = 0.9f;
    [Tooltip("Tốc độ đổi alpha mỗi giây, để thanh không bật/tắt giật.")]
    [SerializeField] private float fadeSpeed = 4f;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (stamina == null)
        {
            Debug.LogError("[StaminaBarUI] Chưa gán StaminaSystem trong Inspector.", this);
            enabled = false;
            return;
        }

        if (fillImage == null)
        {
            Debug.LogError("[StaminaBarUI] Chưa gán Fill Image trong Inspector.", this);
            enabled = false;
            return;
        }

        // Stamina bắt đầu ở mức đầy nên thanh ẩn sẵn.
        canvasGroup.alpha = 0f;
    }

    private void Update()
    {
        float ratio = stamina.Normalized;
        fillImage.fillAmount = ratio;

        float targetAlpha = 1f - Mathf.InverseLerp(fadeStartRatio, 1f, ratio);
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
    }
}