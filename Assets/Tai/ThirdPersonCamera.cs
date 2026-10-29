
using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Camera Settings")]
    public float distance = 4f;
    public float height = 1.5f;
    public float mouseSensitivity = 0.15f;
    public float minPitch = -30f;
    public float maxPitch = 70f;
    public float followSpeed = 10f;

    private float yaw;
    private float pitch = 15f;

    void Start()
    {
        if (target != null)
            yaw = target.eulerAngles.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Xoay camera bang chuot
        if (Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            yaw += mouseDelta.x * mouseSensitivity;
            pitch -= mouseDelta.y * mouseSensitivity;
        }

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // Tinh vi tri camera quanh nhan vat
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 focusPosition = target.position
                              + Vector3.up * height;

        Vector3 desiredPosition = focusPosition
            - rotation * Vector3.forward * distance;

        // Camera di theo nhan vat muot hon
        float smoothTime = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            smoothTime
        );

        transform.rotation = rotation;
    }

    void OnApplicationFocus(bool hasFocus)
    {
        Cursor.lockState = hasFocus
            ? CursorLockMode.Locked
            : CursorLockMode.None;

        Cursor.visible = !hasFocus;
    }
}
