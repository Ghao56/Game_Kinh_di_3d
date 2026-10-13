using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlayerGrab : MonoBehaviour
{
    [Header("Vị trí")]
    [SerializeField] private Transform holdPoint;    // Vị trí cầm trên tay bình thường
    [SerializeField] private Transform inspectPoint; // Vị trí đưa lên trước mắt để xem

    [Header("Cài đặt")]
    [SerializeField] private float pickupRange = 3f;
    [SerializeField] private float rotateSpeed = 0.5f;

    [Header("Khóa Camera & Di Chuyển Khi Đang Xem Đồ (bấm R)")]
    [Tooltip("Kéo script xoay camera + script di chuyển nhân vật vào đây")]
    [SerializeField] private List<Behaviour> scriptsToDisableWhileInspecting;

    private GameObject heldItem;
    private bool isInspecting = false;
    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;

        if (holdPoint == null)
            Debug.LogWarning($"{nameof(PlayerGrab)}: chưa gán holdPoint!", this);

        if (inspectPoint == null)
            Debug.LogWarning($"{nameof(PlayerGrab)}: chưa gán inspectPoint!", this);

        if (mainCamera == null)
            Debug.LogWarning($"{nameof(PlayerGrab)}: không tìm thấy Main Camera.", this);
    }

    private void OnDisable()
    {
        // Đảm bảo không bao giờ bỏ quên camera/di chuyển ở trạng thái bị khóa
        if (isInspecting)
            SetInspectingScriptsEnabled(true);
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        // 1. Nhặt hoặc Vứt đồ (Phím E)
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (heldItem == null)
                TryPickUp();
            else if (!isInspecting)
                DropItem();
        }

        // 2. Bật / Tắt chế độ Xem đồ (Phím R)
        if (Keyboard.current.rKey.wasPressedThisFrame && heldItem != null)
        {
            ToggleInspect();
        }

        // 3. Xoay đồ vật bằng chuột khi đang xem
        if (isInspecting && heldItem != null && Mouse.current != null)
        {
            RotateItem();
        }
    }

    private void TryPickUp()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, pickupRange);
        foreach (Collider col in colliders)
        {
            if (!col.CompareTag("Item")) continue;

            heldItem = col.gameObject;

            Rigidbody rb = heldItem.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            Collider itemCollider = heldItem.GetComponent<Collider>();
            if (itemCollider != null) itemCollider.enabled = false;

            if (holdPoint != null)
            {
                heldItem.transform.SetParent(holdPoint);
                heldItem.transform.localPosition = Vector3.zero;
                heldItem.transform.localRotation = Quaternion.identity;
            }

            isInspecting = false;
            break;
        }
    }

    private void DropItem()
    {
        if (heldItem == null) return;

        Rigidbody rb = heldItem.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;

        Collider itemCollider = heldItem.GetComponent<Collider>();
        if (itemCollider != null) itemCollider.enabled = true;

        heldItem.transform.SetParent(null);
        if (rb != null) rb.AddForce(transform.forward * 2f, ForceMode.Impulse);

        heldItem = null;
    }

    private void ToggleInspect()
    {
        isInspecting = !isInspecting;

        if (isInspecting)
        {
            if (inspectPoint != null)
            {
                heldItem.transform.SetParent(inspectPoint);
                heldItem.transform.localPosition = Vector3.zero;
            }
        }
        else
        {
            if (holdPoint != null)
            {
                heldItem.transform.SetParent(holdPoint);
                heldItem.transform.localPosition = Vector3.zero;
                heldItem.transform.localRotation = Quaternion.identity;
            }
        }

        // Khóa/mở camera + di chuyển đồng bộ với trạng thái xem đồ
        SetInspectingScriptsEnabled(!isInspecting);
    }

    private void SetInspectingScriptsEnabled(bool enabled)
    {
        if (scriptsToDisableWhileInspecting == null) return;

        foreach (var script in scriptsToDisableWhileInspecting)
        {
            if (script != null)
                script.enabled = enabled;
        }
    }

    private void RotateItem()
    {
        if (mainCamera == null || heldItem == null) return;

        float mouseX = Mouse.current.delta.x.ReadValue() * rotateSpeed;
        float mouseY = Mouse.current.delta.y.ReadValue() * rotateSpeed;

        heldItem.transform.Rotate(mainCamera.transform.up, -mouseX, Space.World);
        heldItem.transform.Rotate(mainCamera.transform.right, mouseY, Space.World);
    }
}