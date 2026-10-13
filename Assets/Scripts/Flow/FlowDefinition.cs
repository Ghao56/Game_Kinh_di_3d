using System;
using System.Collections.Generic;
using UnityEngine;

/// Điều kiện để một bước được coi là hoàn thành.
public enum FlowConditionType
{
    Immediate,
    Delay,
    Signal,
    QuestStarted,
    QuestCompleted,
    ObjectiveReached,
    DialogueEnded,
    NightFallen,
    Never
}

[Serializable]
public class FlowCondition
{
    public FlowConditionType type = FlowConditionType.Immediate;

    [Tooltip("Signal: tên tín hiệu FlowBus.Raise.")]
    public string signal = string.Empty;

    [Tooltip("QuestStarted / QuestCompleted / ObjectiveReached: id quest.")]
    public string questId = string.Empty;

    [Tooltip("ObjectiveReached: id mục tiêu trong quest.")]
    public string objectiveId = string.Empty;

    [Tooltip("Delay: số giây chờ kể từ khi vào bước.")]
    [Min(0f)] public float seconds = 1f;
}

/// Hành động chạy khi vào / ra bước, hoặc khi vật FlowInteractable được dùng.
public enum FlowActionType
{
    StartQuest,
    ShowDialogue,
    PlaySfx,
    SetLighting,
    RaiseSignal,
    SetActive,
    ShowCaption
}

[Serializable]
public class FlowAction
{
    public FlowActionType type = FlowActionType.RaiseSignal;

    [Tooltip("StartQuest: questId. ShowDialogue: (bỏ trống). PlaySfx: key âm thanh. " +
             "RaiseSignal: tên tín hiệu. ShowCaption: nội dung caption.")]
    public string text = string.Empty;

    [Tooltip("ShowDialogue: asset thoại.")]
    public DialogueData dialogue;

    [Tooltip("SetActive: object bị bật/tắt.")]
    public GameObject target;

    [Tooltip("SetActive: true = bật, false = tắt.")]
    public bool boolValue = true;

    [Tooltip("SetLighting: mốc ánh sáng.")]
    public LightingState lightingState = LightingState.Dusk;

    [Tooltip("SetLighting: true = đổi ngay, false = chuyển mượt.")]
    public bool immediate;
}

[Serializable]
public class FlowStep
{
    [Tooltip("Khoá duy nhất của bước, ví dụ S0_Intro.")]
    public string id = string.Empty;

    [TextArea(1, 4)] [Tooltip("Ghi chú cho người làm nội dung, không hiện trong game.")]
    public string note = string.Empty;

    public FlowCondition completeWhen = new FlowCondition();

    public List<FlowAction> onEnter = new List<FlowAction>();
    public List<FlowAction> onExit = new List<FlowAction>();

    [Tooltip("Bước kế tiếp. Để trống = dừng flow.")]
    public string nextStepId = string.Empty;
}

[CreateAssetMenu(fileName = "Flow_Chapter1", menuName = "Flow/Flow Definition")]
public class FlowDefinition : ScriptableObject
{
    [TextArea(1, 40)] [Tooltip("Ghi chú cho người làm nội dung.")]
    public string notes = string.Empty;

    public List<FlowStep> steps = new List<FlowStep>();

    public FlowStep GetStep(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        for (int i = 0; i < steps.Count; i++)
        {
            FlowStep step = steps[i];
            if (step != null && step.id == id) return step;
        }

        return null;
    }

    /// Vị trí của bước trong danh sách. -1 nếu không có.
    public int IndexOf(string id)
    {
        if (string.IsNullOrEmpty(id)) return -1;

        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] != null && steps[i].id == id) return i;
        }

        return -1;
    }
}
