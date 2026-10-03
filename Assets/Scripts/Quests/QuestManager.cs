using System;
using System.Collections.Generic;
using UnityEngine;

/// Nguồn sự thật duy nhất về quest: registry, tiến độ, trạng thái.
/// Thêm nhiệm vụ mới chỉ cần tạo QuestDefinition và đưa vào All Quests.
[DefaultExecutionOrder(-100)]
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("Registry")]
    [Tooltip("Mọi QuestDefinition trong game. StartQuest(questId) tra trong danh sách này.")]
    [SerializeField] private List<QuestDefinition> allQuests = new List<QuestDefinition>();

    [Header("Debug")]
    [SerializeField] private bool logVerbose = true;

    public event Action<QuestRuntime> QuestStarted;
    public event Action<QuestRuntime> QuestProgressed;
    public event Action<QuestRuntime> QuestCompleted;

    private readonly Dictionary<string, QuestDefinition> definitions = new Dictionary<string, QuestDefinition>();
    private readonly Dictionary<string, QuestRuntime> runtimes = new Dictionary<string, QuestRuntime>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{nameof(QuestManager)}] Đã có QuestManager khác trong scene, bỏ bản này.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildRegistry();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// Bản này để kéo thẳng vào UnityEvent trong Inspector (UnityEvent, Quest Definition).
    public void StartQuest(QuestDefinition definition)
    {
        if (definition == null)
        {
            Debug.LogError($"[{nameof(QuestManager)}] StartQuest nhận definition null.", this);
            return;
        }

        StartQuest(definition.QuestId);
    }

    /// Bản này cho Inspector (string param) và cho code gọi theo id.
    public void StartQuest(string questId)
    {
        if (!definitions.TryGetValue(questId, out QuestDefinition definition))
        {
            Debug.LogError($"[{nameof(QuestManager)}] Không có quest '{questId}' trong registry (All Quests).", this);
            return;
        }

        QuestRuntime runtime = GetOrCreateRuntime(questId);

        if (runtime.State == QuestState.Completed)
        {
            Log($"'{questId}' đã hoàn thành, bỏ qua lệnh start.");
            return;
        }

        if (runtime.State == QuestState.Active)
        {
            Log($"'{questId}' đang chạy, bỏ qua lệnh start.");
            return;
        }

        runtime.State = QuestState.Active;
        Log($"Bắt đầu '{questId}' — {definition.DisplayTitle}");
        QuestStarted?.Invoke(runtime);

        if (runtime.IsAllObjectivesComplete()) Complete(runtime);
    }

    public void ReportProgress(string questId, string objectiveId, int amount = 1)
    {
        QuestRuntime runtime = GetOrCreateRuntime(questId);
        if (runtime == null)
        {
            Debug.LogError($"[Quest] Không có quest '{questId}' trong registry.", this);
            return;
        }

        if (runtime.State != QuestState.Active)
        {
            Log($"Bỏ qua tiến độ '{questId}/{objectiveId}': quest chưa active.");
            return;
        }

        QuestObjective objective = runtime.Definition.GetObjective(objectiveId);
        if (objective == null)
        {
            Debug.LogError($"[Quest] Quest '{questId}' không có mục tiêu '{objectiveId}'.", this);
            return;
        }

        int current = runtime.GetProgress(objectiveId);
        int target = Mathf.Clamp(current + amount, 0, objective.Required);
        if (target == current)
        {
            Log($"'{questId}/{objectiveId}' đã đạt tối đa {objective.Required}, bỏ qua.");
            return;
        }

        runtime.SetProgress(objectiveId, target);
        Log($"Tiến độ '{questId}/{objectiveId}': {target}/{objective.Required}");
        QuestProgressed?.Invoke(runtime);

        if (runtime.IsAllObjectivesComplete()) Complete(runtime);
    }

    /// Đếm 1 lần cho mỗi uniqueKey (khám phá nhiều phòng: mỗi phòng 1 key).
    public bool ReportUnique(string questId, string objectiveId, string uniqueKey)
    {
        QuestRuntime runtime = GetOrCreateRuntime(questId);
        if (runtime == null)
        {
            Debug.LogError($"[Quest] Không có quest '{questId}' trong registry.", this);
            return false;
        }

        if (runtime.State != QuestState.Active)
        {
            Log($"Bỏ qua '{questId}/{objectiveId}': quest chưa active.");
            return false;
        }

        if (string.IsNullOrEmpty(uniqueKey))
        {
            ReportProgress(questId, objectiveId);
            return true;
        }

        string key = MakeKey(objectiveId, uniqueKey);
        if (runtime.HasKey(key))
        {
            Log($"'{questId}/{objectiveId}' đã tính khóa '{uniqueKey}', bỏ qua.");
            return false;
        }

        runtime.MarkKey(key);
        ReportProgress(questId, objectiveId);
        return true;
    }

    /// Chỉ mở khi quest đang chạy và mục tiêu chưa đạt. Gọi mỗi frame nên không log ở đây.
    public bool IsObjectiveOpen(string questId, string objectiveId)
    {
        if (!runtimes.TryGetValue(questId, out QuestRuntime runtime)) return false;
        if (runtime.State != QuestState.Active) return false;

        QuestObjective objective = runtime.Definition.GetObjective(objectiveId);
        if (objective == null) return false;

        return runtime.GetProgress(objectiveId) < objective.Required;
    }

    public QuestState GetState(string questId)
    {
        QuestRuntime runtime = GetOrCreateRuntime(questId);
        return runtime != null ? runtime.State : QuestState.Locked;
    }

    private void BuildRegistry()
    {
        definitions.Clear();

        for (int i = 0; i < allQuests.Count; i++)
        {
            QuestDefinition definition = allQuests[i];
            if (definition == null)
            {
                Debug.LogError($"[{nameof(QuestManager)}] All Quests có phần tử null ở index {i}.", this);
                continue;
            }

            if (string.IsNullOrEmpty(definition.QuestId))
            {
                Debug.LogError($"[{nameof(QuestManager)}] Quest '{definition.name}' chưa có Quest Id.", definition);
                continue;
            }

            if (definitions.ContainsKey(definition.QuestId))
            {
                Debug.LogError($"[{nameof(QuestManager)}] Trùng Quest Id '{definition.QuestId}', bỏ qua asset '{definition.name}'.", definition);
                continue;
            }

            if (definition.Objectives.Count == 0)
                Debug.LogWarning($"[{nameof(QuestManager)}] Quest '{definition.QuestId}' chưa có mục tiêu, sẽ xong ngay khi start.", definition);

            definitions.Add(definition.QuestId, definition);
        }

        Log($"Registry: {definitions.Count} quest.");
    }

    private QuestRuntime GetOrCreateRuntime(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return null;
        if (runtimes.TryGetValue(questId, out QuestRuntime runtime)) return runtime;
        if (!definitions.TryGetValue(questId, out QuestDefinition definition)) return null;

        runtime = new QuestRuntime(definition);
        runtimes.Add(questId, runtime);
        return runtime;
    }

    private void Complete(QuestRuntime runtime)
    {
        runtime.State = QuestState.Completed;
        Debug.Log($"[Quest] Hoàn thành '{runtime.QuestId}' — {runtime.Definition.DisplayTitle}", this);
        QuestCompleted?.Invoke(runtime);

        QuestDefinition next = runtime.Definition.NextQuest;
        if (next == null) return;

        Log($"Nối tiếp → '{next.QuestId}'");
        StartQuest(next);
    }

    private static string MakeKey(string objectiveId, string uniqueKey)
    {
        return objectiveId + "|" + uniqueKey;
    }

    private void Log(string message)
    {
        if (logVerbose) Debug.Log($"[Quest] {message}", this);
    }
}