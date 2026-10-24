using TMPro;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class InteractPromptUI : MonoBehaviour
{
    [SerializeField] private TMP_Text promptText; // nhận cả TextMeshProUGUI/Mesh Pro, vị trí do người dùng kéo thoải mái
    [SerializeField] private float fadeSpeed = 5f;

    private CanvasGroup canvasGroup;
    private float targetAlpha;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        targetAlpha = 0f;
    }

    public void Show(string text)
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (promptText != null)
        {
            if (!promptText.gameObject.activeSelf)
                promptText.gameObject.SetActive(true);

            promptText.text = text;
        }
        else
        {
            Debug.LogError("[PROMPT_UI] promptText CHƯA ĐƯỢC GÁN trong Inspector!", this);
        }

        targetAlpha = 1f;
    }

    public void Hide()
    {
        targetAlpha = 0f;
    }

    private void Update()
    {
        // Đảm bảo text luôn bật khi đang target
        if (targetAlpha > 0.01f && promptText != null && !promptText.gameObject.activeSelf)
            promptText.gameObject.SetActive(true);

        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
    }
}