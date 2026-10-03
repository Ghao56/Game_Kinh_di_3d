using UnityEngine;
using UnityEngine.Events;

/// Vật tương tác gắn với một mục tiêu quest: cốc nước, hộp thuốc, cửa sổ...
/// Trước khi quest mở thì không có prompt, không có viền highlight.
[RequireComponent(typeof(Collider))]
public class QuestObjectiveInteractable : MonoBehaviour, IInteractable, IInteractableAvailability
{
    [Header("Giao diện")]
    [SerializeField] private string promptText = "Tương tác";
    [SerializeField] private bool holdToInteract = false;
    [SerializeField] private float holdDuration = 0.5f;

    [Header("Nhiệm vụ")]
    [Tooltip("Quest chứa mục tiêu này.")]
    [SerializeField] private string questId = "mother_care";

    [Tooltip("Id mục tiêu, ví dụ: get_water.")]
    [SerializeField] private string objectiveId = string.Empty;

    [Header("Sau khi tương tác")]
    [SerializeField] private UnityEvent onInteracted;

    [Tooltip("Ẩn vật sau khi dùng, ví dụ cốc nước được mang đi.")]
    [SerializeField] private bool hideAfterUse = false;

    private bool used;

    public string PromptText => promptText;
    public bool HoldToInteract => holdToInteract;
    public float HoldDuration => holdDuration;

    public bool IsAvailable
    {
        get
        {
            if (used) return false;

            QuestManager manager = QuestManager.Instance;
            return manager != null && manager.IsObjectiveOpen(questId, objectiveId);
        }
    }

    public void OnInteract(Interactor interactor)
    {
        if (used) return;

        QuestManager manager = QuestManager.Instance;
        if (manager == null)
        {
            Debug.LogError($"[{nameof(QuestObjectiveInteractable)}] Không có QuestManager trong scene.", this);
            return;
        }

        if (!manager.IsObjectiveOpen(questId, objectiveId))
        {
            Debug.LogWarning($"[{nameof(QuestObjectiveInteractable)}] Mục tiêu '{questId}/{objectiveId}' chưa mở.", this);
            return;
        }

        used = true;
        Debug.Log($"[Quest] Tương tác '{gameObject.name}' → {questId}/{objectiveId}", this);
        manager.ReportProgress(questId, objectiveId);

        onInteracted?.Invoke();

        if (hideAfterUse) gameObject.SetActive(false);
    }
}