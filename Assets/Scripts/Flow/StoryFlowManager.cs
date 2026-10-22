using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

/// Chạy một chuỗi bước có thứ tự (flow) từ FlowDefinition.
/// Không phải nhiều script tự kích hoạt: mỗi bước có onEnter, điều kiện hoàn thành,
/// onExit, rồi mới sang bước kế. Vật trong scene tự bám bước qua FlowGate / FlowTrigger.
[DefaultExecutionOrder(-50)]
public class StoryFlowManager : MonoBehaviour
{
    public static StoryFlowManager Instance { get; private set; }

    [Tooltip("Dữ liệu luồng. Thêm/sửa bước chỉ cần sửa asset, không đụng code.")]
    [SerializeField] private FlowDefinition definition;

    [SerializeField] private bool logVerbose = true;

    [Tooltip("Hiện tên bước hiện tại ở góc màn hình (chỉ editor / development build).")]
    [SerializeField] private bool showHud = true;

    public string CurrentStepId { get; private set; }

    public event Action<string> StepEntered;
    public event Action<string> StepExited;

    private FlowStep current;
    private bool stepDone;
    private bool dialogueEndedThisStep;
    private float stepEnterTime;
    private bool started;

    private CaptionUI cachedCaption;
    private bool warnedCaption;
    private bool warnedAudio;

    public FlowDefinition Definition => definition;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{nameof(StoryFlowManager)}] Đã có StoryFlowManager khác trong scene, bỏ bản này.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (definition == null)
        {
            Debug.LogError($"[Flow] Chưa gán FlowDefinition — dừng flow.", this);
            enabled = false;
            return;
        }

        if (QuestManager.Instance == null)
        {
            Debug.LogError($"[Flow] Không có QuestManager trong scene — dừng flow.", this);
            enabled = false;
            return;
        }

        if (DialogueManager.Instance == null)
        {
            Debug.LogError($"[Flow] Không có DialogueManager trong scene — dừng flow.", this);
            enabled = false;
            return;
        }

        if (definition.steps == null || definition.steps.Count == 0)
        {
            Debug.LogError($"[Flow] FlowDefinition '{definition.name}' không có bước nào — dừng flow.", this);
            enabled = false;
            return;
        }

        started = true;
        EnterStep(definition.steps[0].id);
    }

    private void OnDisable()
    {
        // Không có subscription ngoài; callback thoại được chặn bằng stepDone.
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        HandleDebug();

        if (!started || current == null || stepDone) return;

        if (ConditionMet(current.completeWhen)) CompleteCurrentStep();
    }

    // ------------------------------------------------------------ API

    public int IndexOf(string stepId)
    {
        return definition != null ? definition.IndexOf(stepId) : -1;
    }

    public bool IsInStep(string stepId)
    {
        return !string.IsNullOrEmpty(stepId) && CurrentStepId == stepId;
    }

    /// Flow đi một chiều: đang ở bước có index >= index của stepId.
    public bool IsAtOrAfter(string stepId)
    {
        int target = IndexOf(stepId);
        if (target < 0) return false;

        int now = IndexOf(CurrentStepId);
        return now >= target;
    }

    /// Chỉ dùng để debug/test: nhảy tới bước bất kỳ.
    public void GoTo(string stepId)
    {
        if (current != null)
        {
            stepDone = true;
            Log($"Exit {current.id} (GoTo)");
            RunActions(current.onExit);
            StepExited?.Invoke(current.id);
            current = null;
        }

        EnterStep(stepId);
    }

    /// Cho vật tương tác chạy chung một danh sách hành động.
    public void RunActions(List<FlowAction> actions)
    {
        if (actions == null) return;

        for (int i = 0; i < actions.Count; i++)
        {
            RunAction(actions[i]);
        }
    }

    // ------------------------------------------------------------ vòng đời bước

    private void EnterStep(string stepId)
    {
        FlowStep step = definition.GetStep(stepId);
        if (step == null)
        {
            Debug.LogError($"[Flow] Không có bước '{stepId}' trong '{definition.name}' — dừng flow.", this);
            current = null;
            return;
        }

        current = step;
        CurrentStepId = step.id;
        stepDone = false;
        dialogueEndedThisStep = false;
        stepEnterTime = Time.time;

        Log($"Enter {step.id}");
        RunActions(step.onEnter);
        StepEntered?.Invoke(step.id);
    }

    private void CompleteCurrentStep()
    {
        FlowStep step = current;
        if (step == null || stepDone) return;

        stepDone = true;
        string nextId = step.nextStepId;

        Log($"Exit {step.id}");
        RunActions(step.onExit);
        StepExited?.Invoke(step.id);
        current = null;

        if (string.IsNullOrEmpty(nextId))
        {
            Log("Flow kết thúc.");
            return;
        }

        EnterStep(nextId);
    }

    // ------------------------------------------------------------ điều kiện

    private bool ConditionMet(FlowCondition condition)
    {
        if (condition == null) return false;

        switch (condition.type)
        {
            case FlowConditionType.Immediate:
                return true;

            case FlowConditionType.Never:
                return false;

            case FlowConditionType.Delay:
                return Time.time - stepEnterTime >= Mathf.Max(0f, condition.seconds);

            case FlowConditionType.Signal:
                return FlowBus.WasRaised(condition.signal);

            case FlowConditionType.NightFallen:
                return FlowBus.WasRaised("night_fallen");

            case FlowConditionType.DialogueEnded:
                return dialogueEndedThisStep;

            case FlowConditionType.QuestStarted:
                {
                    QuestState state = GetQuestState(condition.questId);
                    return state == QuestState.Active || state == QuestState.Completed;
                }

            case FlowConditionType.QuestCompleted:
                return GetQuestState(condition.questId) == QuestState.Completed;

            case FlowConditionType.ObjectiveReached:
                return ObjectiveReached(condition.questId, condition.objectiveId);

            default:
                return false;
        }
    }

    private static QuestState GetQuestState(string questId)
    {
        QuestManager manager = QuestManager.Instance;
        if (manager == null || string.IsNullOrEmpty(questId)) return QuestState.Locked;
        return manager.GetState(questId);
    }

    private static bool ObjectiveReached(string questId, string objectiveId)
    {
        QuestManager manager = QuestManager.Instance;
        if (manager == null || string.IsNullOrEmpty(questId)) return false;

        QuestState state = manager.GetState(questId);
        if (state == QuestState.Completed) return true;
        if (state != QuestState.Active) return false;

        if (string.IsNullOrEmpty(objectiveId)) return false;

        // QuestManager: IsObjectiveOpen = quest active và mục tiêu chưa đạt.
        return !manager.IsObjectiveOpen(questId, objectiveId);
    }

    // ------------------------------------------------------------ hành động

    private void RunAction(FlowAction action)
    {
        if (action == null) return;

        switch (action.type)
        {
            case FlowActionType.StartQuest:
                {
                    QuestManager manager = QuestManager.Instance;
                    if (manager == null)
                    {
                        Debug.LogError($"[Flow] Thiếu QuestManager — bỏ qua StartQuest '{action.text}'.", this);
                        break;
                    }
                    manager.StartQuest(action.text);
                    break;
                }

            case FlowActionType.ShowDialogue:
                ShowDialogue(action);
                break;

            case FlowActionType.PlaySfx:
                {
                    AudioManager audio = AudioManager.Instance;
                    if (audio == null)
                    {
                        if (!warnedAudio)
                        {
                            warnedAudio = true;
                            Debug.LogWarning($"[Flow] Thiếu AudioManager — bỏ qua PlaySfx. Flow vẫn chạy.", this);
                        }
                        break;
                    }
                    audio.TryPlay(action.text);
                    break;
                }

            case FlowActionType.SetLighting:
                SetLighting(action);
                break;

            case FlowActionType.RaiseSignal:
                FlowBus.Raise(action.text);
                break;

            case FlowActionType.SetActive:
                if (action.target == null)
                {
                    Debug.LogWarning($"[Flow] SetActive thiếu 'target' — bỏ qua.", this);
                    break;
                }
                action.target.SetActive(action.boolValue);
                break;

            case FlowActionType.ShowCaption:
                {
                    CaptionUI caption = ResolveCaption();
                    if (caption == null)
                    {
                        if (!warnedCaption)
                        {
                            warnedCaption = true;
                            Debug.LogWarning($"[Flow] Thiếu CaptionUI — bỏ qua ShowCaption. Flow vẫn chạy.", this);
                        }
                        break;
                    }
                    caption.Show(action.text);
                    break;
                }
        }
    }

    private void ShowDialogue(FlowAction action)
    {
        DialogueManager manager = DialogueManager.Instance;
        if (manager == null)
        {
            Debug.LogError($"[Flow] Thiếu DialogueManager — bỏ qua ShowDialogue.", this);
            dialogueEndedThisStep = true;
            return;
        }

        if (action.dialogue == null)
        {
            Debug.LogError($"[Flow] ShowDialogue thiếu DialogueData — bỏ qua.", this);
            dialogueEndedThisStep = true;
            return;
        }

        manager.ShowDialogue(action.dialogue, () => dialogueEndedThisStep = true);
    }

    private void SetLighting(FlowAction action)
    {
        LightingManager lighting = FindFirstObjectByType<LightingManager>();
        if (lighting == null)
        {
            Debug.LogWarning($"[Flow] Thiếu LightingManager — bỏ qua SetLighting.", this);
            return;
        }

        switch (action.lightingState)
        {
            case LightingState.Day:
                lighting.SetDay();
                break;

            case LightingState.Dusk:
                if (action.immediate) lighting.SetDusk();
                else lighting.StartTransitionToDusk();
                break;

            case LightingState.Night:
                if (action.immediate) lighting.SetNight();
                else lighting.StartTransitionToNight();
                break;
        }
    }

    private CaptionUI ResolveCaption()
    {
        if (cachedCaption == null) cachedCaption = FindFirstObjectByType<CaptionUI>();
        return cachedCaption;
    }

    private void Log(string message)
    {
        if (logVerbose) Debug.Log($"[Flow] {message}", this);
    }

    // ------------------------------------------------------------ debug

    private void HandleDebug()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Keyboard.current == null) return;

        if (Keyboard.current.f8Key.wasPressedThisFrame)
        {
            Debug.Log($"[Flow] Bước hiện tại: {CurrentStepId} (điều kiện: {current?.completeWhen?.type})", this);
        }

        if (Keyboard.current.f7Key.wasPressedThisFrame && current != null && !string.IsNullOrEmpty(current.nextStepId))
        {
            Debug.Log($"[Flow] F7 → {current.nextStepId}", this);
            GoTo(current.nextStepId);
        }
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void OnGUI()
    {
        if (!showHud || string.IsNullOrEmpty(CurrentStepId)) return;

        GUI.Label(new Rect(10f, 10f, 460f, 24f), $"[Flow] {CurrentStepId}");
    }
#endif
}
