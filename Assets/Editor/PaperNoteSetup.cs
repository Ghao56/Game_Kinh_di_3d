using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PaperNoteSetup
{
    private const string NoteReaderName = "NoteReader";
    private const string PaperA = "Paper 1";
    private const string PaperB = "Paper 2";

    private const string NoteReaderContentA =
        "Mảnh giấy này đã nằm ở đây từ rất lâu.\n\n" +
        "Ai đó đã từng cầm nó, đọc nó, rồi không bao giờ quay trở lại.\n" +
        "Đừng đi theo tiếng gọi ở cuối hành lang.\n" +
        "Nó không phải là tiếng của người mà con rối kia từng nói.\n\n" +
        "Nếu nghe thấy tiếng bước chân phía sau, đừng quay đầu.";

    private const string NoteReaderContentB =
        "Căn nhà này không có cửa sổ, nhưng vẫn có thể nhìn thấy mọi thứ.\n\n" +
        "Người giữ cửa sẽ nói dối về con đường thoát.\n" +
        "Chìa khóa thật nằm dưới tấm sàn lỏng lẻo, không phải trong ngăn kéo.\n" +
        "Nếu nghe thấy tiếng gõ, đừng mở.";

    [MenuItem("Tools/Setup Paper Note")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[PaperNoteSetup] Dừng Play mode trước khi chạy Setup.");
            return;
        }

        string scenePath = "Assets/Scenes/SampleScene.unity";
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path != scenePath)
        {
            if (!string.IsNullOrEmpty(activeScene.path) || activeScene.rootCount > 0)
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        InputActionAsset actions =
            AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");

        Canvas canvas = EnsureCanvas();
        EnsureEventSystem();
        NoteReaderUI noteUI = EnsureNoteReaderUI(canvas, actions);

        CreatePaper(PaperA, new Vector3(3.1f, 0.49f, 16.908f), 0f, NoteReaderContentA, noteUI);
        CreatePaper(PaperB, new Vector3(4.12f, 0.49f, 16.908f), 90f, NoteReaderContentB, noteUI);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (Application.isBatchMode)
        {
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("[PaperNoteSetup] Đã lưu SampleScene.");
        }
        Debug.Log("[PaperNoteSetup] Hoàn tất. Vào Play để kiểm tra.");
    }

    private static Canvas EnsureCanvas()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas != null) return canvas;

        GameObject canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
        return canvasGO.GetComponent<Canvas>();
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;

        GameObject eventSystemGO = new GameObject("EventSystem", typeof(EventSystem));
        Undo.RegisterCreatedObjectUndo(eventSystemGO, "Create EventSystem");
        eventSystemGO.AddComponent<InputSystemUIInputModule>();
    }

    private static NoteReaderUI EnsureNoteReaderUI(Canvas canvas, InputActionAsset actions)
    {
        Transform existingRoot = canvas.transform.Find(NoteReaderName);
        if (existingRoot != null)
        {
            NoteReaderUI existing = existingRoot.GetComponent<NoteReaderUI>();
            if (existing != null) return existing;
            return Undo.AddComponent<NoteReaderUI>(existingRoot.gameObject);
        }

        GameObject rootGO = new GameObject(NoteReaderName,
            typeof(RectTransform), typeof(CanvasGroup), typeof(NoteReaderUI));
        Undo.RegisterCreatedObjectUndo(rootGO, "Create " + NoteReaderName);
        rootGO.transform.SetParent(canvas.transform, false);

        RectTransform rootRt = rootGO.GetComponent<RectTransform>();
        SetStretch(rootRt);

        CanvasGroup rootGroup = rootGO.GetComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;

        NoteReaderUI noteUI = rootGO.GetComponent<NoteReaderUI>();

        GameObject dimGO = CreateUIGameObject("Dim", typeof(Image), rootRt);
        Image dimImage = dimGO.GetComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.75f);
        dimImage.raycastTarget = false;
        SetStretch(dimGO.GetComponent<RectTransform>());

        GameObject paperGO = CreateUIGameObject("Paper", typeof(Image), rootRt);
        RectTransform paperRt = paperGO.GetComponent<RectTransform>();
        paperRt.anchorMin = new Vector2(0.5f, 0.5f);
        paperRt.anchorMax = new Vector2(0.5f, 0.5f);
        paperRt.pivot = new Vector2(0.5f, 0.5f);
        paperRt.sizeDelta = new Vector2(520f, 720f);
        paperRt.anchoredPosition = Vector2.zero;
        Image paperImage = paperGO.GetComponent<Image>();
        paperImage.color = new Color(0.93f, 0.90f, 0.80f, 1f);
        paperImage.raycastTarget = false;

        GameObject contentGO = CreateUIGameObject("Content", typeof(Text), paperRt);
        RectTransform contentRt = contentGO.GetComponent<RectTransform>();
        contentRt.anchorMin = Vector2.zero;
        contentRt.anchorMax = Vector2.one;
        contentRt.offsetMin = new Vector2(45f, 45f);
        contentRt.offsetMax = new Vector2(-45f, -45f);
        Text contentText = contentGO.GetComponent<Text>();
        contentText.font = GetFont();
        contentText.fontSize = 28;
        contentText.color = new Color(0.12f, 0.09f, 0.05f, 1f);
        contentText.alignment = TextAnchor.UpperLeft;
        contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
        contentText.verticalOverflow = VerticalWrapMode.Truncate;
        contentText.lineSpacing = 1.3f;

        rootRt.SetAsLastSibling();

        SerializedObject so = new SerializedObject(noteUI);
        so.FindProperty("contentText").objectReferenceValue = contentText;
        so.FindProperty("controller").objectReferenceValue = Object.FindFirstObjectByType<ThirdPersonController>();
        so.FindProperty("cameraRig").objectReferenceValue = Object.FindFirstObjectByType<ThirdPersonCamera>();
        so.FindProperty("interactor").objectReferenceValue = Object.FindFirstObjectByType<Interactor>();
        so.FindProperty("promptUI").objectReferenceValue = Object.FindFirstObjectByType<InteractPromptUI>();
        so.FindProperty("actions").objectReferenceValue = actions;
        so.ApplyModifiedPropertiesWithoutUndo();

        return noteUI;
    }

    private static void CreatePaper(string objectName, Vector3 position, float rotationY,
        string content, NoteReaderUI noteUI)
    {
        if (GameObject.Find(objectName) != null)
        {
            Debug.Log($"[PaperNoteSetup] Đã có '{objectName}' trong Scene, bỏ qua.");
            return;
        }

        GameObject paperGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(paperGO, "Create " + objectName);
        paperGO.name = objectName;
        paperGO.transform.position = position;
        paperGO.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        paperGO.transform.localScale = new Vector3(0.3f, 0.02f, 0.4f);

        PaperNote paper = Undo.AddComponent<PaperNote>(paperGO);
        Undo.AddComponent<InteractableGlow>(paperGO);

        SerializedObject so = new SerializedObject(paper);
        so.FindProperty("noteUI").objectReferenceValue = noteUI;
        so.FindProperty("noteContent").stringValue = content;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreateUIGameObject(string name, System.Type uiType, RectTransform parent)
    {
        GameObject uiGO = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), uiType);
        Undo.RegisterCreatedObjectUndo(uiGO, "Create " + name);
        uiGO.transform.SetParent(parent, false);
        return uiGO;
    }

    private static void SetStretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    private static Font GetFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }
}