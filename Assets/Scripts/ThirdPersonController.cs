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

    [Header("Jump & Gravity")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedGravity = -2f;

    [Header("Crouch")]
    [SerializeField] private float standingHeight = 2f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float standingCenterY = 1f;
    [SerializeField] private float crouchCenterY = 0.5f;

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

        Vector2 input = moveAction.ReadValue<Vector2>();

        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        bool sprinting = sprintAction.IsPressed() && input.magnitude > 0.1f && !isCrouching;
        float speedMultiplier = isCrouching ? crouchMultiplier : (sprinting ? sprintMultiplier : 1f);

        Vector3 moveDirection = camForward * input.y + camRight * input.x;
        controller.Move(moveDirection * moveSpeed * speedMultiplier * Time.deltaTime);

        if (moveDirection.magnitude > 0.1f)
        {
            RotateTowards(moveDirection);
        }

        ApplyGravity();
    }

    private void HandleCrouch()
    {
        bool wantCrouch = crouchAction.IsPressed();
        if (wantCrouch == isCrouching) return;

        isCrouching = wantCrouch;

        float targetHeight = wantCrouch ? crouchHeight : standingHeight;
        controller.height = targetHeight;

        Vector3 center = controller.center;
        center.y = wantCrouch ? crouchCenterY : standingCenterY;
        controller.center = center;

        if (!wantCrouch && controller.isGrounded)
        {
            verticalVelocity = groundedGravity;
        }
    }

    private void RotateTowards(Vector3 direction)
    {
        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void ApplyGravity()
    {
        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedGravity;

            if (jumpAction.WasPressedThisFrame())
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        controller.Move(new Vector3(0f, verticalVelocity, 0f) * Time.deltaTime);
    }
}