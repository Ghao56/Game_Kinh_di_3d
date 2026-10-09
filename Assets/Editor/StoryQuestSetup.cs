using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// Dựng sẵn hạ tầng quest cho màn đầu: QuestManager + 2 asset nhiệm vụ (4.1, 4.2),
/// HUD theo dõi và QuestStarter tạm. Zone/interactable phải đặt tay theo vị trí trong map.
public static class StoryQuestSetup
{
    private const string QuestFolder = "Assets/Quests";
    private const string ExploreQuestPath = QuestFolder + "/Quest_ExploreHouse.asset";
    private const string MotherQuestPath = QuestFolder + "/Quest_MotherCare.asset";

    private const string TrackerName = "Quest Tracker";
    private const string StarterName = "Quest Starters";
    private const string ManagerName = "Quest Manager";

    private const string ExploreQuestId = "explore_house";
    private const string MotherQuestId = "mother_care";

    private struct ObjectiveData
    {
        public string Id;
        public string Text;
        public int Required;

        public ObjectiveData(string id, string text, int required)
        {
            Id = id;
            Text = text;
            Required = required;
        }
    }

    [MenuItem("Tools/Setup Story Quests")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[StoryQuestSetup] Dừng Play mode trước khi chạy Setup.");
            return;
        }

        const string scenePath = "Assets/Scenes/MHoang.unity";
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path != scenePath)
        {
            if (!string.IsNullOrEmpty(activeScene.path) || activeScene.rootCount > 0)
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        QuestDefinition explore = CreateExploreQuest();
        QuestDefinition mother = CreateMotherQuest();

        EnsureQuestManager(explore, mother);
        EnsureQuestTracker();
        EnsureQuestStarter();
        QuestZoneSetup.SetupZones();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (Application.isBatchMode)
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log("[StoryQuestSetup] Xong. Kéo 5 QuestZone về vị trí thật, " +
                  "còn phải đặt tay: 3 QuestObjectiveInteractable cho mother_care.");
    }

    private static QuestDefinition CreateExploreQuest()
    {
        QuestDefinition quest = LoadOrCreate(ExploreQuestPath);

        SerializedObject so = new SerializedObject(quest);
        so.FindProperty("questId").stringValue = ExploreQuestId;
        so.FindProperty("displayTitle").stringValue = "Đi quanh nhà";
        so.FindProperty("nextQuest").objectReferenceValue = null;
        SetObjectives(so, new[]
        {
            new ObjectiveData("explore", "Khám phá các phòng trong nhà", 3)
        });
        so.ApplyModifiedPropertiesWithoutUndo();
        return quest;
    }

    private static QuestDefinition CreateMotherQuest()
    {
        QuestDefinition quest = LoadOrCreate(MotherQuestPath);

        SerializedObject so = new SerializedObject(quest);
        so.FindProperty("questId").stringValue = MotherQuestId;
        so.FindProperty("displayTitle").stringValue = "Chăm sóc mẹ";
        so.FindProperty("nextQuest").objectReferenceValue = null;
        SetObjectives(so, new[]
        {
            new ObjectiveData("get_water", "Lấy nước cho mẹ", 1),
            new ObjectiveData("prepare_medicine", "Chuẩn bị thuốc", 1),
            new ObjectiveData("check_window", "Kiểm tra cửa sổ phòng mẹ", 1)
        });
        so.ApplyModifiedPropertiesWithoutUndo();
        return quest;
    }

    private static QuestDefinition LoadOrCreate(string assetPath)
    {
        QuestDefinition quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(assetPath);
        if (quest != null) return quest;

        if (!AssetDatabase.IsValidFolder(QuestFolder))
            AssetDatabase.CreateFolder("Assets", "Quests");

        quest = ScriptableObject.CreateInstance<QuestDefinition>();
        AssetDatabase.CreateAsset(quest, assetPath);
        AssetDatabase.SaveAssets();
        return quest;
    }

    private static void SetObjectives(SerializedObject so, ObjectiveData[] data)
    {
        SerializedProperty array = so.FindProperty("objectives");
        Dictionary<string, int> previousRequired = ReadRequiredById(array);

        array.arraySize = data.Length;

        for (int i = 0; i < data.Length; i++)
        {
            SerializedProperty element = array.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("id").stringValue = data[i].Id;
            element.FindPropertyRelative("text").stringValue = data[i].Text;
            element.FindPropertyRelative("required").intValue = previousRequired.TryGetValue(data[i].Id, out int keep)
                ? keep
                : data[i].Required;
        }
    }

    /// Giữ lại số Required người dùng đã chỉnh trong Inspector (mục tiêu khám phá có thể
    /// đổi 1-3 tuỳ số phòng thật), chỉ dùng giá trị mặc định khi tạo mục tiêu mới.
    private static Dictionary<string, int> ReadRequiredById(SerializedProperty array)
    {
        Dictionary<string, int> result = new Dictionary<string, int>();
        for (int i = 0; i < array.arraySize; i++)
        {
            SerializedProperty element = array.GetArrayElementAtIndex(i);
            string id = element.FindPropertyRelative("id").stringValue;
            if (string.IsNullOrEmpty(id)) continue;
            result[id] = element.FindPropertyRelative("required").intValue;
        }

        return result;
    }

    private static void EnsureQuestManager(QuestDefinition explore, QuestDefinition mother)
    {
        QuestManager manager = Object.FindFirstObjectByType<QuestManager>();
        if (manager == null)
        {
            GameObject managerGO = new GameObject(ManagerName);
            Undo.RegisterCreatedObjectUndo(managerGO, "Create " + ManagerName);
            manager = Undo.AddComponent<QuestManager>(managerGO);
        }

        SerializedObject so = new SerializedObject(manager);
        SerializedProperty allQuests = so.FindProperty("allQuests");
        UpsertQuest(allQuests, explore);
        UpsertQuest(allQuests, mother);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void UpsertQuest(SerializedProperty list, QuestDefinition quest)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == quest) return;
        }

        int index = list.arraySize;
        list.InsertArrayElementAtIndex(index);
        list.GetArrayElementAtIndex(index).objectReferenceValue = quest;
    }

    private static void EnsureQuestTracker()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[StoryQuestSetup] Scene chưa có Canvas.", null);
            return;
        }

        Transform existingRoot = canvas.transform.Find(TrackerName);
        GameObject rootGO;
        if (existingRoot != null)
        {
            rootGO = existingRoot.gameObject;
        }
        else
        {
            rootGO = new GameObject(TrackerName,
                typeof(RectTransform), typeof(CanvasGroup), typeof(QuestTrackerUI));
            Undo.RegisterCreatedObjectUndo(rootGO, "Create " + TrackerName);
            rootGO.transform.SetParent(canvas.transform, false);
        }

        RectTransform rootRt = rootGO.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(1f, 1f);
        rootRt.anchorMax = new Vector2(1f, 1f);
        rootRt.pivot = new Vector2(1f, 1f);
        rootRt.anchoredPosition = new Vector2(-24f, -24f);
        rootRt.sizeDelta = new Vector2(460f, 180f);

        CanvasGroup group = GetOrAdd<CanvasGroup>(rootGO);
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        Image background = GetOrAdd<Image>(rootGO);
        background.color = new Color(0f, 0f, 0f, 0.55f);
        background.raycastTarget = false;

        VerticalLayoutGroup layout = GetOrAdd<VerticalLayoutGroup>(rootGO);
        layout.padding = new RectOffset(16, 16, 10, 12);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = GetOrAdd<ContentSizeFitter>(rootGO);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Transform legacy = rootRt.Find("Objectives");
        if (legacy != null) Undo.DestroyObjectImmediate(legacy.gameObject);

        Text title = EnsureTrackerTitle(rootRt);
        RectTransform rows = EnsureTrackerRows(rootRt);

        rootRt.SetAsLastSibling();

        SerializedObject so = new SerializedObject(GetOrAdd<QuestTrackerUI>(rootGO));
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("rowsRoot").objectReferenceValue = rows;
        so.FindProperty("backgroundImage").objectReferenceValue = background;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Text EnsureTrackerTitle(RectTransform root)
    {
        Transform child = root.Find("Title");
        GameObject titleGO = child != null ? child.gameObject : CreateUIGameObject("Title", root);

        Text title = titleGO.GetComponent<Text>();
        title.font = GetFont();
        title.fontSize = 26;
        title.fontStyle = FontStyle.Bold;
        title.color = new Color(0.95f, 0.92f, 0.82f, 1f);
        title.alignment = TextAnchor.MiddleLeft;
        title.horizontalOverflow = HorizontalWrapMode.Overflow;
        title.verticalOverflow = VerticalWrapMode.Overflow;
        title.raycastTarget = false;

        LayoutElement element = GetOrAdd<LayoutElement>(titleGO);
        element.ignoreLayout = false;
        element.layoutPriority = 1;
        element.minHeight = 34f;
        element.preferredHeight = 34f;
        element.flexibleHeight = 0f;

        return title;
    }

    private static RectTransform EnsureTrackerRows(RectTransform root)
    {
        Transform child = root.Find("Rows");
        GameObject rowsGO;
        if (child != null)
        {
            rowsGO = child.gameObject;
        }
        else
        {
            rowsGO = new GameObject("Rows", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(rowsGO, "Create Rows");
            rowsGO.transform.SetParent(root, false);
        }

        RectTransform rowsRt = rowsGO.GetComponent<RectTransform>();
        rowsRt.anchorMin = new Vector2(0f, 1f);
        rowsRt.anchorMax = new Vector2(1f, 1f);
        rowsRt.pivot = new Vector2(0.5f, 1f);
        rowsRt.anchoredPosition = Vector2.zero;

        LayoutElement legacyElement = rowsGO.GetComponent<LayoutElement>();
        if (legacyElement != null) Undo.DestroyObjectImmediate(legacyElement);

        VerticalLayoutGroup rowsLayout = GetOrAdd<VerticalLayoutGroup>(rowsGO);
        rowsLayout.padding = new RectOffset(0, 0, 0, 0);
        rowsLayout.spacing = 4f;
        rowsLayout.childAlignment = TextAnchor.UpperLeft;
        rowsLayout.childControlWidth = true;
        rowsLayout.childControlHeight = true;
        rowsLayout.childForceExpandWidth = true;
        rowsLayout.childForceExpandHeight = false;

        return rowsRt;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component == null) component = Undo.AddComponent<T>(go);
        return component;
    }

    private static void EnsureQuestStarter()
    {
        if (GameObject.Find(StarterName) != null) return;

        GameObject starterGO = new GameObject(StarterName);
        Undo.RegisterCreatedObjectUndo(starterGO, "Create " + StarterName);
        QuestStarter starter = Undo.AddComponent<QuestStarter>(starterGO);

        SerializedObject so = new SerializedObject(starter);
        so.FindProperty("questId").stringValue = ExploreQuestId;
        so.FindProperty("delay").floatValue = 0.5f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreateUIGameObject(string name, RectTransform parent)
    {
        GameObject uiGO = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        Undo.RegisterCreatedObjectUndo(uiGO, "Create " + name);
        uiGO.transform.SetParent(parent, false);
        return uiGO;
    }

    private static Font GetFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }
}