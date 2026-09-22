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

    [Header("Crouch")]
    [Tooltip("Để trống nếu chưa có animation — khi đó trạng thái ngồi/đứng chỉ được log ra Console.")]
    [SerializeField] private Animator animator;
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
    private int crouchParameterHash;
    private bool hasCrouchParameter;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

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
    }

    private void CacheCrouchParameter()
    {
        hasCrouchParameter = false;

        if (animator == null || string.IsNullOrEmpty(crouchBoolParameter))
        {
            return;
        }

        crouchParameterHash = Animator.StringToHash(crouchBoolParameter);

        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.nameHash == crouchParameterHash)
            {
                hasCrouchParameter = true;
                return;
            }
        }

        Debug.LogWarning(
            $"[ThirdPersonController] Animator không có bool parameter tên '{crouchBoolParameter}'. " +
            "Crouch sẽ chỉ log ra Console.", this);
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

        RotateTowards(camForward);

        bool wantsSprint = sprintAction.IsPressed() && input.magnitude > 0.1f && !isCrouching;
        bool sprinting = wantsSprint && (stamina == null || stamina.CanSprint);

        if (stamina != null)
        {
            stamina.Tick(sprinting, Time.deltaTime);
        }

        float speedMultiplier = isCrouching ? crouchMultiplier : (sprinting ? sprintMultiplier : 1f);

        return moveDirection * moveSpeed * speedMultiplier;
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
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }
}