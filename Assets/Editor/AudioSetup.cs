using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Dựng hệ thống âm thanh trong scene MHoang mà không đụng tay vào YAML.
/// Chạy lại nhiều lần vẫn an toàn (tìm object theo tên, tái dùng nếu đã có).
///
/// KHÔNG đụng Main.unity, Build Settings, hay các Editor tool trỏ SampleScene.
public static class AudioSetup
{
    private const string ScenePath = "Assets/Scenes/MHoang.unity";
    private const string AudioFolder = "Assets/Audio";
    private const string LibraryPath = AudioFolder + "/SfxLibrary_Main.asset";
    private const string FontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/BeVietnamPro-Regular SDF.asset";

    private const string ManagerName = "AudioManager";
    private const string CaptionCanvasName = "CaptionCanvas";
    private const string CaptionTextName = "Caption Text";
    private const string AmbienceZoneName = "AmbienceZone_Test";

    private const int CaptionSortingOrder = 90;
    private const float CaptionBottom = 284f;
    private const float CaptionHeight = 60f;
    private const float CaptionSideMargin = 48f;

    [MenuItem("Tools/Setup Audio")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[AudioSetup] Không chạy được khi đang play.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        SfxLibrary library = EnsureLibrary();
        CaptionUI captionUI = EnsureCaptionUI();
        AudioManager manager = EnsureManager(library, captionUI);
        FootstepAudio footsteps = EnsureFootstepAudio();
        AmbienceZone zone = EnsureAmbienceZone();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[AudioSetup] Xong trong '{ScenePath}'. " +
                  $"Library='{library.name}' ({library.Entries.Count} key), " +
                  $"Manager='{manager.name}', Caption={(captionUI != null ? "có" : "KHÔNG")}, " +
                  $"Footsteps={(footsteps != null ? "có" : "KHÔNG")}, " +
                  $"AmbienceZone={(zone != null ? "có" : "KHÔNG")}.");
        Debug.Log("[AudioSetup] Chưa gán AudioMixer nào — AudioManager dùng bus âm lượng nội bộ. " +
                  "Muốn định tuyến nhóm thì tạo AudioMixer (Assets > Create > Audio > Audio Mixer) " +
                  "rồi gán vào ô 'Mixer' của AudioManager.");
        Debug.Log("[AudioSetup] Chạy Tools/Audio/Report Missing Clips để xem key nào còn thiếu file.");
    }

    // ---------------------------------------------------------------- asset

    private static SfxLibrary EnsureLibrary()
    {
        EnsureFolder("Assets", "Audio");

        SfxLibrary library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);

        if (library == null)
        {
            library = ScriptableObject.CreateInstance<SfxLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        library.EnsureKeys(SfxLibrary.ExpectedKeys);
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        return library;
    }

    private static void EnsureFolder(string parent, string folder)
    {
        string path = $"{parent}/{folder}";
        if (AssetDatabase.IsValidFolder(path)) return;
        AssetDatabase.CreateFolder(parent, folder);
    }

    // ---------------------------------------------------------------- scene

    private static AudioManager EnsureManager(SfxLibrary library, CaptionUI captionUI)
    {
        AudioManager manager = UnityEngine.Object.FindFirstObjectByType<AudioManager>();

        if (manager == null)
        {
            var go = new GameObject(ManagerName);
            Undo.RegisterCreatedObjectUndo(go, "Create AudioManager");
            manager = Undo.AddComponent<AudioManager>(go);
        }

        if (manager.GetComponent<NoiseDebugView>() == null)
            Undo.AddComponent<NoiseDebugView>(manager.gameObject);

        var so = new SerializedObject(manager);
        SetRef(so, "library", library);
        SetRef(so, "captionUI", captionUI);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(manager);
        return manager;
    }

    private static CaptionUI EnsureCaptionUI()
    {
        CaptionUI existing = UnityEngine.Object.FindFirstObjectByType<CaptionUI>();
        if (existing != null) return existing;

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (font == null)
            Debug.LogWarning($"[AudioSetup] Chưa có font '{FontAssetPath}'. " +
                             "Chạy Tools/Setup Dialogue Font trước, hoặc gán font tay sau.");

        var canvasGo = new GameObject(CaptionCanvasName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Caption Canvas");

        var canvas = Undo.AddComponent<Canvas>(canvasGo);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CaptionSortingOrder;

        var scaler = Undo.AddComponent<CanvasScaler>(canvasGo);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Undo.AddComponent<GraphicRaycaster>(canvasGo);

        var group = Undo.AddComponent<CanvasGroup>(canvasGo);
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        var textGo = new GameObject(CaptionTextName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(textGo, "Create Caption Text");
        textGo.transform.SetParent(canvasGo.transform, false);

        var rect = textGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(CaptionSideMargin, CaptionBottom);
        rect.offsetMax = new Vector2(-CaptionSideMargin, CaptionBottom + CaptionHeight);

        var text = Undo.AddComponent<TextMeshProUGUI>(textGo);
        text.font = font;
        text.fontSize = 26f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.85f, 0.85f, 0.9f, 1f);
        text.raycastTarget = false;
        text.richText = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.text = string.Empty;

        CaptionUI caption = Undo.AddComponent<CaptionUI>(canvasGo);

        var so = new SerializedObject(caption);
        SetRef(so, "captionText", text);
        SetRef(so, "captionGroup", group);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(caption);
        return caption;
    }

    private static FootstepAudio EnsureFootstepAudio()
    {
        ThirdPersonController controller = UnityEngine.Object.FindFirstObjectByType<ThirdPersonController>();

        if (controller == null)
        {
            Debug.LogWarning("[AudioSetup] Không tìm thấy ThirdPersonController — bỏ qua FootstepAudio.");
            return null;
        }

        FootstepAudio existing = controller.GetComponent<FootstepAudio>();
        if (existing != null) return existing;

        return Undo.AddComponent<FootstepAudio>(controller.gameObject);
    }

    private static AmbienceZone EnsureAmbienceZone()
    {
        AmbienceZone existing = UnityEngine.Object.FindFirstObjectByType<AmbienceZone>();
        if (existing != null) return existing;

        ThirdPersonController controller = UnityEngine.Object.FindFirstObjectByType<ThirdPersonController>();
        Vector3 position = controller != null
            ? controller.transform.position + new Vector3(4f, 1f, 0f)
            : new Vector3(0f, 1f, 0f);

        var go = new GameObject(AmbienceZoneName);
        Undo.RegisterCreatedObjectUndo(go, "Create " + AmbienceZoneName);
        go.transform.position = position;

        var box = Undo.AddComponent<BoxCollider>(go);
        box.isTrigger = true;
        box.size = new Vector3(5f, 3f, 5f);

        AmbienceZone zone = Undo.AddComponent<AmbienceZone>(go);

        var so = new SerializedObject(zone);
        var logTransitions = so.FindProperty("logTransitions");
        if (logTransitions != null) logTransitions.boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log($"[AudioSetup] Tạo '{AmbienceZoneName}' ở {position} để thử crossfade. " +
                  "Gán ambienceKey/ambienceClip sau khi có file âm thanh.");

        return zone;
    }

    private static void SetRef(SerializedObject so, string property, Object value)
    {
        SerializedProperty prop = so.FindProperty(property);

        if (prop == null)
        {
            Debug.LogWarning($"[AudioSetup] Không tìm thấy field '{property}' trong {so.targetObject.GetType().Name}.");
            return;
        }

        prop.objectReferenceValue = value;
    }

    // ---------------------------------------------------------------- test / debug

    [MenuItem("Tools/Audio/Report Missing Clips")]
    public static void ReportMissingClips()
    {
        SfxLibrary library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);

        if (library == null)
        {
            Debug.LogWarning($"[AudioSetup] Chưa có '{LibraryPath}'. Chạy Tools/Setup Audio trước.");
            return;
        }

        int missing = 0;
        int ready = 0;

        foreach (SfxLibrary.Entry entry in library.Entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.key)) continue;

            if (entry.HasClip) ready++;
            else missing++;
        }

        Debug.Log($"[AudioSetup] {ready} key đã có clip, {missing} key còn trống. " +
                  "Chừa trống vẫn chạy, chỉ cảnh báo 1 lần mỗi key.");
    }

    [MenuItem("Tools/Audio/Test Noise Bus")]
    public static void TestNoiseBus()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("[AudioSetup] Test Noise Bus cần chạy trong Play mode.");
            return;
        }

        bool received = false;
        var captured = default(NoiseEvent);

        void Handler(NoiseEvent noise)
        {
            received = true;
            captured = noise;
        }

        int before = NoiseSystem.SubscriberCount;
        NoiseSystem.OnNoise += Handler;

        try
        {
            ThirdPersonController controller = UnityEngine.Object.FindFirstObjectByType<ThirdPersonController>();
            Vector3 position = controller != null
                ? controller.transform.position
                : new Vector3(0f, 1f, 0f);

            NoiseSystem.ReportNoise(position, 0.75f, 12f, null);
        }
        finally
        {
            NoiseSystem.OnNoise -= Handler;
        }

        Debug.Log($"[AudioSetup] Noise bus: subscriber trước={before}, nhận={received}, " +
                  $"pos={captured.Position}, loudness={captured.Loudness}, radius={captured.Radius}, " +
                  $"source={(captured.Source != null ? captured.Source.name : "null")}. " +
                  "Bật Noise Debug View trên AudioManager để xem vòng tròn Gizmo.");
    }
}