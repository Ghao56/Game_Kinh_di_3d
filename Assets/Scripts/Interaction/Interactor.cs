using System;
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

    [Tooltip("Bán kính quét quanh player cho các tương tác theo VÙNG (đứng trong vùng chất đồ là hiện prompt, không cần nhìn vào).")]
    [SerializeField, Range(0.4f, 3f)] private float proximityRadius = 1f;

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

    /// Tiến độ giữ E hiện tại (0..1) cho UI vòng tròn; 0 khi không giữ hoặc mục tiêu không cần giữ.
    public float HoldProgress01 =>
        currentTarget != null && currentTarget.HoldToInteract && holdTimer > 0f
            ? Mathf.Clamp01(holdTimer / currentTarget.HoldDuration)
            : 0f;

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
        // Vùng ưu tiên hơn ray khi đã sẵn sàng: cửa hậu nằm ngay trong vùng, nếu ray thắng thì
        // prompt chất đồ biến mất và E sẽ vứt đồ. Chưa sẵn sàng thì vẫn nhìn đâu tương tác đó.
        IInteractable nearest = ProximityTarget();
        if (nearest == null) nearest = RaycastTarget(GetCrosshairRay());

        if (!ReferenceEquals(nearest, currentTarget))
        {
            holdTimer = 0f;
            currentTarget = nearest;
            SetGlow(nearest);
        }

        if (currentTarget != null)
        {
            string p = currentTarget.PromptText ?? string.Empty;
            // Prompt tự nói "giữ E" (VD "GIỮ E ĐỂ THOÁT") thì không thêm tiền tố nữa
            // cho khỏi lặp "Giữ E ... [Giữ E]". Các prompt còn lại giữ nguyên tiền tố cũ.
            bool selfExplainHold = p.ToLowerInvariant().Contains("giữ e");
            string prefix = currentTarget.HoldToInteract
                ? (selfExplainHold ? string.Empty : "[Giữ E] ")
                : "[E] ";
            promptUI.gameObject.SetActive(true);
            if (promptUI.transform.Find("InteractPromptText") is Transform t) t.gameObject.SetActive(true);
            promptUI.Show(prefix + p);
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
        RaycastHit[] hits = Physics.SphereCastAll(ray, viewRadius, viewDistance, interactableMask, QueryTriggerInteraction.Collide);
        if (hits.Length == 0) return null;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            Collider col = hit.collider;

            // Bỏ qua chính người chơi
            if (col.transform.root == transform.root) continue;

            IInteractable candidate = col.GetComponentInParent<IInteractable>();

            if (candidate == null)
            {
                // Trigger vô danh -> xuyên qua, collider rắn -> chặn tầm nhìn
                if (col.isTrigger) continue;
                return null;
            }

            // Vùng proximity đã được xử lý riêng, không để ray "cướp" prompt
            if (candidate is IProximityInteractable) continue;

            if (candidate is IInteractableAvailability availability && !availability.IsAvailable)
            {
                if (col.isTrigger) continue;
                return null;
            }

            return candidate;
        }

        return null;
    }

    /// Quét các trigger đang chồng lên player: chỉ nhận IProximityInteractable còn sẵn sàng.
    private IInteractable ProximityTarget()
    {
        Vector3 center = transform.position + Vector3.up * 0.5f;
        foreach (Collider collider in Physics.OverlapSphere(center, proximityRadius, interactableMask, QueryTriggerInteraction.Collide))
        {
            IInteractable candidate = collider.GetComponentInParent<IInteractable>();
            if (candidate is IProximityInteractable zone && zone.ReadyForProximityPrompt)
                return candidate;
        }
        return null;
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