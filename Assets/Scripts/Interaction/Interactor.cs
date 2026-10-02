using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(50)]
public class Interactor : MonoBehaviour
{
    [Header("Ray Detection")]
    [Tooltip("Bán kính SphereCast để dễ trúng vật mỏng. <= 0.001 thì dùng Raycast thường.")]
    [SerializeField] private float aimRadius = 0.1f;
    [Tooltip("Tầm ray (m), đo từ mặt phẳng người chơi đã dời theo hướng nhìn.")]
    [SerializeField] private float interactRange = 6f;
    [Tooltip("Khoảng cách tối đa từ InteractionOrigin tới vật tương tác (m). Muốn đúng interactRange=6 thì đặt bằng 6.")]
    [SerializeField] private float maxPlayerDistance = 4f;
    [Tooltip("Mask của TẤT CẢ vật cản (bàn/tường/giấy...). Không phải mask chỉ-đồ-tương-tác — bỏ layer Default sẽ mất khả năng chặn xuyên tường.")]
    [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;
    [Tooltip("Để trống: tự lấy Main Camera.")]
    [SerializeField] private Camera rayCamera;
    [Tooltip("Để trống: tự tìm child tên InteractionOrigin dưới Ignore Root, không có thì dùng Ignore Root.")]
    [SerializeField] private Transform interactionOrigin;

    [Header("Input")]
    [SerializeField] private InputActionAsset actions;

    [Header("UI")]
    [SerializeField] private InteractPromptUI promptUI;
    [Tooltip("Bỏ qua collider thuộc root này (mặc định root của Player) để ray không trúng nhân vật.")]
    [SerializeField] private Transform ignoreRoot;

    [Header("Debug")]
    [Tooltip("Log một lần mỗi lần target đổi: tên collider, layer, có IInteractable không.")]
    [SerializeField] private bool debugLog = false;
    [SerializeField] private bool logInteractions = false;

    private static readonly RaycastHit[] RayBuffer = new RaycastHit[32];

    private InputAction interactAction;
    private IInteractable currentTarget;
    private Component currentTargetComp;
    private Collider currentCollider;
    private Transform currentRoot;
    private InteractableGlow currentGlow;
    private float holdTimer;

    private void Awake()
    {
        if (actions == null)
        {
            Debug.LogError("[Interactor] Thiếu InputActionAsset.", this);
            enabled = false;
            return;
        }
        if (promptUI == null)
        {
            Debug.LogError("[Interactor] Thiếu InteractPromptUI.", this);
            enabled = false;
            return;
        }

        var playerMap = actions.FindActionMap("Player");
        interactAction = playerMap?.FindAction("Interact");
        if (interactAction == null)
        {
            Debug.LogError("[Interactor] Không tìm thấy action 'Interact' trong map 'Player'.", this);
            enabled = false;
            return;
        }

        if (rayCamera == null) rayCamera = Camera.main;
        if (rayCamera == null)
            Debug.LogError("[Interactor] Thiếu Camera (gán Ray Camera hoặc đánh tag MainCamera).", this);

        if (ignoreRoot == null) ignoreRoot = transform.root;
        if (interactionOrigin == null && ignoreRoot != null)
            interactionOrigin = FindDeepChild(ignoreRoot, "InteractionOrigin");
        if (interactionOrigin == null)
            interactionOrigin = ignoreRoot != null ? ignoreRoot : transform;
    }

    private void OnEnable() => interactAction?.Enable();

    private void OnDisable()
    {
        interactAction?.Disable();

        if (currentGlow != null) currentGlow.SetHighlighted(false);
        currentTarget = null;
        currentTargetComp = null;
        currentCollider = null;
        currentRoot = null;
        currentGlow = null;
        holdTimer = 0f;

        if (promptUI != null) promptUI.Hide();
    }

    private void LateUpdate()
    {
        DetectTarget();
        HandleInput();
    }

    private void DetectTarget()
    {
        if (rayCamera == null)
        {
            SetTarget(null, null, null, "thiếu rayCamera");
            return;
        }

        Vector3 planePoint = interactionOrigin != null ? interactionOrigin.position : transform.position;
        Vector3 fwd = rayCamera.transform.forward;
        Vector3 origin = rayCamera.transform.position;

        float d = Vector3.Dot(planePoint - origin, fwd);
        if (d > 0f)
            origin += fwd * d;

        int n = aimRadius > 0.001f
            ? Physics.SphereCastNonAlloc(origin, aimRadius, fwd, RayBuffer, interactRange, hitMask, QueryTriggerInteraction.Collide)
            : Physics.RaycastNonAlloc(origin, fwd, RayBuffer, interactRange, hitMask, QueryTriggerInteraction.Collide);

        Collider nearest = null;
        IInteractable nearestIt = null;
        float best = float.MaxValue;

        for (int i = 0; i < n; i++)
        {
            RaycastHit hit = RayBuffer[i];
            Collider col = hit.collider;
            if (col == null) continue;
            if (ignoreRoot != null && col.transform.IsChildOf(ignoreRoot)) continue;

            IInteractable it = col.GetComponentInParent<IInteractable>();
            if (col.isTrigger && it == null) continue;

            if (hit.distance < best)
            {
                best = hit.distance;
                nearest = col;
                nearestIt = it;
            }
        }

        if (nearest == null)
        {
            SetTarget(null, null, null, $"không trúng collider nào trong {interactRange}m (aimRadius={aimRadius})");
            return;
        }

        if (nearestIt == null)
        {
            SetTarget(null, null, null, $"chặn bởi '{nearest.name}' (layer {nearest.gameObject.layer}) — thiếu IInteractable");
            return;
        }

        Vector3 closest = nearest.bounds.ClosestPoint(planePoint);
        float dist = Vector3.Distance(planePoint, closest);
        if (dist > maxPlayerDistance)
        {
            SetTarget(null, null, null, $"'{nearest.name}' cách người chơi {dist:F1}m > maxPlayerDistance {maxPlayerDistance}m");
            return;
        }

        Component itComp = nearestIt as Component;
        if (itComp == null) itComp = nearest;
        SetTarget(nearestIt, itComp, nearest, $"dist={dist:F1}m, layer={nearest.gameObject.layer}, collider='{nearest.name}'");
    }

    private void SetTarget(IInteractable target, Component targetComp, Collider col, string debugMsg)
    {
        bool changed = currentTargetComp != targetComp;

        if (changed)
        {
            if (currentGlow != null) currentGlow.SetHighlighted(false);
            holdTimer = 0f;

            currentTarget = target;
            currentTargetComp = targetComp;
            currentCollider = col;
            currentRoot = targetComp != null ? targetComp.transform : null;
            currentGlow = null;

            if (targetComp != null)
            {
                currentGlow = targetComp.GetComponentInParent<InteractableGlow>();
                if (currentGlow == null)
                    currentGlow = targetComp.gameObject.AddComponent<InteractableGlow>();
                currentGlow.SetHighlighted(true);

                string prefix = currentTarget.HoldToInteract ? "[Giữ E] " : "[E] ";
                promptUI.Show(prefix + currentTarget.PromptText, currentRoot, currentCollider);
            }
            else
            {
                promptUI.Hide();
            }

            if (debugLog)
                Debug.Log($"[Interactor] Target đổi → {(targetComp != null ? targetComp.name : "null")}" +
                          (debugMsg != null ? $" | {debugMsg}" : string.Empty), this);
        }
        else if (currentTargetComp != null)
        {
            return;
        }
        else
        {
            if (currentGlow != null) currentGlow.SetHighlighted(false);
            currentTarget = null;
            currentGlow = null;
            currentRoot = null;
            currentCollider = null;
            holdTimer = 0f;
            promptUI.Hide();
        }
    }

    private void HandleInput()
    {
        if (interactAction == null) return;

        if (currentTargetComp == null || currentTarget == null)
        {
            holdTimer = 0f;
            return;
        }

        if (!currentTarget.HoldToInteract)
        {
            if (interactAction.WasPressedThisFrame())
            {
                if (logInteractions) Debug.Log($"[Interactor] Press → {currentTargetComp.name}");
                currentTarget.OnInteract(this);
            }
            return;
        }

        if (interactAction.IsPressed())
        {
            holdTimer += Time.deltaTime;
            if (holdTimer >= currentTarget.HoldDuration)
            {
                if (logInteractions) Debug.Log($"[Interactor] Hold hoàn tất → {currentTargetComp.name}");
                currentTarget.OnInteract(this);
                holdTimer = 0f;
            }
        }
        else
        {
            holdTimer = 0f;
        }
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName) return child;
            Transform found = FindDeepChild(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    private void OnDrawGizmosSelected()
    {
        Camera cam = rayCamera != null ? rayCamera : Camera.main;
        if (cam == null) return;

        Transform originT = interactionOrigin != null ? interactionOrigin : (ignoreRoot != null ? ignoreRoot : transform);
        if (originT == null) return;

        Vector3 fwd = cam.transform.forward;
        Vector3 origin = cam.transform.position;
        float d = Vector3.Dot(originT.position - origin, fwd);
        if (d > 0f) origin += fwd * d;

        Vector3 end = origin + fwd * interactRange;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, end);
        Gizmos.DrawWireSphere(end, Mathf.Max(aimRadius, 0.01f));
    }
}
