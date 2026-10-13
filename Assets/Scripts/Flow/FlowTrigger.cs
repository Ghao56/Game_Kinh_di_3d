using UnityEngine;

/// Vùng trigger phát một tín hiệu FlowBus khi người chơi bước vào.
/// Chỉ hoạt động ở đúng bước (requiredStepId) nên vào sớm không kích hoạt.
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class FlowTrigger : MonoBehaviour
{
    [Tooltip("Tín hiệu phát ra khi người chơi vào vùng.")]
    [SerializeField] private string signal = string.Empty;

    [Tooltip("Chỉ phát khi flow đang ở bước này. Để trống = luôn phát.")]
    [SerializeField] private string requiredStepId = string.Empty;

    [SerializeField] private bool fireOnce = true;

    private StoryFlowManager flow;
    private bool subscribed;
    private bool fired;
    private bool playerInside;

    private void Start()
    {
        flow = StoryFlowManager.Instance;
        if (flow == null)
        {
            Debug.LogError($"[{nameof(FlowTrigger)}] Không có StoryFlowManager — tự tắt.", this);
            enabled = false;
            return;
        }

        flow.StepEntered += HandleStepEntered;
        subscribed = true;

        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"[{nameof(FlowTrigger)}] Collider chưa bật Is Trigger.", this);
    }

    private void OnDestroy()
    {
        if (subscribed && flow != null) flow.StepEntered -= HandleStepEntered;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        playerInside = true;
        TryFire();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;
        playerInside = false;
    }

    /// Người chơi đã đứng sẵn trong vùng lúc bước bắt đầu thì vẫn phải kích hoạt.
    private void HandleStepEntered(string stepId)
    {
        if (playerInside) TryFire();
    }

    private void TryFire()
    {
        if (fired && fireOnce) return;
        if (flow == null) return;
        if (!string.IsNullOrEmpty(requiredStepId) && !flow.IsInStep(requiredStepId)) return;

        fired = true;
        FlowBus.Raise(signal);
    }

    private static bool IsPlayer(Collider other)
    {
        return other != null && other.GetComponentInParent<ThirdPersonController>() != null;
    }
}
