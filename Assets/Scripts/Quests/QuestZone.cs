using UnityEngine;

/// Vùng kích hoạt theo vị trí người chơi: báo mục tiêu khám phá (mỗi vùng 1 lần)
/// hoặc bắt đầu quest khi bước vào.
[RequireComponent(typeof(Collider))]
[DefaultExecutionOrder(100)]
public class QuestZone : MonoBehaviour
{
    public enum ZoneMode
    {
        ReportObjective,
        StartQuest
    }

    [Header("Chế độ")]
    [SerializeField] private ZoneMode mode = ZoneMode.ReportObjective;

    [Header("Nhiệm vụ")]
    [Tooltip("Quest mà zone này phục vụ.")]
    [SerializeField] private string questId = "explore_house";

    [Tooltip("Mode StartQuest: ưu tiên asset này. Để trống thì dùng Quest Id ở trên.")]
    [SerializeField] private QuestDefinition questToStart;

    [Tooltip("Mode StartQuest: quest này phải hoàn thành trước mới cho chạy. Để trống = không yêu cầu.")]
    [SerializeField] private string requiresCompletedQuestId = string.Empty;

    [Header("Mode Report Objective")]
    [Tooltip("Mục tiêu được báo khi người chơi bước vào zone.")]
    [SerializeField] private string objectiveId = "explore";

    [Tooltip("Khoá đếm một lần. Để trống thì lấy tên GameObject.")]
    [SerializeField] private string uniqueKey = string.Empty;

    [Header("Hành vi")]
    [Tooltip("Mode StartQuest: sau khi bắt đầu quest thì báo luôn 1 điểm cho Mục tiêu ở trên.")]
    [SerializeField] private bool reportObjectiveOnStart;

    [Tooltip("Chạy một lần rồi khoá zone.")]
    [SerializeField] private bool fireOnce = true;

    private bool playerInside;
    private bool fired;

    private void Awake()
    {
        Collider zoneCollider = GetComponent<Collider>();
        if (zoneCollider != null && !zoneCollider.isTrigger)
            Debug.LogWarning($"[{nameof(QuestZone)}] Collider chưa bật Is Trigger, zone sẽ không nhận OnTriggerEnter.", this);
    }

    private void OnEnable()
    {
        if (QuestManager.Instance == null) return;
        QuestManager.Instance.QuestStarted += HandleQuestStarted;
        QuestManager.Instance.QuestCompleted += HandleQuestCompleted;
    }

    private void OnDisable()
    {
        if (QuestManager.Instance == null) return;
        QuestManager.Instance.QuestStarted -= HandleQuestStarted;
        QuestManager.Instance.QuestCompleted -= HandleQuestCompleted;
    }

    private void Reset()
    {
        Collider zoneCollider = GetComponent<Collider>();
        if (zoneCollider != null) zoneCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;

        playerInside = true;
        Debug.Log($"[{nameof(QuestZone)}] '{name}' OnTriggerEnter (collider: {other.name}).", this);
        RunZone();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;

        playerInside = false;
    }

    private void HandleQuestStarted(QuestRuntime runtime)
    {
        if (!playerInside) return;
        if (mode == ZoneMode.ReportObjective && runtime.QuestId != questId) return;
        if (!RequirementMet()) return;

        RunZone();
    }

    private void HandleQuestCompleted(QuestRuntime runtime)
    {
        if (mode != ZoneMode.StartQuest) return;
        if (!playerInside) return;
        if (string.IsNullOrEmpty(requiresCompletedQuestId) || runtime.QuestId != requiresCompletedQuestId) return;

        RunZone();
    }

    private void RunZone()
    {
        if (fireOnce && fired)
        {
            Debug.Log($"[{nameof(QuestZone)}] '{name}' đã chạy xong (Fire Once), bỏ qua.", this);
            return;
        }

        QuestManager manager = QuestManager.Instance;
        if (manager == null)
        {
            Debug.LogError($"[{nameof(QuestZone)}] Không có QuestManager trong scene.", this);
            return;
        }

        if (mode == ZoneMode.StartQuest)
        {
            if (!RequirementMet())
            {
                Debug.Log($"[{nameof(QuestZone)}] '{name}' bị khoá: cần hoàn thành '{requiresCompletedQuestId}' " +
                          $"trước (trạng thái hiện tại: {manager.GetState(requiresCompletedQuestId)}).", this);
                return;
            }

            if (questToStart != null) manager.StartQuest(questToStart);
            else manager.StartQuest(questId);

            if (reportObjectiveOnStart && !string.IsNullOrEmpty(objectiveId))
            {
                string startKey = string.IsNullOrEmpty(uniqueKey) ? gameObject.name : uniqueKey;
                manager.ReportUnique(questId, objectiveId, startKey);
            }

            if (fireOnce) fired = true;
            return;
        }

        string key = string.IsNullOrEmpty(uniqueKey) ? gameObject.name : uniqueKey;
        bool counted = manager.ReportUnique(questId, objectiveId, key);
        if (counted && fireOnce) fired = true;
    }

    private bool RequirementMet()
    {
        if (mode != ZoneMode.StartQuest) return true;
        if (string.IsNullOrEmpty(requiresCompletedQuestId)) return true;

        QuestManager manager = QuestManager.Instance;
        return manager != null && manager.GetState(requiresCompletedQuestId) == QuestState.Completed;
    }

    private static bool IsPlayer(Collider other)
    {
        return other != null && other.GetComponentInParent<ThirdPersonController>() != null;
    }
}