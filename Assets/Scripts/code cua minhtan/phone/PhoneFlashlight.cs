
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PhoneFlashlight : MonoBehaviour
{
    [Header("Gắn Đèn & Camera")]
    [SerializeField] private Light spotLight;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private bool isFlashlightOn = true;

    [Header("Xoay đèn theo hướng nhìn")]
    [SerializeField] private float rotationSpeed = 15f;

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
            spotLight = FindSpotLight();

        if (spotLight == null)
            Debug.LogWarning(
                $"[{nameof(PhoneFlashlight)}] Chưa gán 'spotLight' trong Inspector!",
                this
            );
        else
        {
            spotLight.enabled = isFlashlightOn;
            spotLight.shadows = LightShadows.Soft;
        }
    }

    private Light FindSpotLight()
    {
        Light child = GetComponentInChildren<Light>();
        if (child != null) return child;

        foreach (Light candidate in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (candidate.type != LightType.Spot) continue;
            if (candidate == RenderSettings.sun) continue;
            return candidate;
        }

        return null;
    }

    private void Update()
    {
        if (spotLight == null || mainCamera == null) return;

        HandleToggleInput();

        if (isFlashlightOn)
            AlignLightToView();
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
    private void AlignLightToView()
    {
        Quaternion targetRotation = Quaternion.LookRotation(mainCamera.transform.forward);

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