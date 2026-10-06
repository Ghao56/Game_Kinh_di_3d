using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
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

    private InputAction lookAction;
    private float yaw;
    private float pitch;

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

        yaw = target.eulerAngles.y;
        pitch = startPitch;
    }

    private void OnEnable()
    {
        lookAction.Enable();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        lookAction.Disable();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void LateUpdate()
    {
        Vector2 look = lookAction.ReadValue<Vector2>();
        yaw += look.x * mouseSensitivity;
        pitch -= look.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + pivotOffset;
        Vector3 desiredPosition = pivot - rotation * Vector3.forward * distance;

        transform.position = desiredPosition;
        transform.LookAt(pivot);
    }
}