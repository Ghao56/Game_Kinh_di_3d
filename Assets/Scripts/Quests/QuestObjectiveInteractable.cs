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

    [Header("Thoại")]
    [Tooltip("Đoạn thoại hiện khi tương tác. Để trống thì tương tác bình thường, không có thoại.")]
    [SerializeField] private DialogueData dialogue;

    [Tooltip("Chỉ hoàn thành mục tiêu sau khi đoạn thoại đã hiện xong. Bỏ chọn thì hoàn thành ngay lập tức.")]
    [SerializeField] private bool waitForDialogue = false;

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

        DialogueManager dialogueManager = DialogueManager.Instance;
        if (dialogue != null && dialogueManager != null && waitForDialogue)
        {
            dialogueManager.ShowDialogue(dialogue, () =>
            {
                manager.ReportProgress(questId, objectiveId);
                onInteracted?.Invoke();
                if (hideAfterUse) gameObject.SetActive(false);
            });
            return;
        }

        manager.ReportProgress(questId, objectiveId);
        onInteracted?.Invoke();

        if (dialogue != null && dialogueManager != null) dialogueManager.ShowDialogue(dialogue);

        if (hideAfterUse) gameObject.SetActive(false);
    }
}