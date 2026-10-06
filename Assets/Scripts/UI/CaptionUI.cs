using System.Collections;
using TMPro;
using UnityEngine;

/// Dòng chữ mô tả âm thanh vang lên: [KNOCK], [FOOTSTEPS ABOVE], [LOCKED]...
/// Hiện khi không có hội thoại; tự ẩn khi DialogueManager đang mở để không chồng chữ.
public class CaptionUI : MonoBehaviour
{
    [SerializeField] private TMP_Text captionText;

    [Tooltip("Để trống thì không fade, chữ hiện/ẩn ngay.")]
    [SerializeField] private CanvasGroup captionGroup;

    [SerializeField] [Min(0.1f)] private float defaultDuration = 2.5f;
    [SerializeField] [Min(0.1f)] private float fadeSpeed = 5f;

    private Coroutine hideRoutine;

    private void Awake()
    {
        if (captionText == null) captionText = GetComponentInChildren<TMP_Text>(true);

        if (captionText == null)
        {
            Debug.LogError($"[{nameof(CaptionUI)}] Thiếu TMP_Text.", this);
            enabled = false;
            return;
        }

        SetAlpha(0f);
        captionText.text = string.Empty;
    }

    private void OnDisable()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }
    }

    /// duration < 0 thì dùng defaultDuration.
    public void Show(string text, float duration = -1f)
    {
        // Hội thoại đang chiếm chỗ dưới màn hình — không chồng caption lên.
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive)
        {
            Clear();
            return;
        }

        if (string.IsNullOrEmpty(text) || !enabled || !gameObject.activeInHierarchy)
        {
            Clear();
            return;
        }

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        captionText.text = text;
        SetAlpha(1f);

        if (duration < 0f) duration = defaultDuration;

        if (duration > 0f && isActiveAndEnabled) hideRoutine = StartCoroutine(HideAfter(duration));
    }

    public void Clear()
    {
        if (captionText == null) return;

        captionText.text = string.Empty;
        SetAlpha(0f);
    }

    private IEnumerator HideAfter(float duration)
    {
        yield return new WaitForSeconds(duration);

        // Chờ dialogue mở mới ẩn hẳn, tránh nhấp nháy giữa hai lớp chữ.
        while (DialogueManager.Instance != null && DialogueManager.Instance.IsActive)
            yield return null;

        yield return FadeOut();

        hideRoutine = null;
    }

    private IEnumerator FadeOut()
    {
        if (captionGroup == null)
        {
            captionText.text = string.Empty;
            yield break;
        }

        while (captionGroup.alpha > 0f)
        {
            SetAlpha(Mathf.Max(0f, captionGroup.alpha - fadeSpeed * Time.deltaTime));
            yield return null;
        }

        captionText.text = string.Empty;
    }

    private void SetAlpha(float alpha)
    {
        if (captionGroup != null)
        {
            captionGroup.alpha = alpha;
            return;
        }

        if (captionText != null)
        {
            // Không có CanvasGroup thì chỉ hiện/ẩn cả khối.
            captionText.enabled = alpha > 0f;
        }
    }
}