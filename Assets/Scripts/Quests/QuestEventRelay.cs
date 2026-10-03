using UnityEngine;
using UnityEngine.Events;

/// Cầu nối từ quest ra scene: ví dụ 4.2 xong thì chạy thoại của mẹ,
/// 4.1 xong thì mở điện thoại.
public class QuestEventRelay : MonoBehaviour
{
    [Tooltip("Quest cần nghe.")]
    [SerializeField] private string questId = "explore_house";

    [Header("Sự kiện")]
    [SerializeField] private UnityEvent onStarted;
    [SerializeField] private UnityEvent onCompleted;

    private void OnEnable()
    {
        if (QuestManager.Instance == null) return;
        QuestManager.Instance.QuestStarted += HandleStarted;
        QuestManager.Instance.QuestCompleted += HandleCompleted;
    }

    private void OnDisable()
    {
        if (QuestManager.Instance == null) return;
        QuestManager.Instance.QuestStarted -= HandleStarted;
        QuestManager.Instance.QuestCompleted -= HandleCompleted;
    }

    private void HandleStarted(QuestRuntime runtime)
    {
        if (runtime.QuestId != questId) return;
        onStarted?.Invoke();
    }

    private void HandleCompleted(QuestRuntime runtime)
    {
        if (runtime.QuestId != questId) return;
        onCompleted?.Invoke();
    }
}