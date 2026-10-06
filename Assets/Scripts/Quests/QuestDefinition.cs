using System.Collections.Generic;
using UnityEngine;

/// Dữ liệu của một nhiệm vụ. Thêm nhiệm vụ mới = tạo asset, không phải viết code.
[CreateAssetMenu(fileName = "Quest_New", menuName = "Quests/Quest Definition", order = 0)]
public class QuestDefinition : ScriptableObject
{
    [Header("Định danh")]
    [Tooltip("Khoá duy nhất, ví dụ: explore_house. QuestStarter/QuestZone gọi StartQuest bằng chuỗi này.")]
    [SerializeField] private string questId = "quest_01";

    [SerializeField] private string displayTitle = "Nhiệm vụ mới";

    [Header("Mục tiêu")]
    [SerializeField] private List<QuestObjective> objectives = new List<QuestObjective>();

    [Header("Chuỗi nhiệm vụ")]
    [Tooltip("Tự bắt đầu quest này khi quest hiện tại hoàn thành. Để trống nếu không nối.")]
    [SerializeField] private QuestDefinition nextQuest;

    public string QuestId => questId;
    public string DisplayTitle => displayTitle;
    public IReadOnlyList<QuestObjective> Objectives => objectives;
    public QuestDefinition NextQuest => nextQuest;

    public QuestObjective GetObjective(string objectiveId)
    {
        if (string.IsNullOrEmpty(objectiveId)) return null;

        for (int i = 0; i < objectives.Count; i++)
        {
            QuestObjective objective = objectives[i];
            if (objective != null && objective.Id == objectiveId) return objective;
        }

        return null;
    }

    private void OnValidate()
    {
        questId = questId != null ? questId.Trim() : string.Empty;

        if (string.IsNullOrEmpty(questId))
            Debug.LogWarning($"[{nameof(QuestDefinition)}] Asset '{name}' chưa có Quest Id.", this);
    }
}