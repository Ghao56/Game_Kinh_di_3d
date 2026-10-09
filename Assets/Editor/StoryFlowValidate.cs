using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Kiểm tra luồng chương 1 và dây nối trong scene MHoang. Chạy lại nhiều lần, không sửa gì.
public static class StoryFlowValidate
{
    private const string ScenePath = "Assets/Scenes/MHoang.unity";

    private static int pass;
    private static int fail;

    [MenuItem("Tools/Validate Story Flow")]
    public static void Run()
    {
        pass = 0;
        fail = 0;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.isLoaded || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogError("[StoryFlowValidate] Hãy mở scene đã lưu (Assets/Scenes/MHoang.unity) trước khi chạy.");
            return;
        }

        StoryFlowManager[] managers = Object.FindObjectsByType<StoryFlowManager>(FindObjectsSortMode.None);
        Check(managers.Length == 1, $"Đúng 1 StoryFlowManager (đang có {managers.Length})");
        if (managers.Length != 1)
        {
            Report();
            return;
        }

        FlowDefinition definition = managers[0].Definition;
        Check(definition != null, "StoryFlowManager đã gán FlowDefinition");
        if (definition == null)
        {
            Report();
            return;
        }

        List<FlowStep> steps = definition.steps ?? new List<FlowStep>();
        Check(steps.Count > 0, $"FlowDefinition có bước ({steps.Count})");

        // ids
        HashSet<string> ids = new HashSet<string>();
        foreach (FlowStep step in steps)
        {
            bool nonEmpty = step != null && !string.IsNullOrEmpty(step.id);
            Check(nonEmpty, $"Bước có id: '{(step != null ? step.id : "<null>")}'");
            if (!nonEmpty) continue;

            Check(ids.Add(step.id), $"Id không trùng: {step.id}");
        }

        Check(steps.Count > 0 && steps[0] != null && steps[0].id == "S0_Intro", "Bước đầu là S0_Intro");

        // nextStepId + orphan
        HashSet<string> referenced = new HashSet<string>();
        foreach (FlowStep step in steps)
        {
            if (step == null) continue;

            if (string.IsNullOrEmpty(step.nextStepId))
            {
                Check(step == steps[steps.Count - 1], $"Chỉ bước cuối được trống nextStepId (đang ở {step.id})");
                continue;
            }

            Check(ids.Contains(step.nextStepId), $"{step.id}.nextStepId='{step.nextStepId}' tồn tại");
            referenced.Add(step.nextStepId);
        }

        for (int i = 1; i < steps.Count; i++)
        {
            if (steps[i] == null) continue;
            Check(referenced.Contains(steps[i].id), $"Không mồ côi: {steps[i].id} được bước khác trỏ tới");
        }

        // quest registry
        Dictionary<string, QuestDefinition> quests = BuildQuestRegistry(out bool hasQuestManager);
        Check(hasQuestManager, "Có QuestManager trong scene");

        // audio keys
        HashSet<string> audioKeys = new HashSet<string>();
        foreach (SfxLibrary.KeySpec spec in SfxLibrary.ExpectedKeys) audioKeys.Add(spec.Key);

        // action checks
        foreach (FlowStep step in steps)
        {
            if (step == null) continue;
            CheckActions($"{step.id}.onEnter", step.onEnter, quests, audioKeys);
            CheckActions($"{step.id}.onExit", step.onExit, quests, audioKeys);
            CheckCondition($"{step.id}.completeWhen", step.completeWhen, quests);
        }

        // scene wiring
        CheckSceneGates(ids, quests, audioKeys);
        CheckLegacyZones(definition);

        Report();
    }

    private static void CheckActions(string where, List<FlowAction> actions, Dictionary<string, QuestDefinition> quests,
        HashSet<string> audioKeys)
    {
        if (actions == null) return;

        foreach (FlowAction action in actions)
        {
            if (action == null) continue;

            switch (action.type)
            {
                case FlowActionType.StartQuest:
                    Check(quests.ContainsKey(action.text), $"{where}: StartQuest '{action.text}' có trong All Quests");
                    break;

                case FlowActionType.ShowDialogue:
                    Check(action.dialogue != null, $"{where}: ShowDialogue có DialogueData");
                    break;

                case FlowActionType.PlaySfx:
                    Check(audioKeys.Contains(action.text), $"{where}: PlaySfx '{action.text}' có trong SfxLibrary");
                    break;

                case FlowActionType.RaiseSignal:
                    Check(!string.IsNullOrEmpty(action.text), $"{where}: RaiseSignal có tên tín hiệu");
                    break;
            }
        }
    }

    private static void CheckCondition(string where, FlowCondition condition, Dictionary<string, QuestDefinition> quests)
    {
        if (condition == null) return;

        switch (condition.type)
        {
            case FlowConditionType.QuestStarted:
            case FlowConditionType.QuestCompleted:
                Check(quests.ContainsKey(condition.questId), $"{where}: quest '{condition.questId}' có trong All Quests");
                break;

            case FlowConditionType.ObjectiveReached:
                Check(quests.TryGetValue(condition.questId, out QuestDefinition quest) &&
                      quest.GetObjective(condition.objectiveId) != null,
                      $"{where}: mục tiêu '{condition.questId}/{condition.objectiveId}' tồn tại");
                break;
        }
    }

    private static void CheckSceneGates(HashSet<string> ids, Dictionary<string, QuestDefinition> quests,
        HashSet<string> audioKeys)
    {
        FlowGate[] gates = Object.FindObjectsByType<FlowGate>(FindObjectsSortMode.None);
        foreach (FlowGate gate in gates)
        {
            SerializedObject so = new SerializedObject(gate);
            string from = so.FindProperty("activeFromStep").stringValue;
            string until = so.FindProperty("activeUntilStep").stringValue;
            Check(string.IsNullOrEmpty(from) || ids.Contains(from), $"{gate.name}.FlowGate.activeFromStep hợp lệ");
            Check(string.IsNullOrEmpty(until) || ids.Contains(until), $"{gate.name}.FlowGate.activeUntilStep hợp lệ");
        }

        FlowTrigger[] triggers = Object.FindObjectsByType<FlowTrigger>(FindObjectsSortMode.None);
        foreach (FlowTrigger trigger in triggers)
        {
            SerializedObject so = new SerializedObject(trigger);
            string step = so.FindProperty("requiredStepId").stringValue;
            Check(string.IsNullOrEmpty(step) || ids.Contains(step), $"{trigger.name}.FlowTrigger.requiredStepId hợp lệ");
        }

        FlowInteractable[] interactables = Object.FindObjectsByType<FlowInteractable>(FindObjectsSortMode.None);
        foreach (FlowInteractable interactable in interactables)
        {
            SerializedObject so = new SerializedObject(interactable);
            string step = so.FindProperty("requiredStepId").stringValue;
            Check(string.IsNullOrEmpty(step) || ids.Contains(step), $"{interactable.name}.FlowInteractable.requiredStepId hợp lệ");

            SerializedProperty actions = so.FindProperty("actions");
            for (int i = 0; i < actions.arraySize; i++)
            {
                SerializedProperty element = actions.GetArrayElementAtIndex(i);
                var type = (FlowActionType)element.FindPropertyRelative("type").enumValueIndex;
                string text = element.FindPropertyRelative("text").stringValue;

                if (type == FlowActionType.StartQuest)
                    Check(quests.ContainsKey(text), $"{interactable.name}.FlowInteractable action StartQuest '{text}' có trong All Quests");
                else if (type == FlowActionType.PlaySfx)
                    Check(audioKeys.Contains(text), $"{interactable.name}.FlowInteractable action PlaySfx '{text}' có trong SfxLibrary");
            }
        }
    }

    private static void CheckLegacyZones(FlowDefinition definition)
    {
        HashSet<string> flowStartQuests = new HashSet<string>();
        foreach (FlowStep step in definition.steps)
        {
            if (step == null || step.onEnter == null) continue;
            foreach (FlowAction action in step.onEnter)
            {
                if (action != null && action.type == FlowActionType.StartQuest) flowStartQuests.Add(action.text);
            }
        }

        QuestZone[] zones = Object.FindObjectsByType<QuestZone>(FindObjectsSortMode.None);
        foreach (QuestZone zone in zones)
        {
            if (!zone.enabled) continue;

            SerializedObject so = new SerializedObject(zone);
            bool isStart = so.FindProperty("mode").enumValueIndex == (int)QuestZone.ZoneMode.StartQuest;
            if (!isStart) continue;

            string questId = so.FindProperty("questId").stringValue;
            Check(!flowStartQuests.Contains(questId),
                $"QuestZone '{zone.name}' mode StartQuest trùng quest '{questId}' với flow (phải tắt)");
        }
    }

    private static Dictionary<string, QuestDefinition> BuildQuestRegistry(out bool hasManager)
    {
        var result = new Dictionary<string, QuestDefinition>();

        QuestManager manager = Object.FindFirstObjectByType<QuestManager>();
        hasManager = manager != null;
        if (manager == null) return result;

        SerializedObject so = new SerializedObject(manager);
        SerializedProperty all = so.FindProperty("allQuests");
        if (all == null || !all.isArray) return result;

        for (int i = 0; i < all.arraySize; i++)
        {
            QuestDefinition quest = all.GetArrayElementAtIndex(i).objectReferenceValue as QuestDefinition;
            if (quest == null || string.IsNullOrEmpty(quest.QuestId)) continue;
            result[quest.QuestId] = quest;
        }

        return result;
    }

    /// Mở đúng scene rồi chạy — dùng cho batchmode/CLI.
    public static void RunBatch()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Run();
    }

    private static void Check(bool ok, string label)
    {
        if (ok) pass++;
        else fail++;
        Debug.Log($"[StoryFlowValidate] {(ok ? "PASS" : "FAIL")} - {label}");
    }

    private static void Report()
    {
        Debug.Log($"[StoryFlowValidate] ===== {pass} pass, {fail} fail =====");
        if (fail > 0) Debug.LogError($"[StoryFlowValidate] CÓ {fail} mục CHƯA ĐẠT.");
    }
}
