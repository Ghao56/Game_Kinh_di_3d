using UnityEngine;
using UnityEngine.InputSystem;

public class Interactor : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float interactionRadius = 2.5f;
    [SerializeField] private Vector3 detectionOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private LayerMask interactableMask = ~0; // mặc định: tất cả layer
    [SerializeField] private int maxColliders = 8;

    [Header("Input")]
    [SerializeField] private InputActionAsset actions;

    [Header("UI")]
    [SerializeField] private InteractPromptUI promptUI;

    [Header("Debug")]
    [SerializeField] private bool logInteractions = false;

    private InputAction interactAction;
    private Collider[] hits;
    private IInteractable currentTarget;
    private float holdTimer;

    private void Awake()
    {
        if (actions == null)
        {
            Debug.LogError("[Interactor] Thiếu InputActionAsset.");
            enabled = false;
            return;
        }
        if (promptUI == null)
        {
            Debug.LogError("[Interactor] Thiếu InteractPromptUI.");
            enabled = false;
            return;
        }

        var playerMap = actions.FindActionMap("Player");
        interactAction = playerMap?.FindAction("Interact");
        if (interactAction == null)
        {
            Debug.LogError("[Interactor] Không tìm thấy action 'Interact' trong map 'Player'.");
            enabled = false;
            return;
        }

        hits = new Collider[maxColliders];
    }

    private void OnEnable() => interactAction?.Enable();
    private void OnDisable() => interactAction?.Disable();

    private void Update()
    {
        DetectTarget();
        HandleInput();
    }

    private void DetectTarget()
    {
        Vector3 center = transform.position + detectionOffset;
        int count = Physics.OverlapSphereNonAlloc(
            center, interactionRadius, hits, interactableMask, QueryTriggerInteraction.Collide);

        IInteractable nearest = null;
        float nearestSqrDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            var candidate = hits[i].GetComponentInParent<IInteractable>();
            if (candidate == null) continue;

            float sqrDist = (hits[i].transform.position - transform.position).sqrMagnitude;
            if (sqrDist < nearestSqrDist)
            {
                nearestSqrDist = sqrDist;
                nearest = candidate;
            }
        }

        if (!ReferenceEquals(nearest, currentTarget))
        {
            holdTimer = 0f;
            currentTarget = nearest;
        }

        if (currentTarget != null)
        {
            string prefix = currentTarget.HoldToInteract ? "[Giữ E] " : "[E] ";
            promptUI.Show(prefix + currentTarget.PromptText);
        }
        else
        {
            promptUI.Hide();
        }
    }

    private void HandleInput()
    {
        if (currentTarget == null || interactAction == null) return;

        if (!currentTarget.HoldToInteract)
        {
            if (interactAction.WasPressedThisFrame())
            {
                if (logInteractions) Debug.Log($"[Interactor] Press → {((Component)currentTarget).name}");
                currentTarget.OnInteract(this);
            }
            return;
        }

        if (interactAction.IsPressed())
        {
            holdTimer += Time.deltaTime;
            if (holdTimer >= currentTarget.HoldDuration)
            {
                if (logInteractions) Debug.Log($"[Interactor] Hold hoàn tất → {((Component)currentTarget).name}");
                currentTarget.OnInteract(this);
                holdTimer = 0f;
            }
        }
        else if (interactAction.WasReleasedThisFrame())
        {
            holdTimer = 0f;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + detectionOffset, interactionRadius);
    }
}