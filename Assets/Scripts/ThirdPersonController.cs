using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 1.8f;
    [SerializeField] private float crouchMultiplier = 0.5f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Stamina")]
    [Tooltip("Để trống nếu không muốn giới hạn chạy nhanh bằng stamina.")]
    [SerializeField] private StaminaSystem stamina;

    [Header("Jump & Gravity")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedGravity = -2f;
    [Tooltip("Bật để log isGrounded mỗi lần ấn Jump — dùng để kiểm tra lỗi nhảy, xong thì tắt.")]
    [SerializeField] private bool logJumpDebug = true;

    [Header("Animation")]
    [Tooltip("Để trống nếu chưa có animation — script sẽ tự tìm Animator trong con. Trạng thái Idle/Walk/Run/Jump được điều khiển tự động.")]
    [SerializeField] private Animator animator;
    [Tooltip("Tên parameter bool để chuyển sang state Walking.")]
    [SerializeField] private string walkingBoolParameter = "isWalking";
    [Tooltip("Tên parameter int để sang state Fast Run (1) / trở lại Walking (0).")]
    [SerializeField] private string runIntParameter = "Run";
    [Tooltip("Tên parameter trigger gọi state Jump.")]
    [SerializeField] private string jumpTriggerParameter = "Jump";

    [Header("Crouch")]
    [SerializeField] private string crouchBoolParameter = "IsCrouching";
    [SerializeField] private bool logCrouchState = true;
    [Tooltip("Nếu bật, không cho nhảy khi đang ngồi.")]
    [SerializeField] private bool blockJumpWhileCrouching = true;

    [Header("Input")]
    [SerializeField] private InputActionAsset actions;

    [Header("Camera Reference")]
    [SerializeField] private Transform cameraTransform;

    private CharacterController controller;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction crouchAction;
    private float verticalVelocity;
    private bool isCrouching;

    // Crouching chỉ được dùng nếu Animator controller thực sự có parameter này.
    private int crouchParameterHash;
    private bool hasCrouchParameter;

    // các parameter điều khiển Idle/Walk/Run/Jump trong Animator Controller.
    private int walkingParameterHash;
    private bool hasWalkingParameter;
    private int runParameterHash;
    private bool hasRunParameter;
    private AnimatorControllerParameterType runParameterType;
    private int jumpParameterHash;
    private bool hasJumpParameter;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (animator == null)
        {
            // Field Animator trong Inspector đang trống → tự tìm trong con của object này.
            animator = GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            Debug.LogWarning("[ThirdPersonController] Không tìm thấy Animator nào trên PlayerCharacter hay trong con — " +
                             "nhân vật sẽ di chuyển nhưng không chạy animation đi/chạy.", this);
        }

        if (actions == null)
        {
            Debug.LogError("[ThirdPersonController] Chưa gán Input Actions asset trong Inspector.", this);
            enabled = false;
            return;
        }

        if (cameraTransform == null)
        {
            Debug.LogError("[ThirdPersonController] Chưa gán Camera Transform trong Inspector.", this);
            enabled = false;
            return;
        }

        var playerMap = actions.FindActionMap("Player", throwIfNotFound: true);
        moveAction = playerMap.FindAction("Move", throwIfNotFound: true);
        jumpAction = playerMap.FindAction("Jump", throwIfNotFound: true);
        sprintAction = playerMap.FindAction("Sprint", throwIfNotFound: true);
        crouchAction = playerMap.FindAction("Crouch", throwIfNotFound: true);

        CacheCrouchParameter();
        CacheLocomotionParameters();
    }

    private void CacheCrouchParameter()
    {
        hasCrouchParameter = TryFindAnimatorParameter(crouchBoolParameter, out var crouchParameter) &&
                             crouchParameter.type == AnimatorControllerParameterType.Bool;
        crouchParameterHash = Animator.StringToHash(crouchBoolParameter);

        if (!hasCrouchParameter && animator != null && !string.IsNullOrEmpty(crouchBoolParameter))
        {
            Debug.LogWarning(
                $"[ThirdPersonController] Animator không có bool parameter tên '{crouchBoolParameter}'. " +
                "Crouch sẽ chỉ log ra Console.", this);
        }
    }

    private void CacheLocomotionParameters()
    {
        hasWalkingParameter = TryFindAnimatorParameter(walkingBoolParameter, out var walkingParameter) &&
                              walkingParameter.type == AnimatorControllerParameterType.Bool;
        walkingParameterHash = Animator.StringToHash(walkingBoolParameter);

        hasRunParameter = TryFindAnimatorParameter(runIntParameter, out var runParameter) &&
                          (runParameter.type == AnimatorControllerParameterType.Int ||
                           runParameter.type == AnimatorControllerParameterType.Bool);
        runParameterType = hasRunParameter ? runParameter.type : AnimatorControllerParameterType.Int;
        runParameterHash = Animator.StringToHash(runIntParameter);

        hasJumpParameter = TryFindAnimatorParameter(jumpTriggerParameter, out var jumpParameter) &&
                           jumpParameter.type == AnimatorControllerParameterType.Trigger;
        jumpParameterHash = Animator.StringToHash(jumpTriggerParameter);

        if (animator == null)
        {
            return;
        }

        if (!hasWalkingParameter)
        {
            Debug.LogWarning(
                $"[ThirdPersonController] Animator không có bool parameter tên '{walkingBoolParameter}'. State Walking sẽ không được bật.", this);
        }

        if (!hasRunParameter)
        {
            Debug.LogWarning(
                $"[ThirdPersonController] Animator không có parameter '{runIntParameter}' (int/bool). State Fast Run sẽ không được bật.", this);
        }

        if (!hasJumpParameter)
        {
            Debug.LogWarning(
                $"[ThirdPersonController] Animator không có trigger parameter tên '{jumpTriggerParameter}'. State Jump sẽ không được gọi.", this);
        }
    }

    private bool TryFindAnimatorParameter(string name, out AnimatorControllerParameter parameter)
    {
        parameter = null;

        if (animator == null || string.IsNullOrEmpty(name))
        {
            return false;
        }

        int hash = Animator.StringToHash(name);
        foreach (var candidate in animator.parameters)
        {
            if (candidate.nameHash == hash)
            {
                parameter = candidate;
                return true;
            }
        }

        return false;
    }

    private void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
        sprintAction.Enable();
        crouchAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        jumpAction.Disable();
        sprintAction.Disable();
        crouchAction.Disable();
    }

    private void Update()
    {
        HandleCrouch();

        // isGrounded của CharacterController chỉ phản ánh LẦN Move GẦN NHẤT.
        // Vì vậy phải tính hết vận tốc ngang + dọc trước, rồi gọi Move ĐÚNG MỘT LẦN.
        Vector3 horizontalVelocity = HandleMovement();
        UpdateVerticalVelocity();

        Vector3 velocity = horizontalVelocity;
        velocity.y = verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    private void HandleCrouch()
    {
        bool wantCrouch = crouchAction.ReadValue<float>() > 0.5f;

        if (wantCrouch == isCrouching)
        {
            return;
        }

        isCrouching = wantCrouch;

        if (hasCrouchParameter)
        {
            animator.SetBool(crouchParameterHash, isCrouching);
        }

        if (logCrouchState)
        {
            Debug.Log(isCrouching
                ? "[Crouch] NGỒI XUỐNG — tốc độ giảm còn 50% (placeholder: chưa có animation)"
                : "[Crouch] ĐỨNG DẬY — tốc độ trở lại bình thường");
        }
    }

    // Trả về vận tốc theo mặt phẳng ngang (m/s). Không gọi controller.Move ở đây.
    private Vector3 HandleMovement()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDirection = camForward * input.y + camRight * input.x;

        bool wantsSprint = sprintAction.IsPressed() && input.magnitude > 0.1f && !isCrouching;
        bool sprinting = wantsSprint && (stamina == null || stamina.CanSprint);

        if (stamina != null)
        {
            stamina.Tick(sprinting, Time.deltaTime);
        }

        float speedMultiplier = isCrouching ? crouchMultiplier : (sprinting ? sprintMultiplier : 1f);

        UpdateLocomotionAnimation(input, sprinting);

        if (camForward.sqrMagnitude > 0.001f)
        {
            RotateTowards(camForward);
        }

        return moveDirection * moveSpeed * speedMultiplier;
    }

    // Set parameter trong Animator để chuyển state Idle / Walking / Fast Run.
    private void UpdateLocomotionAnimation(Vector2 input, bool sprinting)
    {
        if (animator == null)
        {
            return;
        }

        bool moving = input.magnitude > 0.1f;

        if (hasWalkingParameter)
        {
            animator.SetBool(walkingParameterHash, moving);
        }

        if (hasRunParameter)
        {
            if (runParameterType == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(runParameterHash, sprinting);
            }
            else
            {
                animator.SetInteger(runParameterHash, sprinting ? 1 : 0);
            }
        }
    }

    private void RotateTowards(Vector3 direction)
    {
        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    // Chỉ cập nhật verticalVelocity, việc di chuyển thật sự nằm ở Update().
    private void UpdateVerticalVelocity()
    {
        bool jumpPressed = jumpAction.WasPressedThisFrame();

        if (logJumpDebug && jumpPressed)
        {
            Debug.Log($"[Jump] pressed | isGrounded={controller.isGrounded} | " +
                      $"verticalVelocity={verticalVelocity:F2} | crouching={isCrouching}");
        }

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedGravity;

            bool canJump = !(blockJumpWhileCrouching && isCrouching);
            if (canJump && jumpPressed)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

                if (hasJumpParameter)
                {
                    animator.SetTrigger(jumpParameterHash);
                }
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }
}