using UnityEngine;
using UnityEngine.Events;

/// Mở thoại khi người chơi bước vào vùng trigger.
/// Cố tình KHÔNG implement IInteractable: Interactor raycast với QueryTriggerInteraction.Collide,
/// nếu DialogueTrigger cũng là IInteractable thì nó sẽ giành mục tiêu tương tác của vật khác.
[RequireComponent(typeof(Collider))]
public class DialogueTrigger : MonoBehaviour
{
    [Header("Thoại")]
    [SerializeField] private DialogueData dialogue;

    [Tooltip("Chạy một lần rồi tắt vùng trigger. Bỏ chọn để vào ra nhiều lần.")]
    [SerializeField] private bool triggerOnce = true;

    [Header("Sau khi thoại kết thúc")]
    [SerializeField] private UnityEvent onFinished;

    private bool fired;
    private bool warnedNotTrigger;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col == null || !col.isTrigger) Debug.LogWarning($"[{nameof(DialogueTrigger)}] Collider không phải trigger.", this);
    }

    public void Trigger()
    {
        if (fired && triggerOnce) return;
        if (dialogue == null)
        {
            Debug.LogWarning($"[{nameof(DialogueTrigger)}] Chưa gán Dialogue.", this);
            return;
        }

        DialogueManager manager = DialogueManager.Instance;
        if (manager == null)
        {
            Debug.LogError($"[{nameof(DialogueTrigger)}] Không có DialogueManager trong scene.", this);
            return;
        }
        if (manager.IsActive) return;

        fired = true;
        if (triggerOnce) enabled = false;

        manager.ShowDialogue(dialogue, () =>
        {
            onFinished?.Invoke();
            if (triggerOnce) gameObject.SetActive(false);
        });
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<ThirdPersonController>() == null) return;
        Trigger();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger && !warnedNotTrigger)
        {
            warnedNotTrigger = true;
            Debug.LogWarning($"[{nameof(DialogueTrigger)}] Nhớ bật Is Trigger trên Collider.", this);
        }
    }
#endif
}