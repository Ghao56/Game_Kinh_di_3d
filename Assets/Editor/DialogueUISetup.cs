using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Dựng UI thoại trong scene mà không đụng tay vào YAML và không xoá UI hiện có.
/// Chạy lại nhiều lần vẫn an toàn (tìm object theo tên, tái dùng nếu đã có).
public static class DialogueUISetup
{
    private const string BlipPath = "Assets/Sound/Dialogue_Blip.wav";
    private const string FontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/BeVietnamPro-Regular SDF.asset";

    private const string CanvasName = "DialogueCanvas";
    private const string PanelName = "DialoguePanel";
    private const string SpeakerName = "Speaker Text";
    private const string BodyName = "Dialogue Text";
    private const string IndicatorName = "Continue Indicator";
    private const string ManagerName = "DialogueManager";

    private const int SortingOrder = 100;
    private const float PanelHeight = 220f;
    private const float PanelBottom = 40f;
    private const float SideMargin = 48f;
    private const float SpeakerTopMargin = 24f;
    private const float SpeakerHeight = 44f;
    private const float SpeakerAreaHeight = SpeakerTopMargin + SpeakerHeight + 12f;

    [MenuItem("Tools/Setup Dialogue UI")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[DialogueUISetup] Không chạy được khi đang play.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.isLoaded || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogError("[DialogueUISetup] Hãy mở scene đã lưu (vd TestCutscreen.unity) trước khi chạy.");
            return;
        }

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (font == null)
            Debug.LogWarning($"[DialogueUISetup] Chưa có font '{FontAssetPath}'. " +
                             "Chạy Tools/Setup Dialogue Font trước, hoặc gán font tay sau.");

        GameObject canvas = EnsureCanvas();
        GameObject panel = EnsurePanel(canvas);
        CanvasGroup group = EnsureCanvasGroup(panel);
        TMP_Text speaker = EnsureSpeaker(panel, font);
        TMP_Text body = EnsureBody(panel, font);
        Image indicator = EnsureIndicator(panel);
        TMPCharJump charJump = body.GetComponent<TMPCharJump>();
        if (charJump == null)
        {
            Undo.AddComponent<TMPCharJump>(body.gameObject);
            charJump = body.GetComponent<TMPCharJump>();
        }

        DialogueManager manager = EnsureManager();

        var so = new SerializedObject(manager);
        SetRef(so, "panelRoot", panel);
        SetRef(so, "panelGroup", group);
        SetRef(so, "speakerText", speaker);
        SetRef(so, "bodyText", body);
        SetRef(so, "charJump", charJump);
        SetRef(so, "continueIndicator", indicator);

        AudioSource source = manager.GetComponent<AudioSource>();
        if (source == null)
        {
            Undo.AddComponent<AudioSource>(manager.gameObject);
            source = manager.GetComponent<AudioSource>();
        }
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        SetRef(so, "blipSource", source);

        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(BlipPath);
        if (clip != null) SetRef(so, "blipClip", clip);
        else Debug.LogWarning($"[DialogueUISetup] Không có '{BlipPath}', thoại sẽ chạy không có tiếng.");

        // 0 = blip trên mọi ký tự (kiểu Undertale). Scene cũ còn lưu 0.045 nên phải ghi lại.
        var blipInterval = so.FindProperty("minBlipInterval");
        if (blipInterval != null)
        {
            blipInterval.floatValue = 0f;
        }

        // minBlipInterval = 0 nghĩa là không throttle, nhưng để an toàn thì AudioSource
        // phải thật sự phát được tiếng.
        source.mute = false;
        source.volume = 1f;
        source.loop = false;
        source.bypassEffects = false;
        source.bypassListenerEffects = false;
        source.outputAudioMixerGroup = null;

        var controller = UnityEngine.Object.FindFirstObjectByType<ThirdPersonController>();
        if (controller != null) SetRef(so, "controller", controller);
        var cameraRig = UnityEngine.Object.FindFirstObjectByType<ThirdPersonCamera>();
        if (cameraRig != null) SetRef(so, "cameraRig", cameraRig);
        var interactor = UnityEngine.Object.FindFirstObjectByType<Interactor>();
        if (interactor != null) SetRef(so, "interactor", interactor);
        var prompt = UnityEngine.Object.FindFirstObjectByType<InteractPromptUI>();
        if (prompt != null) SetRef(so, "promptUI", prompt);

        so.ApplyModifiedPropertiesWithoutUndo();

        EnsureEventSystem();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[DialogueUISetup] Xong trong '{scene.path}'. " +
                  $"Canvas sortingOrder={SortingOrder}, panel cao {PanelHeight}px, " +
                  $"font={(font != null ? font.name : "CHƯA GÁN")}, blip={(clip != null ? clip.name : "null")}.");
    }

    private static void SetRef(SerializedObject so, string property, Object value)
    {
        SerializedProperty prop = so.FindProperty(property);
        if (prop == null)
        {
            Debug.LogWarning($"[DialogueUISetup] DialogueManager không có field '{property}'.");
            return;
        }
        prop.objectReferenceValue = value;
    }

    private static GameObject EnsureCanvas()
    {
        var existing = GameObject.Find(CanvasName);
        if (existing != null) return existing;

        var go = new GameObject(CanvasName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create Dialogue Canvas");

        var canvas = Undo.AddComponent<Canvas>(go);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        var scaler = Undo.AddComponent<CanvasScaler>(go);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Undo.AddComponent<GraphicRaycaster>(go);
        return go;
    }

    private static GameObject EnsurePanel(GameObject canvas)
    {
        Transform found = canvas.transform.Find(PanelName);
        if (found != null) return found.gameObject;

        var go = new GameObject(PanelName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create Dialogue Panel");
        go.transform.SetParent(canvas.transform, false);

        SetStretch(go, new Vector2(0f, 0f), new Vector2(1f, 0f),
                   new Vector2(SideMargin, PanelBottom), new Vector2(-SideMargin, PanelBottom + PanelHeight));

        var image = Undo.AddComponent<Image>(go);
        image.color = new Color(0f, 0f, 0f, 0.72f);
        image.raycastTarget = false;
        return go;
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject panel)
    {
        var group = panel.GetComponent<CanvasGroup>();
        if (group != null) return group;

        group = Undo.AddComponent<CanvasGroup>(panel);
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        return group;
    }

    private static TMP_Text EnsureSpeaker(GameObject panel, TMP_FontAsset font)
    {
        Transform found = panel.transform.Find(SpeakerName);
        GameObject go;
        if (found != null)
        {
            go = found.gameObject;
        }
        else
        {
            go = new GameObject(SpeakerName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create Speaker Text");
            go.transform.SetParent(panel.transform, false);

            SetStretch(go, new Vector2(0f, 1f), new Vector2(1f, 1f),
                       new Vector2(SideMargin, -(SpeakerTopMargin + SpeakerHeight)),
                       new Vector2(-SideMargin, -SpeakerTopMargin));
        }

        var text = go.GetComponent<TextMeshProUGUI>();
        if (text == null) text = Undo.AddComponent<TextMeshProUGUI>(go);

        text.font = font;
        text.fontSize = 30f;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(1f, 0.84f, 0.4f);
        text.alignment = TextAlignmentOptions.Left;
        text.raycastTarget = false;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.text = string.Empty;
        return text;
    }

    private static TMP_Text EnsureBody(GameObject panel, TMP_FontAsset font)
    {
        Transform found = panel.transform.Find(BodyName);
        GameObject go;
        if (found != null)
        {
            go = found.gameObject;
        }
        else
        {
            go = new GameObject(BodyName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create Dialogue Text");
            go.transform.SetParent(panel.transform, false);

            SetStretch(go, Vector2.zero, Vector2.one,
                       new Vector2(SideMargin, PanelBottom),
                       new Vector2(-SideMargin, -SpeakerAreaHeight));
        }

        var text = go.GetComponent<TextMeshProUGUI>();
        if (text == null) text = Undo.AddComponent<TextMeshProUGUI>(go);

        text.font = font;
        text.fontSize = 28f;
        text.color = new Color(1f, 1f, 1f, 1f);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.text = string.Empty;
        return text;
    }

    private static Image EnsureIndicator(GameObject panel)
    {
        Transform found = panel.transform.Find(IndicatorName);
        GameObject go;
        if (found != null)
        {
            go = found.gameObject;
        }
        else
        {
            go = new GameObject(IndicatorName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create Continue Indicator");
            go.transform.SetParent(panel.transform, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(30f, 18f);
            rect.anchoredPosition = new Vector2(-SideMargin, PanelBottom + 14f);
        }

        var image = go.GetComponent<Image>();
        if (image == null) image = Undo.AddComponent<Image>(go);
        image.color = new Color(1f, 1f, 1f, 0.8f);
        image.raycastTarget = false;
        image.sprite = null;
        image.enabled = false;
        return image;
    }

    private static DialogueManager EnsureManager()
    {
        var existing = UnityEngine.Object.FindFirstObjectByType<DialogueManager>();
        if (existing != null) return existing;

        var go = new GameObject(ManagerName);
        Undo.RegisterCreatedObjectUndo(go, "Create Dialogue Manager");
        return Undo.AddComponent<DialogueManager>(go);
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
        Debug.LogWarning("[DialogueUISetup] Scene chưa có EventSystem.");
    }

    private static void SetStretch(GameObject go, Vector2 anchorMin, Vector2 anchorMax,
                                  Vector2 offsetMin, Vector2 offsetMax)
    {
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}