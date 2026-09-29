using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Camera góc nhìn thứ ba kiểu over-the-shoulder (RE2/RE4 Remake), viết tay — không dùng Cinemachine.
///
/// Kiến trúc 2 lớp:
///   • LỚP AIM  — yaw/pitch THÔ đọc thẳng từ input, xuất ra public cho Controller/Interactor.
///   • LỚP TRÌNH DIỄN — shoulder offset → collision → smoothing → sway/shake → mới ghi vào transform.
///
/// Nhờ tách vậy, các hiệu ứng "cho có" (handheld sway, shake, FOV, pullback) chỉ cộng vào VỊ TRÍ và trục Z (roll).
/// Roll không đổi transform.forward, nên CrosshairUI và ray của Interactor luôn khớp với tâm màn hình.
///
/// Thứ tự chạy: LateUpdate (DefaultExecutionOrder 0) → Interactor (50) → InteractPromptUI (100).
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    // =====================================================================================
    //  CÁC FIELD GỐC — GIỮ NGUYÊN TÊN.
    //  SampleScene đã serialize sẵn (distance = 2, pivotOffset.y = 1.6).
    //  Đổi default trong code KHÔNG đổi được giá trị đã lưu trong scene/Inspector.
    // =====================================================================================
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField] private float distance = 4f;

    [Header("Rotation")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 60f;
    [SerializeField] private float startPitch = 15f;

    [Header("Input")]
    [SerializeField] private InputActionAsset actions;

    // =====================================================================================
    //  INPUT — bổ sung
    // =====================================================================================
    [Header("Input - Thiết bị")]
    [Tooltip("Tốc độ xoay khi dùng gamepad (độ/giây). Chuột/touchscreen vẫn dùng mouseSensitivity (độ trên mỗi đơn vị delta) để giữ nguyên cảm giác cũ.")]
    [SerializeField] private float gamepadSensitivity = 180f;
    [Tooltip("Đảo chiều trục Y: kéo chuột lên thì nhìn xuống.")]
    [SerializeField] private bool invertY = false;

    // =====================================================================================
    //  SHOULDER OFFSET (over-the-shoulder)
    // =====================================================================================
    [Header("Shoulder Offset")]
    [Tooltip("Độ lệch vai theo trục ngang của camera (m). Dương = vai phải.")]
    [Range(-1.5f, 1.5f)]
    [SerializeField] private float shoulderX = 0.55f;
    [Tooltip("Nâng/hạ camera theo trục Y thế giới (m).")]
    [Range(-0.5f, 0.75f)]
    [SerializeField] private float shoulderY = 0.1f;
    [Tooltip("Tỉ lệ điểm nhìn so với offset vai. < 1 để nhân vật lệch khung hình mà camera không bị xoay lệch. Dải ổn: 0.6–0.75.")]
    [Range(0f, 1.5f)]
    [SerializeField] private float lookShoulderRatio = 0.7f;
    [Tooltip("1 = vai phải, -1 = vai trái.")]
    [Range(-1f, 1f)]
    [SerializeField] private float defaultShoulderSide = 1f;
    [Tooltip("Thời gian lerp khi đổi vai (giây).")]
    [Range(0.05f, 1f)]
    [SerializeField] private float shoulderBlendTime = 0.25f;
    [Tooltip("Tự đổi vai khi vai hiện tại bị vật cản chặn liên tục và bên kia thoáng.")]
    [SerializeField] private bool autoShoulderSwap = true;
    [Tooltip("Thời gian vai bị chặn trước khi tự đổi (giây).")]
    [Range(0.05f, 1f)]
    [SerializeField] private float autoSwapDelay = 0.2f;

    // =====================================================================================
    //  AIM MODE
    // =====================================================================================
    [Header("Aim Mode")]
    [SerializeField] private bool aimEnabled = true;
    [Tooltip("Khoảng cách camera khi ngắm (m).")]
    [Range(0.5f, 3f)]
    [SerializeField] private float aimDistance = 1.2f;
    [Tooltip("Độ lệch vai khi ngắm (m).")]
    [Range(0f, 1.5f)]
    [SerializeField] private float aimShoulderX = 0.65f;
    [Tooltip("Nâng/hạ camera khi ngắm (m).")]
    [Range(-0.5f, 0.75f)]
    [SerializeField] private float aimShoulderY = 0.05f;
    [Tooltip("FOV khi ngắm (độ). Hẹp lại => góc nhìn hẹp, tầm xa tốt hơn.")]
    [Range(20f, 90f)]
    [SerializeField] private float aimFov = 45f;
    [Tooltip("Nhân độ nhạy xoay khi ngắm. 0.6 = chậm, chính xác hơn.")]
    [Range(0.1f, 1.5f)]
    [SerializeField] private float aimSensitivityMultiplier = 0.6f;
    [Tooltip("Thời gian bám vị trí khi ngắm (giây) — nhỏ = cứng, dính tâm ngắm.")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float aimPositionSmoothTime = 0.04f;
    [Tooltip("Thời gian chuyển qua lại giữa thường và ngắm (giây).")]
    [Range(0.05f, 1f)]
    [SerializeField] private float aimBlendTime = 0.2f;

    // =====================================================================================
    //  SMOOTHING
    // =====================================================================================
    [Header("Smoothing")]
    [Tooltip("Thời gian bám vị trí camera (giây). ~0.06 rất gọn, ~0.12 mượt mà.")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float positionSmoothTime = 0.08f;

    // =====================================================================================
    //  COLLISION
    // =====================================================================================
    [Header("Collision")]
    [SerializeField] private bool collisionEnabled = true;
    [Tooltip("Bán kính quả cầu quét. Camera không lọt qua khe hẹp hơn 2 * bán kính này.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float collisionRadius = 0.25f;
    [Tooltip("Lớp nào được coi là vật cản. Player luôn bị loại trừ dù thuộc layer nào.")]
    [SerializeField] private LayerMask collisionMask = ~0;
    [Tooltip("Lùi thêm khỏi mặt tường (m) để không bị clipping vào mesh.")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float collisionSkin = 0.05f;
    [Tooltip("Khoảng cách tối thiểu camera-nhân vật (m). Camera không bao giờ lùi sát hơn mức này.")]
    [Range(0.1f, 1.5f)]
    [SerializeField] private float minCollisionDistance = 0.35f;
    [Tooltip("Thời gian bám khi bị vật cản chặn (giây) — nhỏ để camera lùi tức thì, chống xuyên tường.")]
    [Range(0.005f, 0.5f)]
    [SerializeField] private float collisionInSmoothTime = 0.02f;
    [Tooltip("Thời gian bám khi được đẩy ra (giây) — lớn hơn để không giật khi lướt qua cột.")]
    [Range(0.02f, 1f)]
    [SerializeField] private float collisionOutSmoothTime = 0.25f;

    // =====================================================================================
    //  CẢM GIÁC SỐNG
    // =====================================================================================
    [Header("Feel - FOV")]
    [Tooltip("FOV cơ bản. Để <= 0 để lấy FOV hiện tại của Camera con trong Awake.")]
    [SerializeField] private float baseFov = 60f;
    [Tooltip("FOV cộng thêm khi chạy hết tốc (độ).")]
    [Range(0f, 20f)]
    [SerializeField] private float sprintFovBoost = 4f;
    [Tooltip("Thời gian đổi FOV (giây).")]
    [Range(0.05f, 1f)]
    [SerializeField] private float fovSmoothTime = 0.3f;

    [Header("Feel - Khoảng cách")]
    [Tooltip("Camera rút ra thêm khi chạy nhanh (m).")]
    [Range(0f, 1f)]
    [SerializeField] private float sprintDistanceBoost = 0.25f;
    [Tooltip("Nhân khoảng cách theo góc nhìn dọc. Trục X chuẩn hoá 0 (nhìn lên hết cỡ) → 1 (nhìn xuống hết cỡ). Nhìn xuống thì camera rút ra để không dập vào đầu.")]
    [SerializeField] private AnimationCurve distanceByPitch = new AnimationCurve(
        new Keyframe(0f, 0.92f),
        new Keyframe(0.5f, 1f),
        new Keyframe(1f, 1.15f));

    [Header("Feel - Handheld Sway")]
    [Tooltip("Dao động tay cầm rất nhẹ. CHỈ cộng vào vị trí + trục Z (roll) nên không bao giờ lệch tâm ngắm.")]
    [SerializeField] private bool enableHandheld = true;
    [Range(0f, 1f)]
    [SerializeField] private float handheldAmount = 0.35f;
    [Tooltip("Tần số dao động (Hz). 0.4 = chậm, thư giãn.")]
    [Range(0.05f, 2f)]
    [SerializeField] private float handheldFrequency = 0.4f;
    [Tooltip("Biên độ lắc ngang/dọc (m) ở handheldAmount = 1.")]
    [Range(0f, 0.05f)]
    [SerializeField] private float handheldPositionAmplitude = 0.012f;
    [Tooltip("Biên độ lắc roll (độ) ở handheldAmount = 1. Roll KHÔNG đổi transform.forward nên an toàn tuyệt đối cho ray bắn.")]
    [Range(0f, 5f)]
    [SerializeField] private float handheldRollDegrees = 0.15f;

    [Header("Feel - Shake")]
    [Tooltip("Xoá shake tích luỹ khi bật lại camera (mở/đóng NoteReader).")]
    [SerializeField] private bool clearShakeOnEnable = true;
    [Tooltip("Thời gian phai mặc định của shake (giây) — dùng khi AddShake không truyền duration.")]
    [Range(0.05f, 3f)]
    [SerializeField] private float shakeDuration = 0.35f;
    [Tooltip("Tần số rung (Hz).")]
    [Range(5f, 60f)]
    [SerializeField] private float shakeFrequency = 22f;
    [Tooltip("Biên độ rung theo vị trí (m) khi cường độ = 1.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float shakePositionAmplitude = 0.08f;
    [Tooltip("Biên độ rung theo roll (độ) khi cường độ = 1.")]
    [Range(0f, 20f)]
    [SerializeField] private float shakeRollDegrees = 3f;

    [Header("Camera Con")]
    [Tooltip("Để trống: tự lấy Camera đầu tiên là con trong Awake. Cần cho mọi hiệu ứng FOV.")]
    [SerializeField] private Camera viewCamera;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    // =====================================================================================
    //  TRẠNG THÁI NỘI BỘ
    // =====================================================================================
    private static readonly RaycastHit[] CollisionHits = new RaycastHit[16];
    private static readonly RaycastHit[] ShoulderHits = new RaycastHit[8];

    private InputAction lookAction;
    private InputAction aimAction;
    private InputAction swapShoulderAction;

    // Lớp AIM — thô, không bao giờ smoothing.
    private float yaw;
    private float pitch;

    // Lớp trình diễn — đã mượt.
    private float shoulderSide = 1f;
    private float currentShoulderX;
    private float shoulderXVelocity;
    private float currentShoulderY;
    private float shoulderYVelocity;
    private float currentDistance;
    private float currentDistanceVelocity;
    private float collisionDistance;
    private float collisionDistanceVelocity;
    private float aimBlend;
    private float aimBlendVelocity;
    private float speed01Smooth;
    private float speed01Velocity;
    private float pitchDistanceScale = 1f;
    private float currentFov = 60f;
    private float fovVelocity;

    private Vector3 positionVelocity;
    private Quaternion shakeReferenceRotation = Quaternion.identity;
    private float handheldTimer;
    private float shoulderBlockedTimer;

    private float shakeTimer;
    private float shakeTotalDuration = 0.35f;
    private float shakeIntensity;

    private ThirdPersonController controller;
    private Transform playerRoot;
    private bool needsSnap = true;

    // =====================================================================================
    //  PUBLIC API
    // =====================================================================================

    /// <summary>Góc yaw THÔ (độ): chưa smoothing, chưa shake, chưa cộng shoulder. Dùng để tính hướng di chuyển.</summary>
    public float Yaw => yaw;

    /// <summary>Góc pitch THÔ (độ).</summary>
    public float Pitch => pitch;

    /// <summary>Đang ngắm hay không. Cho animation, UI, controller.</summary>
    public bool IsAiming { get; private set; }

    /// <summary>0 = đứng yên, 1 = chạy hết tốc. Lấy từ ThirdPersonController.</summary>
    public float Speed01 => controller != null ? controller.SpeedNormalized : 0f;

    /// <summary>Khoảng cách camera→nhân vật hiện tại sau collision (m). Cho Depth of Field.</summary>
    public float CurrentDistance => collisionDistance;

    /// <summary>Camera con (dùng cho FOV / post-processing). Có thể null.</summary>
    public Camera ViewCamera => viewCamera;

    /// <summary>
    /// Rung camera (impulse). Mặc định chỉ rung vị trí + roll nên KHÔNG lệch tâm ngắm.
    /// Đặt affectAim = true nếu muốn rung cả tâm ngắm (đòn đánh gây choáng).
    /// </summary>
    /// <param name="amplitude">Cường độ 0–1 nhân với biên độ đã cấu hình.</param>
    /// <param name="duration">Thời gian phai (giây). Âm = dùng <see cref="shakeDuration"/>.</param>
    public void AddShake(float amplitude, float duration = -1f, bool affectAim = false)
    {
        if (amplitude <= 0f) return;

        shakeTotalDuration = duration > 0f ? duration : shakeDuration;
        shakeIntensity = Mathf.Max(shakeIntensity, Mathf.Clamp01(amplitude));
        shakeTimer = Mathf.Max(shakeTimer, shakeTotalDuration);
        shakeAffectsAim |= affectAim;
    }

    private bool shakeAffectsAim;

    // =====================================================================================
    //  LIFECYCLE
    // =====================================================================================
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

        // Action mới — thiếu thì tự tắt tính năng, KHÔNG làm hỏng camera.
        aimAction = playerMap.FindAction("Aim");
        if (aimAction == null)
        {
            Debug.LogWarning(
                "[ThirdPersonCamera] Không tìm thấy action 'Aim' trong map 'Player' — đã tắt chế độ ngắm. " +
                "Thêm action Aim (Button) vào Input Actions rồi gán lại asset.", this);
        }

        swapShoulderAction = playerMap.FindAction("SwapShoulder");
        if (swapShoulderAction == null)
        {
            Debug.LogWarning(
                "[ThirdPersonCamera] Không tìm thấy action 'SwapShoulder' trong map 'Player' — đã tắt đổi vai bằng phím.", this);
        }

        if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        if (viewCamera == null)
        {
            Debug.LogWarning("[ThirdPersonCamera] Không tìm thấy Camera con — đã tắt các hiệu ứng FOV.", this);
        }
        else if (baseFov <= 0f)
        {
            baseFov = viewCamera.fieldOfView;
        }

        controller = target.GetComponent<ThirdPersonController>();
        if (controller == null) controller = target.GetComponentInParent<ThirdPersonController>();

        playerRoot = target.root;

        currentFov = baseFov;
        yaw = target.eulerAngles.y;
        pitch = Mathf.Clamp(startPitch, minPitch, maxPitch);
        shoulderSide = defaultShoulderSide >= 0f ? 1f : -1f;
        currentShoulderX = shoulderX * shoulderSide;
        currentShoulderY = shoulderY;
        currentDistance = Mathf.Max(distance, 0.01f);
        collisionDistance = currentDistance;
        needsSnap = true;
    }

    private void OnEnable()
    {
        lookAction?.Enable();
        if (aimEnabled) aimAction?.Enable();
        swapShoulderAction?.Enable();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Ràng buộc 4: mở/đóng NoteReader không được làm camera nhảy vị trí.
        positionVelocity = Vector3.zero;
        currentDistanceVelocity = 0f;
        collisionDistanceVelocity = 0f;
        shoulderXVelocity = 0f;
        shoulderYVelocity = 0f;
        aimBlendVelocity = 0f;
        fovVelocity = 0f;
        needsSnap = true;

        if (clearShakeOnEnable) ClearShake();
    }

    private void OnDisable()
    {
        lookAction?.Disable();
        aimAction?.Disable();
        swapShoulderAction?.Disable();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // =====================================================================================
    //  VÒNG LẶP CHÍNH
    // =====================================================================================
    private void LateUpdate()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        // Input look dùng unscaled để camera vẫn phản hồi đúng khi slow-mo.
        UpdateLook(Mathf.Max(Time.unscaledDeltaTime, 0.0001f));
        UpdateFeelState(dt);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + pivotOffset;
        Vector3 right = rotation * Vector3.right;

        UpdateShoulder(dt, pivot, right);
        UpdateDistance(dt);
        UpdateCollision(dt, pivot, right, rotation);

        Vector3 castOrigin = pivot + right * currentShoulderX + Vector3.up * currentShoulderY;
        Vector3 desiredPosition = castOrigin - rotation * Vector3.forward * collisionDistance;
        Vector3 lookPoint = pivot + right * (currentShoulderX * lookShoulderRatio);

        ApplyPose(dt, desiredPosition, lookPoint);
    }

    // -------------------------------------------------------------------------------------
    //  LỚP AIM — logic thô
    // -------------------------------------------------------------------------------------
    private void UpdateLook(float inputDt)
    {
        Vector2 look = lookAction.ReadValue<Vector2>();

        float yawDelta;
        float pitchDelta;

        // Chuột: Pointer delta tính theo frame nên giữ nguyên cảm giác cũ (mouseSensitivity = độ / đơn vị delta).
        // Gamepad: analog là TỐC ĐỘ, phải nhân deltaTime — nếu không, tốc độ xoay sẽ phụ thuộc FPS.
        if (IsAnalogDevice())
        {
            yawDelta = look.x * gamepadSensitivity * inputDt;
            pitchDelta = look.y * gamepadSensitivity * inputDt;
        }
        else
        {
            yawDelta = look.x * mouseSensitivity;
            pitchDelta = look.y * mouseSensitivity;
        }

        if (IsAiming) yawDelta *= aimSensitivityMultiplier;

        if (invertY) pitchDelta = -pitchDelta;

        yaw += yawDelta;
        pitch = Mathf.Clamp(pitch - pitchDelta, minPitch, maxPitch);
    }

    private bool IsAnalogDevice()
    {
        var control = lookAction?.activeControl;
        if (control == null) return false;
        return control.device is Gamepad || control.device is Joystick;
    }

    private void UpdateFeelState(float dt)
    {
        IsAiming = aimEnabled && aimAction != null && aimAction.IsPressed();

        if (swapShoulderAction != null && swapShoulderAction.WasPressedThisFrame())
        {
            shoulderSide = -shoulderSide;
            shoulderBlockedTimer = 0f;
        }

        aimBlend = Smooth(IsAiming ? 1f : 0f, aimBlend, ref aimBlendVelocity, aimBlendTime, dt);
        speed01Smooth = Smooth(Speed01, speed01Smooth, ref speed01Velocity, fovSmoothTime, dt);
        pitchDistanceScale = distanceByPitch.Evaluate(
            Mathf.InverseLerp(minPitch, maxPitch, pitch));
    }

    // -------------------------------------------------------------------------------------
    //  LỚP TRÌNH DIỄN
    // -------------------------------------------------------------------------------------
    private void UpdateShoulder(float dt, Vector3 pivot, Vector3 right)
    {
        float targetShoulderX = Mathf.Lerp(shoulderX, aimShoulderX, aimBlend) * shoulderSide;
        float targetShoulderY = Mathf.Lerp(shoulderY, aimShoulderY, aimBlend);

        currentShoulderX = Smooth(targetShoulderX, currentShoulderX, ref shoulderXVelocity, shoulderBlendTime, dt);
        currentShoulderY = Smooth(targetShoulderY, currentShoulderY, ref shoulderYVelocity, shoulderBlendTime, dt);

        if (!autoShoulderSwap || !collisionEnabled) return;

        if (IsShoulderBlocked(pivot, pivot + right * currentShoulderX))
        {
            shoulderBlockedTimer += dt;
            if (shoulderBlockedTimer >= autoSwapDelay && !IsShoulderBlocked(pivot, pivot - right * currentShoulderX))
            {
                shoulderSide = -shoulderSide;
                shoulderBlockedTimer = 0f;
            }
        }
        else
        {
            shoulderBlockedTimer = 0f;
        }
    }

    private void UpdateDistance(float dt)
    {
        float baseDist = Mathf.Lerp(distance, aimDistance, aimBlend) * pitchDistanceScale;
        float target = baseDist + sprintDistanceBoost * speed01Smooth * (1f - aimBlend);
        target = Mathf.Max(target, 0.01f);

        currentDistance = Smooth(target, currentDistance, ref currentDistanceVelocity, positionSmoothTime, dt);
    }

    private void UpdateCollision(float dt, Vector3 pivot, Vector3 right, Quaternion rotation)
    {
        if (!collisionEnabled)
        {
            collisionDistance = currentDistance;
            collisionDistanceVelocity = 0f;
            return;
        }

        Vector3 castOrigin = pivot + right * currentShoulderX + Vector3.up * currentShoulderY;
        Vector3 castDir = -(rotation * Vector3.forward);

        float allowed = currentDistance;
        int n = Physics.SphereCastNonAlloc(
            castOrigin, collisionRadius, castDir, CollisionHits, currentDistance,
            collisionMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < n; i++)
        {
            Collider col = CollisionHits[i].collider;
            if (col == null || IsPlayerCollider(col)) continue;
            allowed = Mathf.Min(allowed, CollisionHits[i].distance);
        }

        allowed = Mathf.Clamp(
            allowed - collisionSkin,
            Mathf.Min(minCollisionDistance, currentDistance),
            currentDistance);

        // Bất đối xứng: lùi vào cực nhanh (chống xuyên tường), đẩy ra chậm (chống giật qua cột/khe hẹp).
        float smoothTime = allowed < collisionDistance ? collisionInSmoothTime : collisionOutSmoothTime;
        collisionDistance = Smooth(allowed, collisionDistance, ref collisionDistanceVelocity, smoothTime, dt);
        collisionDistance = Mathf.Clamp(collisionDistance, 0.01f, currentDistance);
    }

    private void UpdateFov(float dt)
    {
        if (viewCamera == null) return;

        float target = Mathf.Lerp(baseFov, aimFov, aimBlend) + sprintFovBoost * speed01Smooth * (1f - aimBlend);
        currentFov = Smooth(Mathf.Clamp(target, 10f, 120f), currentFov, ref fovVelocity, fovSmoothTime, dt);

        if (!Mathf.Approximately(viewCamera.fieldOfView, currentFov))
            viewCamera.fieldOfView = currentFov;
    }

    private void ApplyPose(float dt, Vector3 desiredPosition, Vector3 lookPoint)
    {
        if (needsSnap)
        {
            transform.position = desiredPosition;
            transform.rotation = Quaternion.LookRotation(lookPoint - desiredPosition, Vector3.up);
            positionVelocity = Vector3.zero;
            collisionDistance = currentDistance;
            needsSnap = false;
        }
        else
        {
            float smoothTime = Mathf.Lerp(positionSmoothTime, aimPositionSmoothTime, aimBlend);
            transform.position = Vector3.SmoothDamp(
                transform.position, desiredPosition, ref positionVelocity, smoothTime, Mathf.Infinity, dt);
            transform.LookAt(lookPoint);
        }

        shakeReferenceRotation = transform.rotation;
        UpdateFov(dt);
        ApplyHandheldAndShake(dt);
    }

    private void ApplyHandheldAndShake(float dt)
    {
        Vector3 posOffset = Vector3.zero;
        float roll = 0f;
        float yawOffset = 0f;
        float pitchOffset = 0f;

        // --- Handheld sway. Tắt khi ngắm. Chỉ động vị trí + roll ⇒ không lệch tâm ngắm.
        if (enableHandheld && !IsAiming)
        {
            handheldTimer += dt;
            float t = handheldTimer * handheldFrequency;
            float amp = handheldAmount;

            float nx = Mathf.PerlinNoise(t, 0.13f) * 2f - 1f;
            float ny = Mathf.PerlinNoise(0.37f, t) * 2f - 1f;
            float nz = Mathf.PerlinNoise(t * 0.7f, 0.71f) * 2f - 1f;

            posOffset += shakeReferenceRotation * new Vector3(nx, ny, 0f) * (handheldPositionAmplitude * amp);
            roll += nz * handheldRollDegrees * amp;
        }

        // --- Shake: impulse phai bậc hai theo thời gian.
        if (shakeTimer > 0f)
        {
            shakeTimer -= dt;

            float t01 = Mathf.Clamp01(shakeTimer / Mathf.Max(shakeTotalDuration, 0.0001f));
            float decay = t01 * t01 * shakeIntensity;
            float phase = Time.time * shakeFrequency;

            float sx = Mathf.PerlinNoise(phase, 0f) * 2f - 1f;
            float sy = Mathf.PerlinNoise(0f, phase) * 2f - 1f;
            float sz = Mathf.PerlinNoise(0.5f, phase * 1.3f) * 2f - 1f;

            posOffset += shakeReferenceRotation * new Vector3(sx, sy, 0f) * (shakePositionAmplitude * decay);
            roll += sz * shakeRollDegrees * decay;

            if (shakeAffectsAim)
            {
                yawOffset = sx * shakeRollDegrees * 0.35f * decay;
                pitchOffset = sy * shakeRollDegrees * 0.35f * decay;
            }

            if (shakeTimer <= 0f) ClearShake();
        }

        transform.position += posOffset;
        transform.rotation = shakeReferenceRotation * Quaternion.Euler(pitchOffset, yawOffset, roll);
    }

    private void ClearShake()
    {
        shakeTimer = 0f;
        shakeIntensity = 0f;
        shakeAffectsAim = false;
    }

    // -------------------------------------------------------------------------------------
    //  TIỆN ÍCH
    // -------------------------------------------------------------------------------------
    private bool IsPlayerCollider(Collider col)
    {
        return playerRoot != null && col.transform.IsChildOf(playerRoot);
    }

    private bool IsShoulderBlocked(Vector3 pivot, Vector3 shoulderPoint)
    {
        Vector3 delta = shoulderPoint - pivot;
        float len = delta.magnitude;
        if (len < 0.001f) return false;

        int n = Physics.RaycastNonAlloc(
            pivot, delta / len, ShoulderHits, len * 0.9f,
            collisionMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < n; i++)
        {
            Collider col = ShoulderHits[i].collider;
            if (col == null || IsPlayerCollider(col)) continue;
            return true;
        }

        return false;
    }

    /// <summary>
    /// SmoothDamp cho scalar, tự nhân deltaTime (frame-rate independent).
    /// Lưu ý: đừng dùng Mathf.Lerp(a, b, 0.1f) — tốc độ hội tụ sẽ phụ thuộc FPS.
    /// </summary>
    private static float Smooth(float target, float current, ref float velocity, float smoothTime, float dt)
    {
        return Mathf.SmoothDamp(current, target, ref velocity, Mathf.Max(smoothTime, 0.0001f), Mathf.Infinity, dt);
    }

    // =====================================================================================
    //  GIZMOS / VALIDATION
    // =====================================================================================
    private void OnValidate()
    {
        if (minPitch > maxPitch) minPitch = maxPitch;
        if (collisionRadius <= minCollisionDistance)
            collisionRadius = minCollisionDistance + 0.01f;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || target == null) return;

        bool playing = Application.isPlaying;
        Quaternion rot = playing
            ? transform.rotation
            : Quaternion.Euler(Mathf.Clamp(startPitch, minPitch, maxPitch), target.eulerAngles.y, 0f);

        Vector3 pivot = target.position + pivotOffset;
        Vector3 right = rot * Vector3.right;
        float shX = playing ? currentShoulderX : shoulderX * (defaultShoulderSide >= 0f ? 1f : -1f);
        float dist = playing ? collisionDistance : distance;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(pivot, 0.06f);
        Gizmos.DrawLine(pivot, pivot + right * shoulderX);
        Gizmos.DrawWireSphere(pivot + right * shoulderX, 0.05f);

        Vector3 end = pivot + right * shX - rot * Vector3.forward * dist;
        Gizmos.color = collisionEnabled ? Color.yellow : Color.green;
        Gizmos.DrawLine(pivot + right * shX, end);

        if (collisionEnabled)
        {
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.4f);
            Gizmos.DrawWireSphere(end, collisionRadius);
        }

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(pivot + right * (shX * lookShoulderRatio), 0.04f);
    }
}
