using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Màn hình kết thúc: nền đen full-screen + dòng chữ chính giữa
// ("BẠN ĐÃ TRẢ HẾT NỢ"). Tự dựng bằng code (Canvas/CanvasGroup/Image/Text cùng
// stack UI với QuestTrackerUI nên không cần prefab trong scene).
//
// Ràng buộc đoạn kết:
//  - fade bằng Time.unscaledDeltaTime: vẫn chạy mượt kể cả khi timeScale = 0;
//  - fade chỉ chạy đúng một lần (chống kích hoạt fade nhiều lần);
//  - chữ CHỈ xuất hiện sau khi màn hình đã đen hoàn toàn;
//  - khi đen hoàn tất: tuỳ chọn Time.timeScale = 0 -> mọi gameplay phía sau
//    đứng im; KHÔNG tự chuyển scene.
public class EndScreenUI : MonoBehaviour
{
    public const string DefaultEndText = "B\u1EA0N \u0110\u00C3 TR\u1EA2 H\u1EBET N\u1EE2";

    private CanvasGroup blackGroup;
    private Text endText;
    private bool built;
    private bool playStarted;

    public bool PlayStarted => playStarted;

    public static EndScreenUI Create()
    {
        GameObject go = new GameObject("EndingScreenUI", typeof(Canvas), typeof(CanvasGroup));
        EndScreenUI ui = go.AddComponent<EndScreenUI>();
        ui.Build();
        return ui;
    }

    // Dựng UI: nền đen che hết màn hình + chữ trắng giữa màn hình (ẩn sẵn).
    private void Build()
    {
        if (built) return;
        built = true;

        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3000; // đè lên mọi HUD khác

        blackGroup = GetComponent<CanvasGroup>();
        blackGroup.alpha = 0f;
        blackGroup.interactable = false;
        blackGroup.blocksRaycasts = false;

        Stretch((RectTransform)transform);

        GameObject blackGo = CreateChild("Black", (RectTransform)transform);
        Image blackImage = blackGo.AddComponent<Image>();
        blackImage.color = Color.black;
        blackImage.raycastTarget = false;
        Stretch(blackGo.GetComponent<RectTransform>());

        GameObject textGo = CreateChild("EndText", (RectTransform)transform);
        endText = textGo.AddComponent<Text>();
        endText.text = DefaultEndText;
        endText.font = GetFont();
        endText.fontSize = 64;
        endText.fontStyle = FontStyle.Bold;
        endText.alignment = TextAnchor.MiddleCenter;
        endText.color = Color.white;
        endText.raycastTarget = false;
        textGo.SetActive(false);
        Stretch(textGo.GetComponent<RectTransform>());
    }

    /// <summary>
    /// Tiện gọi không cần coroutine: khởi xướng fade một lần duy nhất.
    /// </summary>
    public void Play(string message, float seconds, bool freezeTime, Action onBlack)
    {
        if (playStarted) return;

        StartCoroutine(PlayRoutine(message, seconds, freezeTime, onBlack));
    }

    /// <summary>
    /// Fade sang đen rồi hiện chữ kết. Đã nhấn PlayRoutine một lần thì gọi thêm bỏ qua.
    /// </summary>
    public IEnumerator PlayRoutine(string message, float seconds, bool freezeTime, Action onBlack = null)
    {
        if (playStarted) yield break;
        playStarted = true;

        // 1) Fade sang đen mượt; unscaledDeltaTime nên độc lập Time.timeScale.
        float duration = Mathf.Max(0.01f, seconds);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            blackGroup.alpha = Mathf.Clamp01(t / duration);
            yield return null;
        }

        blackGroup.alpha = 1f;

        // 2) Đen hoàn toàn: dừng mọi gameplay phía sau rồi chữ mới xuất hiện.
        if (freezeTime) Time.timeScale = 0f;

        endText.text = string.IsNullOrEmpty(message) ? DefaultEndText : message;
        endText.gameObject.SetActive(true);

        if (onBlack != null) onBlack.Invoke();
    }

    // ------------------------------------------------------------ dựng scene-ui

    private static GameObject CreateChild(string name, RectTransform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        return go;
    }

    // Kéo full màn hình bằng neo 0..1, offset = 0.
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }

    // Cùng font với HUD quest (LegacyRuntime, fallback Arial) để chữ Việt hiển thị tốt.
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
