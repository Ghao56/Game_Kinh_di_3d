using UnityEngine;
using UnityEngine.InputSystem;

public class Interactor : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private LayerMask interactableMask = ~0; // mặc định: tất cả layer
    [Tooltip("Ray xuyên qua tâm màn hình (crosshair): camera bắn SphereCast mảnh theo hướng nhìn; trúng vật nào thì chỉ vật đó được chọn.")]
    [SerializeField, Min(0.5f)] private float viewDistance = 4f;
    [Tooltip("Bán kính spherecast để ray không quá mảnh, dễ trượt vào cạnh cửa.")]
    [SerializeField, Range(0.01f, 0.3f)] private float viewRadius = 0.1f;

    [Header("Input")]
    [SerializeField] private InputActionAsset actions;

    [Header("UI")]
    [SerializeField] private InteractPromptUI promptUI;

    [Header("Debug")]
    [SerializeField] private bool logInteractions = false;

    private InputAction interactAction;
    private Camera cachedCam;
    private IInteractable currentTarget;
    private InteractableGlow currentGlow;
    private float holdTimer;

    /// Mục tiêu mà ray hướng nhìn đang trúng (null nếu không có). PlayerGrab đọc để chặn hành vi trùng phím E.
    public IInteractable CurrentTarget => currentTarget;

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
    }

    private void OnEnable() => interactAction?.Enable();

    private void OnDisable()
    {
        interactAction?.Disable();
        currentTarget = null;
        SetGlow(null);
    }

    private void Update()
    {
        DetectTarget();
        HandleInput();
    }

    private void DetectTarget()
    {
        // Chỉ nhìn vào đâu tương tác đó: spherecast mảnh dọc theo crosshair (tâm camera).
        IInteractable nearest = RaycastTarget(GetCrosshairRay());

        if (!ReferenceEquals(nearest, currentTarget))
        {
            holdTimer = 0f;
            currentTarget = nearest;
            SetGlow(nearest);
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

    /// Ray xuyên qua tâm màn hình (điểm crosshair), giống cách PlayerGrab bắn ray.
    private Ray GetCrosshairRay()
    {
        if (cachedCam == null) cachedCam = Camera.main; // camera runtime có thể được tạo/thay (cutscene)
        if (cachedCam != null)
            return cachedCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        // Dự phòng khi không có camera nào tag MainCamera.
        return new Ray(transform.position, transform.forward);
    }

    /// nhìn vào đâu thì nhắm vật đó: spherecast mảnh theo crosshair.
    private IInteractable RaycastTarget(Ray ray)
    {
        if (!Physics.SphereCast(ray, viewRadius, out RaycastHit hit, viewDistance, interactableMask, QueryTriggerInteraction.Collide))
            return null;

        IInteractable candidate = hit.collider.GetComponentInParent<IInteractable>();
        if (candidate == null) return null;

        if (candidate is IInteractableAvailability availability && !availability.IsAvailable) return null;

        return candidate;
    }

    private void SetGlow(IInteractable target)
    {
        Component component = target as Component;
        InteractableGlow glow = component != null ? component.GetComponentInParent<InteractableGlow>() : null;

        if (currentGlow == glow) return;

        if (currentGlow != null) currentGlow.SetHighlighted(false);
        currentGlow = glow;
        if (currentGlow != null) currentGlow.SetHighlighted(true);
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
        Gizmos.color = Color.cyan;
        if (Camera.main != null)
        {
            Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            Gizmos.DrawRay(ray.origin, ray.direction * viewDistance);
        }
        else
        {
            Gizmos.DrawRay(transform.position, transform.forward * viewDistance);
        }
    }
}