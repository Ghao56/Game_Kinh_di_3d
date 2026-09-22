using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class InteractPromptUI : MonoBehaviour
{
    [SerializeField] private Text promptText; // Legacy UI, không dùng TMPro
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
        if (promptText != null) promptText.text = text;
        targetAlpha = 1f;
    }

    public void Hide()
    {
        targetAlpha = 0f;
    }

    private void Update()
    {
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
    }
}