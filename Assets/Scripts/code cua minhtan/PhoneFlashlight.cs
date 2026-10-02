
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PhoneFlashlight : MonoBehaviour
{
    [Header("Gắn Đèn & Camera")]
    [SerializeField] private Light spotLight;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private bool isFlashlightOn = true;

    [Header("Cấu hình Xoay & Bỏ qua điểm gần")]
    [SerializeField] private float rotationSpeed = 15f;
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField] private float minHitDistance = 1.2f;
    [SerializeField] private float maxRayDistance = 100f;

    [Header("Bật / tắt bằng chuột trái")]
    [SerializeField] private bool toggleWithLeftClick = true;

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            Debug.LogWarning(
                $"[{nameof(PhoneFlashlight)}] Không tìm thấy Camera!",
                this
            );

        if (spotLight == null)
            Debug.LogWarning(
                $"[{nameof(PhoneFlashlight)}] Chưa gán 'spotLight' trong Inspector!",
                this
            );
        else
            spotLight.enabled = isFlashlightOn;
    }

    private void Update()
    {
        if (spotLight == null || mainCamera == null) return;

        HandleToggleInput();

        if (isFlashlightOn && !IsPointerOverUI())
            RotateLightToMouse();
    }

    private void HandleToggleInput()
    {
        if (!toggleWithLeftClick || Mouse.current == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (IsPointerOverUI()) return;

        ToggleFlashlight();
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject();
    }

    private void RotateLightToMouse()
    {
        if (Mouse.current == null) return;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mouseScreenPos);
        Vector3 targetPoint;

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            maxRayDistance,
            hitLayers,
            QueryTriggerInteraction.Ignore))
        {
            targetPoint =
                Vector3.Distance(hit.point, spotLight.transform.position) < minHitDistance
                ? ray.GetPoint(10f)
                : hit.point;
        }
        else
        {
            targetPoint = ray.GetPoint(20f);
        }

        Vector3 targetDirection =
            targetPoint - spotLight.transform.position;

        if (targetDirection.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation =
            Quaternion.LookRotation(targetDirection);

        spotLight.transform.rotation = Quaternion.Slerp(
            spotLight.transform.rotation,
            targetRotation,
            Time.deltaTime * rotationSpeed
        );
    }

    public void ToggleFlashlight()
    {
        isFlashlightOn = !isFlashlightOn;

        if (spotLight != null)
            spotLight.enabled = isFlashlightOn;
    }
}