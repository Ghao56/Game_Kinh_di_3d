using UnityEngine;

/// Bật/tắt một vật theo bước hiện tại của flow.
/// Pad_Phong1-3 chỉ hoạt động trong bước S1_Explore chẳng hạn.
[DisallowMultipleComponent]
public class FlowGate : MonoBehaviour
{
    [Tooltip("Bước bắt đầu được hoạt động. Để trống = từ đầu.")]
    [SerializeField] private string activeFromStep = string.Empty;

    [Tooltip("Bước kết thúc (không bao gồm). Để trống = đến hết flow.")]
    [SerializeField] private string activeUntilStep = string.Empty;

    [Tooltip("Tắt object khi ngoài khoảng bước. Bỏ chọn thì object luôn bật.")]
    [SerializeField] private bool hideWhenInactive = true;

    [Tooltip("Object bị bật/tắt. Để trống = chính object này.")]
    [SerializeField] private GameObject target;

    private StoryFlowManager flow;
    private bool subscribed;

    private void Start()
    {
        flow = StoryFlowManager.Instance;
        if (flow == null)
        {
            Debug.LogError($"[{nameof(FlowGate)}] Không có StoryFlowManager — tự tắt.", this);
            enabled = false;
            return;
        }

        // Không huỷ ở OnDisable: gate có thể tự SetActive(false) chính mình,
        // vẫn cần nhận StepEntered để bật lại. Chỉ huỷ khi object bị huỷ.
        flow.StepEntered += HandleStepEntered;
        subscribed = true;

        ApplyState();
    }

    private void OnDestroy()
    {
        if (subscribed && flow != null) flow.StepEntered -= HandleStepEntered;
    }

    private void HandleStepEntered(string stepId)
    {
        ApplyState();
    }

    private void ApplyState()
    {
        if (flow == null) return;

        int now = flow.IndexOf(flow.CurrentStepId);

        int from = string.IsNullOrEmpty(activeFromStep) ? int.MinValue : flow.IndexOf(activeFromStep);
        if (!string.IsNullOrEmpty(activeFromStep) && from < 0)
        {
            Debug.LogError($"[{nameof(FlowGate)}] Bước '{activeFromStep}' không có trong flow.", this);
            from = int.MaxValue;
        }

        int until = string.IsNullOrEmpty(activeUntilStep) ? int.MaxValue : flow.IndexOf(activeUntilStep);
        if (!string.IsNullOrEmpty(activeUntilStep) && until < 0)
        {
            Debug.LogError($"[{nameof(FlowGate)}] Bước '{activeUntilStep}' không có trong flow.", this);
            until = int.MinValue;
        }

        bool active = now >= from && now < until;

        if (!hideWhenInactive) return;

        GameObject go = target != null ? target : gameObject;
        if (go.activeSelf != active) go.SetActive(active);
    }
}
