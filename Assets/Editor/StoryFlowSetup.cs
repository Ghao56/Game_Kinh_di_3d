using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Dựng luồng chương 1 cho scene MHoang: Flow_Chapter1.asset, StoryFlowManager,
/// FlowGate cho các pad, FlowTrigger cho Pad_MotherCare, FlowInteractable cho Cube "Mở".
/// Idempotent: chạy lại không nhân bản, không ghi đè dữ liệu người dùng đã sửa.
public static class StoryFlowSetup
{
    private const string ScenePath = "Assets/Scenes/MHoang.unity";
    private const string FlowFolder = "Assets/Flow";
    private const string FlowPath = FlowFolder + "/Flow_Chapter1.asset";
    private const string TestDialoguePath = "Assets/Dialogue/Dialogue_Test.asset";
    private const string MotherTalkPath = "Assets/Dialogue/Dialogue_MotherTalk.asset";

    private const string FlowManagerName = "Story Flow";
    private const string CubeName = "Cube";
    private const string MotherPadName = "Pad_MotherCare";
    private const string ExplorePadName = "Pad_ExploreHouse";

    private const string S0 = "S0_Intro";
    private const string S1 = "S1_Explore";
    private const string S2 = "S2_GoToMother";
    private const string S3 = "S3_MotherCare";
    private const string S4 = "S4_MotherTalk";
    private const string S5 = "S5_Dusk";
    private const string S6 = "S6_Night";
    private const string S7 = "S7_Final";

    private static readonly string[] RoomPads = { "Pad_Phong1", "Pad_Phong2", "Pad_Phong3" };

    [MenuItem("Tools/Setup Story Flow")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[StoryFlowSetup] Dừng Play mode trước khi chạy Setup.");
            return;
        }

        OpenSceneIfNeeded();

        DialogueData testDialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(TestDialoguePath);
        if (testDialogue == null)
            Debug.LogWarning($"[StoryFlowSetup] Không thấy '{TestDialoguePath}' — bước S0 sẽ thiếu thoại.");

        DialogueData motherTalk = EnsureMotherTalk();
        FlowDefinition definition = EnsureDefinition(testDialogue, motherTalk);

        EnsureManager(definition);
        ConfigureRoomGates();
        ConfigureMotherTrigger();
        ConfigureCube();

        int disabled = DisableLegacyComponents();

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        int gates = Object.FindObjectsByType<FlowGate>(FindObjectsSortMode.None).Length;
        int triggers = Object.FindObjectsByType<FlowTrigger>(FindObjectsSortMode.None).Length;
        Debug.Log($"[Flow] Setup: {definition.steps.Count} bước, {gates} gate, {triggers} trigger, " +
                  $"{disabled} component cũ bị tắt. Đã lưu '{ScenePath}'.");
    }

    private static void OpenSceneIfNeeded()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path == ScenePath) return;

        if (!string.IsNullOrEmpty(active.path) || active.rootCount > 0)
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    // ------------------------------------------------------------ asset

    private static FlowDefinition EnsureDefinition(DialogueData testDialogue, DialogueData motherTalk)
    {
        FlowDefinition existing = AssetDatabase.LoadAssetAtPath<FlowDefinition>(FlowPath);
        if (existing != null)
        {
            Debug.Log($"[StoryFlowSetup] Đã có '{FlowPath}', giữ nguyên dữ liệu (không ghi đè).");
            return existing;
        }

        if (!AssetDatabase.IsValidFolder(FlowFolder))
            AssetDatabase.CreateFolder("Assets", "Flow");

        FlowDefinition definition = ScriptableObject.CreateInstance<FlowDefinition>();
        definition.name = "Flow_Chapter1";
        definition.notes = "Luồng chương 1 — scene MHoang. Sửa bước ở đây, không cần đụng code.";
        definition.steps = BuildSteps(testDialogue, motherTalk);

        AssetDatabase.CreateAsset(definition, FlowPath);
        AssetDatabase.SaveAssets();
        return definition;
    }

    private static DialogueData EnsureMotherTalk()
    {
        DialogueData existing = AssetDatabase.LoadAssetAtPath<DialogueData>(MotherTalkPath);
        if (existing != null) return existing;

        DialogueData data = ScriptableObject.CreateInstance<DialogueData>();
        data.name = "Dialogue_MotherTalk";
        data.notes = "Placeholder: viết lời mẹ dặn 'đừng mở cửa' vào dòng dưới.";
        data.lines = new[]
        {
            new DialogueLine { speaker = "Mẹ", text = string.Empty }
        };

        AssetDatabase.CreateAsset(data, MotherTalkPath);
        AssetDatabase.SaveAssets();
        return data;
    }

    private static System.Collections.Generic.List<FlowStep> BuildSteps(DialogueData testDialogue, DialogueData motherTalk)
    {
        return new System.Collections.Generic.List<FlowStep>
        {
            new FlowStep
            {
                id = S0, note = "Mở đầu: thoại 'Vào đi…'",
                completeWhen = Condition(FlowConditionType.DialogueEnded),
                onEnter = { Dialogue(testDialogue) },
                nextStepId = S1
            },
            new FlowStep
            {
                id = S1, note = "Bắt đầu quest khám phá nhà",
                completeWhen = Condition(FlowConditionType.QuestCompleted, questId: "explore_house"),
                onEnter = { Action(FlowActionType.StartQuest, "explore_house") },
                nextStepId = S2
            },
            new FlowStep
            {
                id = S2, note = "Đi tới phòng mẹ (Pad_MotherCare phát enter_mother_room)",
                completeWhen = Condition(FlowConditionType.Signal, signal: "enter_mother_room"),
                onEnter = { Action(FlowActionType.ShowCaption, "Đi đến phòng mẹ.") },
                nextStepId = S3
            },
            new FlowStep
            {
                id = S3, note = "Bắt đầu quest chăm sóc mẹ (3 mục tiêu tự do)",
                completeWhen = Condition(FlowConditionType.QuestCompleted, questId: "mother_care"),
                onEnter = { Action(FlowActionType.StartQuest, "mother_care") },
                nextStepId = S4
            },
            new FlowStep
            {
                id = S4, note = "Mẹ dặn đừng mở cửa",
                completeWhen = Condition(FlowConditionType.DialogueEnded),
                onEnter = { Dialogue(motherTalk) },
                nextStepId = S5
            },
            new FlowStep
            {
                id = S5, note = "Chạng vạng (LightingManager không có event dusk xong nên chờ 12s)",
                completeWhen = Condition(FlowConditionType.Delay, seconds: 12f),
                onEnter = { Lighting(LightingState.Dusk, false) },
                nextStepId = S6
            },
            new FlowStep
            {
                id = S6, note = "Đêm xuống + tiếng gõ cửa",
                completeWhen = Condition(FlowConditionType.NightFallen),
                onEnter = { Lighting(LightingState.Night, false), Action(FlowActionType.PlaySfx, "knock_door") },
                nextStepId = S7
            },
            new FlowStep
            {
                id = S7, note = "Bước cuối: Cube 'Mở' dùng được một lần",
                completeWhen = Condition(FlowConditionType.Never),
                nextStepId = string.Empty
            }
        };
    }

    private static FlowCondition Condition(FlowConditionType type, string signal = "", string questId = "",
        string objectiveId = "", float seconds = 0f)
    {
        return new FlowCondition
        {
            type = type,
            signal = signal,
            questId = questId,
            objectiveId = objectiveId,
            seconds = seconds
        };
    }

    private static FlowAction Action(FlowActionType type, string text)
    {
        return new FlowAction { type = type, text = text };
    }

    private static FlowAction Dialogue(DialogueData data)
    {
        return new FlowAction { type = FlowActionType.ShowDialogue, dialogue = data };
    }

    private static FlowAction Lighting(LightingState state, bool immediate)
    {
        return new FlowAction { type = FlowActionType.SetLighting, lightingState = state, immediate = immediate };
    }

    // ------------------------------------------------------------ scene

    private static void EnsureManager(FlowDefinition definition)
    {
        StoryFlowManager manager = Object.FindFirstObjectByType<StoryFlowManager>();
        if (manager == null)
        {
            GameObject go = new GameObject(FlowManagerName);
            Undo.RegisterCreatedObjectUndo(go, "Create " + FlowManagerName);
            manager = Undo.AddComponent<StoryFlowManager>(go);
        }

        SerializedObject so = new SerializedObject(manager);
        SerializedProperty prop = so.FindProperty("definition");
        if (prop.objectReferenceValue == null) prop.objectReferenceValue = definition;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manager);
    }

    private static void ConfigureRoomGates()
    {
        foreach (string padName in RoomPads)
        {
            GameObject pad = GameObject.Find(padName);
            if (pad == null)
            {
                Debug.LogWarning($"[StoryFlowSetup] Không thấy '{padName}' trong scene.");
                continue;
            }

            FlowGate gate = pad.GetComponent<FlowGate>();
            if (gate == null) gate = Undo.AddComponent<FlowGate>(pad);

            SerializedObject so = new SerializedObject(gate);
            so.FindProperty("activeFromStep").stringValue = S1;
            so.FindProperty("activeUntilStep").stringValue = S2;
            so.FindProperty("hideWhenInactive").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gate);
        }
    }

    private static void ConfigureMotherTrigger()
    {
        GameObject pad = GameObject.Find(MotherPadName);
        if (pad == null)
        {
            Debug.LogWarning($"[StoryFlowSetup] Không thấy '{MotherPadName}' trong scene.");
            return;
        }

        QuestZone legacy = pad.GetComponent<QuestZone>();
        if (legacy != null) legacy.enabled = false;

        FlowTrigger trigger = pad.GetComponent<FlowTrigger>();
        if (trigger == null) trigger = Undo.AddComponent<FlowTrigger>(pad);

        SerializedObject so = new SerializedObject(trigger);
        so.FindProperty("signal").stringValue = "enter_mother_room";
        so.FindProperty("requiredStepId").stringValue = S2;
        so.FindProperty("fireOnce").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(trigger);
    }

    private static void ConfigureCube()
    {
        GameObject cube = GameObject.Find(CubeName);
        if (cube == null)
        {
            Debug.LogWarning($"[StoryFlowSetup] Không thấy '{CubeName}' trong scene.");
            return;
        }

        Interactable legacy = cube.GetComponent<Interactable>();
        if (legacy != null) Undo.DestroyObjectImmediate(legacy);

        if (cube.GetComponent<InteractableGlow>() == null) Undo.AddComponent<InteractableGlow>(cube);

        FlowInteractable interactable = cube.GetComponent<FlowInteractable>();
        if (interactable == null) interactable = Undo.AddComponent<FlowInteractable>(cube);

        SerializedObject so = new SerializedObject(interactable);
        so.FindProperty("promptText").stringValue = "Mở";
        so.FindProperty("holdToInteract").boolValue = false;
        so.FindProperty("requiredStepId").stringValue = S7;
        so.FindProperty("signalOnInteract").stringValue = "open_door";
        so.FindProperty("oneShot").boolValue = true;

        SerializedProperty actions = so.FindProperty("actions");
        actions.arraySize = 2;
        SetAction(actions.GetArrayElementAtIndex(0), FlowActionType.RaiseSignal, "open_door");
        SetAction(actions.GetArrayElementAtIndex(1), FlowActionType.PlaySfx, "door_creak");

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(interactable);
    }

    private static void SetAction(SerializedProperty element, FlowActionType type, string text)
    {
        element.FindPropertyRelative("type").enumValueIndex = (int)type;
        element.FindPropertyRelative("text").stringValue = text;
        element.FindPropertyRelative("boolValue").boolValue = true;
    }

    /// Tắt các nguồn cũ để flow là nguồn duy nhất: QuestZone của Pad_ExploreHouse,
    /// QuestStarter, và các DialogueTrigger_Test.
    private static int DisableLegacyComponents()
    {
        int count = 0;

        GameObject explorePad = GameObject.Find(ExplorePadName);
        if (explorePad != null)
        {
            QuestZone zone = explorePad.GetComponent<QuestZone>();
            if (zone != null && zone.enabled) { zone.enabled = false; count++; }
        }

        QuestStarter[] starters = Object.FindObjectsByType<QuestStarter>(FindObjectsSortMode.None);
        foreach (QuestStarter starter in starters)
        {
            if (starter.enabled) { starter.enabled = false; count++; }
        }

        DialogueTrigger[] triggers = Object.FindObjectsByType<DialogueTrigger>(FindObjectsSortMode.None);
        foreach (DialogueTrigger trigger in triggers)
        {
            if (trigger.enabled) { trigger.enabled = false; count++; }
        }

        return count;
    }
}
