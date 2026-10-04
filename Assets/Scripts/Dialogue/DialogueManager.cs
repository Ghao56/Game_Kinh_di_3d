using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// Điều khiển hội thoại: gõ từng chữ, hiệu ứng blip, khoá nhân vật, xếp hàng nhiều đoạn thoại.
/// Đặt trong scene (không DontDestroyOnLoad). Mọi thời gian dùng unscaled nên chạy được khi game pause.
public class DialogueManager : MonoBehaviour
{
    private struct PendingDialogue
    {
        public DialogueLine[] Lines;
        public Action OnComplete;
    }

    public static DialogueManager Instance { get; private set; }

    public static event Action OnDialogueStarted;
    public static event Action OnDialogueEnded;

    [Header("Giao diện")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMPCharJump charJump;
    [Tooltip("Ảnh nhấp nháy báo hiệu có thể bấm để tiếp. Để trống thì bỏ qua.")]
    [SerializeField] private Image continueIndicator;

    [Header("Âm thanh")]
    [SerializeField] private AudioSource blipSource;
    [Tooltip("Để trống thì không phát tiếng.")]
    [SerializeField] private AudioClip blipClip;
    [Tooltip("Khoảng cách tối thiểu giữa hai tiếng blip. 0 = kêu blip trên MỌI ký tự (kiểu Undertale). " +
             "Tăng lên nếu thấy tiếng bị chồng lên nhau.")]
    [SerializeField] private float minBlipInterval = 0f;

    [Header("Khoá nhân vật")]
    [SerializeField] private ThirdPersonController controller;
    [SerializeField] private ThirdPersonCamera cameraRig;
    [SerializeField] private Interactor interactor;
    [SerializeField] private InteractPromptUI promptUI;

    [Header("Điều khiển")]
    [Tooltip("Phím để hiện hết dòng / sang dòng tiếp. Bỏ E nếu trùng với hành động khác.")]
    [SerializeField] private Key[] advanceKeys = { Key.Space, Key.Enter, Key.E };
    [SerializeField] private bool allowMouseAdvance = true;

    [Header("Hiệu ứng")]
    [Min(0f)] [SerializeField] private float fadeDuration = 0.15f;
    [Tooltip("Biên độ nhảy của chữ. 0 = tắt hiệu ứng nhảy.")]
    [SerializeField] private float jumpHeight = 10f;
    [SerializeField] private float continuePulseSpeed = 6f;

    private readonly List<PendingDialogue> queue = new List<PendingDialogue>();

    private bool isActive;
    private int openedFrame = -1;
    private float nextBlipTime;

    private bool lockCaptured;

    public bool IsActive => isActive;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"[{nameof(DialogueManager)}] Đã có DialogueManager trong scene.", this);
            enabled = false;
            return;
        }
        Instance = this;

        if (charJump == null) charJump = bodyText != null ? bodyText.GetComponent<TMPCharJump>() : null;

        if (panelRoot == null || panelGroup == null || bodyText == null || charJump == null)
        {
            Debug.LogError($"[{nameof(DialogueManager)}] Thiếu panelRoot / panelGroup / bodyText / charJump.", this);
            enabled = false;
            return;
        }

        if (controller == null) controller = FindFirstObjectByType<ThirdPersonController>();
        if (cameraRig == null) cameraRig = FindFirstObjectByType<ThirdPersonCamera>();
        if (interactor == null) interactor = FindFirstObjectByType<Interactor>();
        if (promptUI == null) promptUI = FindFirstObjectByType<InteractPromptUI>();

        if (blipSource != null)
        {
            blipSource.playOnAwake = false;
            blipSource.spatialBlend = 0f;
        }

        panelGroup.alpha = 0f;
        panelGroup.interactable = false;
        panelGroup.blocksRaycasts = false;
        panelRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnDisable()
    {
        if (!isActive) return;
        StopAllCoroutines();
        Finish();
    }

    public void ShowDialogue(DialogueData data, Action onComplete = null)
    {
        if (data == null)
        {
            Debug.LogError($"[{nameof(DialogueManager)}] DialogueData null.", this);
            return;
        }
        ShowLines(data.lines, onComplete);
    }

    public void ShowLines(IList<DialogueLine> lines, Action onComplete = null)
    {
        if (lines == null || lines.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        var arr = new DialogueLine[lines.Count];
        for (int i = 0; i < lines.Count; i++) arr[i] = lines[i];
        Enqueue(arr, onComplete);
    }

    public void ShowLine(string text, string speaker = null, Action onComplete = null)
    {
        var line = new DialogueLine
        {
            text = text ?? string.Empty,
            speaker = speaker ?? string.Empty
        };
        Enqueue(new[] { line }, onComplete);
    }

    public void ClearQueue()
    {
        queue.Clear();
    }

    private void Enqueue(DialogueLine[] lines, Action onComplete)
    {
        if (lines.Length == 0)
        {
            onComplete?.Invoke();
            return;
        }

        bool alreadyRunning = isActive;
        queue.Add(new PendingDialogue { Lines = lines, OnComplete = onComplete });
        if (alreadyRunning) return;

        isActive = true;
        openedFrame = Time.frameCount;
        AcquireControl();
        OnDialogueStarted?.Invoke();
        StartCoroutine(RunQueue());
    }

    private IEnumerator RunQueue()
    {
        yield return Fade(1f);

        while (queue.Count > 0)
        {
            PendingDialogue req = queue[0];
            queue.RemoveAt(0);

            if (req.Lines != null)
            {
                for (int i = 0; i < req.Lines.Length; i++)
                {
                    if (req.Lines[i] == null) continue;
                    yield return PlayLine(req.Lines[i]);
                }
            }

            req.OnComplete?.Invoke();
        }

        yield return Fade(0f);
        Finish();
    }

    private void Finish()
    {
        isActive = false;
        queue.Clear();
        SetContinueVisible(false);
        ReleaseControl();
        OnDialogueEnded?.Invoke();
    }

    private IEnumerator PlayLine(DialogueLine line)
    {
        if (speakerText != null)
        {
            bool hasSpeaker = !string.IsNullOrEmpty(line.speaker);
            if (hasSpeaker)
            {
                if (speakerText.text != line.speaker.Trim())
                {
                    speakerText.text = line.speaker.Trim();
                }
                if (!speakerText.gameObject.activeSelf)
                {
                    speakerText.gameObject.SetActive(true);
                }
            }
            else
            {
                // Giữ tên speaker hiển thị nếu dòng trước cũng cùng speaker
                // (không clear text khi trống)
            }
        }

        bodyText.text = line.text ?? string.Empty;
        bodyText.maxVisibleCharacters = 0;
        bodyText.ForceMeshUpdate();

        int total = bodyText.textInfo.characterCount;
        charJump.ResetRevealTimes(total);
        charJump.Configure(jumpHeight, charJump.JumpDuration);

        bool skipped = false;

        for (int i = 0; i < total; i++)
        {
            if (ConsumeAdvance())
            {
                skipped = true;
                break;
            }

            bodyText.maxVisibleCharacters = i + 1;
            charJump.MarkRevealed(i);
            TryPlayBlip(i, line);

            float delay = GetDelay(i, line);
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        }

        SetContinueVisible(true);

        if (!skipped)
        {
            while (true)
            {
                if (ConsumeAdvance()) break;
                yield return null;
            }
        }

        bodyText.maxVisibleCharacters = total;
        charJump.SettleFrom(0);
        SetContinueVisible(false);
    }

    private float GetDelay(int index, DialogueLine line)
    {
        TMP_TextInfo info = bodyText.textInfo;
        if (index < 0 || index >= info.characterCount) return line.charDelay;

        char c = info.characterInfo[index].character;

        if (c == '\n') return line.newlineDelay;
        if (IsComma(c)) return line.commaDelay;
        if (IsSentence(c)) return NextIsSentence(index) ? line.commaDelay * 0.6f : line.sentenceDelay;
        if (c == ' ') return line.spaceDelay;

        float jitter = Mathf.Clamp01(line.jitter);
        return line.charDelay * UnityEngine.Random.Range(1f - jitter, 1f + jitter);
    }

    private bool NextIsSentence(int index)
    {
        TMP_TextInfo info = bodyText.textInfo;
        for (int i = index + 1; i < info.characterCount; i++)
        {
            char c = info.characterInfo[i].character;
            if (c == ' ' || c == '\n') continue;
            return IsSentence(c);
        }
        return false;
    }

    private void TryPlayBlip(int index, DialogueLine line)
    {
        if (blipSource == null || blipClip == null) return;

        TMP_TextInfo info = bodyText.textInfo;
        if (index < 0 || index >= info.characterCount) return;

        TMP_CharacterInfo ci = info.characterInfo[index];
        if (!ShouldPlayBlip(ci.character, line)) return;

        if (minBlipInterval > 0f && Time.unscaledTime < nextBlipTime) return;
        nextBlipTime = Time.unscaledTime + minBlipInterval;

        float min = line.blipPitchRange.x;
        float max = line.blipPitchRange.y;
        blipSource.pitch = Mathf.Approximately(min, max) ? min : UnityEngine.Random.Range(min, max);
        blipSource.PlayOneShot(blipClip, line.blipVolume);
    }

    /// Quyết định có kêu blip cho ký tự này không.
    /// Không dựa vào TMP_CharacterInfo.isVisible: textInfo chỉ cập nhật ở lần dựng mesh kế tiếp,
    /// nên khi vừa đặt maxVisibleCharacters = i + 1 thì ký tự i vẫn còn isVisible = false và
    /// mọi tiếng blip đều bị bỏ qua. Chỉ cần xét chính ký tự là đủ.
    public static bool ShouldPlayBlip(char c, DialogueLine line)
    {
        if (line == null || line.blipVolume <= 0f) return false;
        if (c == '\n' || c == '\r') return false;
        if (char.IsWhiteSpace(c)) return line.blipOnSpaces;
        if (IsComma(c) || IsSentence(c)) return false;

        // Bỏ qua ký tự điều khiển và khoảng trắng không in.
        if (char.IsControl(c)) return false;

        return true;
    }

    private static bool IsComma(char c) => c == ',' || c == ';' || c == ':' || c == '-';

    private static bool IsSentence(char c) => c == '.' || c == '!' || c == '?' || c == '…';

    private bool ConsumeAdvance()
    {
        if (Time.frameCount == openedFrame) return false;
        if (!AdvancePressed()) return false;
        openedFrame = Time.frameCount;
        return true;
    }

    private bool AdvancePressed()
    {
        if (Keyboard.current != null)
        {
            for (int i = 0; i < advanceKeys.Length; i++)
            {
                if (advanceKeys[i] == Key.None) continue;
                if (Keyboard.current[advanceKeys[i]].wasPressedThisFrame) return true;
            }
        }

        return allowMouseAdvance && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    private IEnumerator Fade(float target)
    {
        if (target > 0f && !panelRoot.activeSelf) panelRoot.SetActive(true);

        while (Mathf.Abs(panelGroup.alpha - target) > 0.001f)
        {
            float speed = fadeDuration > 0f ? Time.unscaledDeltaTime / fadeDuration : 1f;
            panelGroup.alpha = Mathf.MoveTowards(panelGroup.alpha, target, speed);
            yield return null;
        }

        panelGroup.alpha = target;
        if (target <= 0f) panelRoot.SetActive(false);
    }

    private void SetContinueVisible(bool visible)
    {
        if (continueIndicator == null) return;
        continueIndicator.enabled = visible;
    }

    private void Update()
    {
        if (continueIndicator == null || !continueIndicator.enabled) return;
        float a = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * continuePulseSpeed));
        var c = continueIndicator.color;
        c.a = a;
        continueIndicator.color = c;
    }

    private void AcquireControl()
    {
        if (lockCaptured) return;
        lockCaptured = true;

        // Không khoá nhân vật/camera/interactor - chỉ hiển thị UI thoại
        promptUI?.Hide();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ReleaseControl()
    {
        if (!lockCaptured) return;
        lockCaptured = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        minBlipInterval = Mathf.Max(0f, minBlipInterval);
        fadeDuration = Mathf.Max(0f, fadeDuration);
        jumpHeight = Mathf.Max(0f, jumpHeight);
        continuePulseSpeed = Mathf.Max(0f, continuePulseSpeed);
    }
#endif
}