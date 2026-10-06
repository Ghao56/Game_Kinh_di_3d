using System.Collections.Generic;

/// Trạng thái runtime của một quest trong lức chơi. QuestManager là nơi duy nhất được tạo và ghi vào.
public class QuestRuntime
{
    private readonly Dictionary<string, int> progress = new Dictionary<string, int>();
    private readonly HashSet<string> countedKeys = new HashSet<string>();

    public QuestDefinition Definition { get; }
    public string QuestId => Definition.QuestId;
    public IReadOnlyList<QuestObjective> Objectives => Definition.Objectives;
    public QuestState State { get; internal set; } = QuestState.Locked;

    public QuestRuntime(QuestDefinition definition)
    {
        Definition = definition;
    }

    public int GetProgress(string objectiveId)
    {
        return progress.TryGetValue(objectiveId, out int value) ? value : 0;
    }

    public bool IsObjectiveComplete(string objectiveId)
    {
        QuestObjective objective = Definition.GetObjective(objectiveId);
        if (objective == null) return true;

        return GetProgress(objectiveId) >= objective.Required;
    }

    public bool IsAllObjectivesComplete()
    {
        IReadOnlyList<QuestObjective> list = Definition.Objectives;
        for (int i = 0; i < list.Count; i++)
        {
            QuestObjective objective = list[i];
            if (objective == null) continue;
            if (GetProgress(objective.Id) < objective.Required) return false;
        }

        return true;
    }

    public void SetProgress(string objectiveId, int value)
    {
        progress[objectiveId] = value;
    }

    public bool HasKey(string key)
    {
        return countedKeys.Contains(key);
    }

    public void MarkKey(string key)
    {
        countedKeys.Add(key);
    }
}