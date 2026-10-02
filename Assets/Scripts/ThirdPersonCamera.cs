using UnityEngine;
using UnityEngine.InputSystem;

// Chạy trước InteractionOriginFollower (0) và Interactor (50) trong LateUpdate
// để ray của Interactor đọc đúng pose camera của frame này, không phải frame trước.
[DefaultExecutionOrder(-10)]
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [Tooltip("Offset pivot so với transform target. LƯU Ý: target đứng ở GIỮA capsule, không phải ở chân. " +
             "Nhân vật cao 2m: chân ở -0.48, đỉnh đầu ở 1.52. Nên Y = 0.5 là ngang ngực — đừng tăng lại 1.5.")]
    [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private float distance = 2.5f;

    [Header("Over The Shoulder")]
    [Tooltip("Lệch camera sang vai (đơn vị mét, cộng vào local X của rig). Dấu quyết định bên, runtime đổi bằng phím Q.")]
    [SerializeField] private float shoulderOffset = 0.55f;
    [Tooltip("Thời gian SmoothDamp khi đổi vai. 0.1 ≈ 0.3s thực tế.")]
    [SerializeField] private float shoulderSwapSmoothTime = 0.1f;

    [Header("Rotation")]
    [Tooltip("Độ / pixel chuột.")]
    [SerializeField] private float mouseSensitivity = 2f;
    [Tooltip("Độ / giây tay cầm (đã nhân deltaTime).")]
    [SerializeField] private float stickSensitivity = 120f;
    [Tooltip("Vùng chết của stick, tránh camera trôi khi không cầm.")]
    [SerializeField] private float stickDeadzone = 0.15f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 60f;
    [SerializeField] private float startPitch = 15f;
    [Tooltip("SmoothDamp góc nhìn (giây). Mượt góc chứ không mượt delta chuột.")]
    [SerializeField] private float lookSmoothTime = 0.04f;

    [Header("Follow")]
    [Tooltip("SmoothDamp pivot theo X/Z (giây).")]
    [SerializeField] private float followSmoothTime = 0.08f;
    [Tooltip("SmoothDamp pivot theo Y (giây) — chậm hơn để không giật khi lên bậc thang.")]
    [SerializeField] private float verticalSmoothTime = 0.15f;

    [Header("Collision")]
    [SerializeField] private float collisionRadius = 0.25f;
    [Tooltip("Để trống = mặc định trừ layer IgnoreRaycast. Phải loại layer của Player.")]
    [SerializeField] private LayerMask collisionMask = Physics.DefaultRaycastLayers;
    [Tooltip("Khoảng cách tối thiểu khi bị ép sát tường (m).")]
    [SerializeField] private float minDistance = 0.35f;
    [Tooltip("Tốc độ nới camera ra lại khi rời vật cản (m/s).")]
    [SerializeField] private float recoverSpeed = 6f;
    [Tooltip("Giảm distance tối đa khi ngước lên để camera không chui xuống đất.")]
    [SerializeField] private float pitchUpDistanceLoss = 0.6f;

    [Header("Lens")]
    [Tooltip("Để trống = tự lấy Camera trong children.")]
    [SerializeField] private Camera outputCamera;
    [SerializeField] private float fieldOfView = 55f;
    [SerializeField] private float nearClipPlane = 0.1f;

    [Header("Input")]
    [SerializeField] private InputActionAsset actions;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

    private InputAction lookAction;
    private InputAction swapAction;
    private Camera cachedCamera;

    private float targetYaw;
    private float targetPitch;
    private float yaw;
    private float pitch;
    private float yawVelocity;
    private float pitchVelocity;

    private float shoulderSide = 1f;
    private float shoulderNow;
    private float shoulderVel;

    private Vector3 smoothPivot;
    private Vector3 pivotVelXZ;
    private float pivotVelY;
    private float currentDistance;
    private bool initialized;

    private void Awake()
    {
        if (actions == null)
        {
            Debug.LogError("[ThirdPersonCamera] Chưa gán Input Actions asset trong Inspector.", this);
            enabled = false;
            return;
        }

        if (target == null)
        {
            Debug.LogError("[ThirdPersonCamera] Chưa gán Target trong Inspector.", this);
            enabled = false;
            return;
        }

        var playerMap = actions.FindActionMap("Player", throwIfNotFound: true);
        lookAction = playerMap.FindAction("Look", throwIfNotFound: true);
        // Null-safe: asset chưa có action thì camera vẫn chạy, chỉ mất phím đổi vai.
        swapAction = playerMap.FindAction("CameraSwap", throwIfNotFound: false);

        ResolveCamera();
        ApplyLensSettings();
    }

    private void OnEnable()
    {
        if (lookAction != null) lookAction.Enable();
        swapAction?.Enable();

        // NoteReaderUI tắt/bật rig khi đọc note → snap về góc nhìn hiện tại thay vì trôi từ góc cũ.
        if (target != null)
        {
            targetYaw = target.eulerAngles.y;
            targetPitch = Mathf.Clamp(startPitch, minPitch, maxPitch);
            yaw = targetYaw;
            pitch = targetPitch;
            yawVelocity = 0f;
            pitchVelocity = 0f;
            pivotVelXZ = Vector3.zero;
            pivotVelY = 0f;
            smoothPivot = target.position + pivotOffset;
            currentDistance = Mathf.Max(minDistance, distance);
            initialized = true;
        }

        shoulderNow = shoulderOffset * shoulderSide;
        shoulderVel = 0f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        if (lookAction != null) lookAction.Disable();
        swapAction?.Disable();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnValidate()
    {
        distance = Mathf.Max(0.05f, distance);
        minDistance = Mathf.Clamp(minDistance, 0.05f, distance);
        collisionRadius = Mathf.Max(0.01f, collisionRadius);
        followSmoothTime = Mathf.Max(0.0001f, followSmoothTime);
        verticalSmoothTime = Mathf.Max(0.0001f, verticalSmoothTime);
        lookSmoothTime = Mathf.Max(0.0001f, lookSmoothTime);
        shoulderSwapSmoothTime = Mathf.Max(0.01f, shoulderSwapSmoothTime);
        fieldOfView = Mathf.Clamp(fieldOfView, 10f, 120f);
        nearClipPlane = Mathf.Max(0.001f, nearClipPlane);

        if (isActiveAndEnabled)
        {
            ResolveCamera();
            ApplyLensSettings();
        }
    }

    private void ResolveCamera()
    {
        if (outputCamera == null) outputCamera = GetComponentInChildren<Camera>();
        cachedCamera = outputCamera;
    }

    private void ApplyLensSettings()
    {
        if (cachedCamera == null) return;
        cachedCamera.fieldOfView = fieldOfView;
        cachedCamera.nearClipPlane = nearClipPlane;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        float dt = Time.deltaTime;
        HandleLook(dt);
        HandleFollow();

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        if (swapAction != null && swapAction.WasPressedThisFrame())
        {
            shoulderSide = -shoulderSide;
        }

        shoulderNow = Mathf.SmoothDamp(
            shoulderNow, shoulderOffset * shoulderSide,
            ref shoulderVel, shoulderSwapSmoothTime);

        // Camera đặt theo offset trong không gian xoay của rig, KHÔNG LookAt.
        // Nhờ vậy tâm màn hình là hướng nhìn thật và nhân vật tự lệch sang trái khung hình.
        // Dùng shoulderNow (đang chuyển động) để khi đổi vai, bước tránh va chạm vẫn bám theo.
        Vector3 shoulderOrigin = smoothPivot + rotation * new Vector3(shoulderNow, 0f, 0f);
        Vector3 back = rotation * Vector3.back;

        currentDistance = ResolveDistance(shoulderOrigin, back, dt);

        transform.SetPositionAndRotation(shoulderOrigin + back * currentDistance, rotation);
    }

    private void HandleLook(float dt)
    {
        Vector2 look = ReadLookScaled(dt);
        if (look.sqrMagnitude > 0f)
        {
            targetYaw += look.x;
            targetPitch = Mathf.Clamp(targetPitch - look.y, minPitch, maxPitch);
        }

        yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, lookSmoothTime);
        pitch = Mathf.SmoothDampAngle(pitch, targetPitch, ref pitchVelocity, lookSmoothTime);

        if (!initialized)
        {
            yaw = targetYaw;
            pitch = targetPitch;
            Vector3 rawPivot = target.position + pivotOffset;
            smoothPivot = rawPivot;
            currentDistance = Mathf.Max(minDistance, distance);
            initialized = true;
        }
    }

    // <Pointer>/delta là pixel mỗi frame → KHÔNG nhân deltaTime.
    // <Gamepad>/rightStick là giá trị liên tục → PHẢI nhân deltaTime rồi đổi sang độ/giây.
    private Vector2 ReadLookScaled(float dt)
    {
        if (lookAction == null) return Vector2.zero;

        Vector2 raw = lookAction.ReadValue<Vector2>();
        if (raw.sqrMagnitude <= 1e-8f) return Vector2.zero;

        InputControl control = lookAction.activeControl;
        bool isAnalog = control != null && control.device is Gamepad;

        if (!isAnalog) return raw * mouseSensitivity;

        float magnitude = raw.magnitude;
        if (magnitude < stickDeadzone) return Vector2.zero;

        float scaled = Mathf.InverseLerp(stickDeadzone, 1f, magnitude);
        return raw.normalized * scaled * stickSensitivity * dt;
    }

    private void HandleFollow()
    {
        Vector3 desired = target.position + pivotOffset;

        Vector3 xz = Vector3.SmoothDamp(
            new Vector3(smoothPivot.x, 0f, smoothPivot.z),
            new Vector3(desired.x, 0f, desired.z),
            ref pivotVelXZ, followSmoothTime);

        float y = Mathf.SmoothDamp(smoothPivot.y, desired.y, ref pivotVelY, verticalSmoothTime);

        smoothPivot = new Vector3(xz.x, y, xz.z);
    }

    private float ResolveDistance(Vector3 origin, Vector3 back, float dt)
    {
        float desired = Mathf.Max(minDistance, distance);

        // Ngước lên (pitch âm) → camera lùi về sẽ chui xuống đất, rút ngắn lại.
        float lookUp = Mathf.Clamp01(-pitch / 45f);
        desired = Mathf.Max(minDistance, desired - lookUp * pitchUpDistanceLoss);

        float allowed = desired;

        int count = Physics.SphereCastNonAlloc(
            origin, collisionRadius, back, HitBuffer, desired,
            collisionMask, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = HitBuffer[i];
            if (hit.collider == null) continue;
            if (hit.collider.transform.IsChildOf(target.root)) continue;
            if (hit.distance < nearest) nearest = hit.distance;
        }

        if (nearest < float.MaxValue)
            allowed = Mathf.Max(minDistance, nearest);

        // Bị chặn: kéo vào ngay. Hết bị chặn: nới ra từ từ để không giật.
        if (allowed < currentDistance) return allowed;
        return Mathf.MoveTowards(currentDistance, allowed, recoverSpeed * dt);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || target == null) return;

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 origin = smoothPivot + rotation * new Vector3(shoulderNow, 0f, 0f);
        Vector3 back = rotation * Vector3.back;

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(origin, 0.05f);
        Gizmos.DrawLine(origin, origin + back * currentDistance);
        Gizmos.DrawWireSphere(origin + back * currentDistance, collisionRadius);

        Gizmos.color = Color.yellow;
        Vector3 pivot = target.position + pivotOffset;
        Gizmos.DrawLine(smoothPivot, pivot);
        Gizmos.DrawWireSphere(pivot, 0.05f);
    }
}
