
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class TestPlayerMove : MonoBehaviour
{
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float rotationSpeed = 10f;

    [Header("Jump")]
    public float jumpForce = 6f;
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundMask;

    [Header("References")]
    public Transform cameraTransform;
    public Animator animator;

    private Rigidbody rb;
    private Vector2 moveInput;
    private bool isRunning;
    private bool isGrounded;
    private bool jumpRequested;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        rb.useGravity = true;
        rb.isKinematic = false;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Update()
    {
        ReadInput();
        CheckGround();
        HandleJump();
        UpdateAnimation();
    }

    void ReadInput()
    {
        moveInput = Vector2.zero;
        isRunning = false;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.wKey.isPressed)
            moveInput.y += 1;

        if (Keyboard.current.sKey.isPressed)
            moveInput.y -= 1;

        if (Keyboard.current.aKey.isPressed)
            moveInput.x -= 1;

        if (Keyboard.current.dKey.isPressed)
            moveInput.x += 1;

        isRunning = Keyboard.current.leftShiftKey.isPressed
                 || Keyboard.current.rightShiftKey.isPressed;

        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
    }

    void CheckGround()
    {
        isGrounded = false;

        if (groundCheck == null)
            return;

        Collider[] hits = Physics.OverlapSphere(
            groundCheck.position,
            groundCheckRadius,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in hits)
        {
            if (hit.transform != transform &&
                !hit.transform.IsChildOf(transform))
            {
                isGrounded = true;
                break;
            }
        }
    }

    void HandleJump()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
        {
            jumpRequested = true;

            if (animator != null)
                animator.SetTrigger("Jump");
        }
    }

    void FixedUpdate()
    {
        MovePlayer();

        if (jumpRequested)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            jumpRequested = false;
            isGrounded = false;
        }
    }

    void MovePlayer()
    {
        Vector3 forward;
        Vector3 right;

        if (cameraTransform != null)
        {
            forward = cameraTransform.forward;
            right = cameraTransform.right;

            forward.y = 0f;
            right.y = 0f;

            forward.Normalize();
            right.Normalize();
        }
        else
        {
            forward = Vector3.forward;
            right = Vector3.right;
        }

        Vector3 moveDirection =
            forward * moveInput.y + right * moveInput.x;

        float speed = isRunning ? runSpeed : walkSpeed;

        Vector3 velocity = rb.linearVelocity;
        velocity.x = moveDirection.x * speed;
        velocity.z = moveDirection.z * speed;
        rb.linearVelocity = velocity;

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(moveDirection);

            rb.MoveRotation(Quaternion.Slerp(
                rb.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime
            ));
        }
    }

    void UpdateAnimation()
    {
        if (animator == null)
            return;

        bool isWalking = moveInput.sqrMagnitude > 0.01f;

        animator.SetBool("isWalking", isWalking);
        animator.SetInteger("Run",
            isWalking && isRunning ? 1 : 0);
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            groundCheck.position,
            groundCheckRadius
        );
    }
}
