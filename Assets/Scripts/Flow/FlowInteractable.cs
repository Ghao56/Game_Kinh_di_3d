using System.Collections.Generic;
using UnityEngine;

/// Vật tương tác chỉ dùng được ở một bước của flow.
/// Cube "Mở" chẳng hạn: chỉ hiện prompt/viền ở bước cuối, dùng một lần rồi thôi.
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class FlowInteractable : MonoBehaviour, IInteractable, IInteractableAvailability
{
    [Header("Giao diện")]
    [SerializeField] private string promptText = "Tương tác";
    [SerializeField] private bool holdToInteract = false;
    [SerializeField] private float holdDuration = 0.5f;

    [Header("Flow")]
    [Tooltip("Chỉ cho tương tác khi flow đang ở bước này. Để trống = luôn cho.")]
    [SerializeField] private string requiredStepId = string.Empty;

    [Tooltip("Tín hiệu phát ra sau khi tương tác.")]
    [SerializeField] private string signalOnInteract = string.Empty;

    [Tooltip("Hành động chạy khi tương tác.")]
    [SerializeField] private List<FlowAction> actions = new List<FlowAction>();

    [Tooltip("Chỉ dùng được một lần.")]
    [SerializeField] private bool oneShot = true;

    private bool used;

    public string PromptText => promptText;
    public bool HoldToInteract => holdToInteract;
    public float HoldDuration => holdDuration;

    public bool IsAvailable
    {
        get
        {
            if (used) return false;

            StoryFlowManager flow = StoryFlowManager.Instance;
            if (flow == null) return false;
            if (!string.IsNullOrEmpty(requiredStepId) && !flow.IsInStep(requiredStepId)) return false;

            return true;
        }
    }

    public void OnInteract(Interactor interactor)
    {
        if (!IsAvailable) return;

        used = true;

        StoryFlowManager flow = StoryFlowManager.Instance;
        if (flow != null) flow.RunActions(actions);

        FlowBus.Raise(signalOnInteract);
    }
}
