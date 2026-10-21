using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlayerGrab : MonoBehaviour
{
    [Header("Vị trí")]
    [SerializeField] private Transform holdPoint;
    [SerializeField] private Transform inspectPoint;

    [Header("Cài đặt")]
    [SerializeField] private float pickupRange = 3f;
    [SerializeField] private float interactRange = 5f;
    [SerializeField] private float placeRange = 3f;
    [SerializeField] private float rotateSpeed = 0.5f;
    [SerializeField] private LayerMask placeSurfaceMask = ~0;
    [SerializeField] private LayerMask interactMask = ~0;

    [Header("Giao diện Hướng dẫn Tương tác")]
    [SerializeField] private GameObject promptUI;

    [Header("Khóa Camera Khi Đang Xem Đồ (bấm R)")]
    [SerializeField] private List<Behaviour> scriptsToDisableWhileInspecting;

    private GameObject heldItem;
    private bool isInspecting = false;
    private Camera mainCamera;

    // Biến quản lý đèn pin
    private Light itemLight;
    private Quaternion defaultLightRotation;

    private void Awake()
    {
        mainCamera = Camera.main;
        if (promptUI != null) promptUI.SetActive(false);
    }

    private void OnDisable()
    {
        if (isInspecting)
            SetInspectingScriptsEnabled(true);
    }

    private void Update()
    {
        CheckInteractableLook();

        if (Keyboard.current == null) return;

        // 1. Phím E: Tương tác tủ hoặc Nhặt/Vứt đồ
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (heldItem == null)
                TryPickUp();
            else if (!isInspecting)
                DropItem();
        }

        // 2. Phím R: Xoay xem đồ
        if (Keyboard.current.rKey.wasPressedThisFrame && heldItem != null)
        {
            ToggleInspect();
        }

        // 3. Chuột Trái: Bật/tắt đèn hoặc Đặt đồ xuống
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && heldItem != null)
        {
            if (!isInspecting)
            {
                if (itemLight != null)
                {
                    itemLight.enabled = !itemLight.enabled;
                }
                else
                {
                    TryPlaceItem();
                }
            }
        }

        // 4. Xoay đồ vật khi ở chế độ xem (Phím R)
        if (isInspecting && heldItem != null && Mouse.current != null)
        {
            RotateItem();
        }
    }

    private void LateUpdate()
    {
        // Khi đang xoay kiểm tra điện thoại, khóa góc quay của đèn pin theo hướng camera để đèn không bị quay lồng lộn theo chuột
        if (isInspecting && itemLight != null && mainCamera != null)
        {
            itemLight.transform.rotation = mainCamera.transform.rotation;
        }
    }

    private void CheckInteractableLook()
    {
        if (mainCamera == null || promptUI == null) return;

        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactMask, QueryTriggerInteraction.Ignore))
        {
            CabinetController cabinet = hit.collider.GetComponent<CabinetController>();
            if (cabinet != null)
            {
                promptUI.SetActive(true);
                return;
            }
        }
        promptUI.SetActive(false);
    }

    private void TryPickUp()
    {
        if (mainCamera != null)
        {
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactMask, QueryTriggerInteraction.Ignore))
            {
                CabinetController cabinet = hit.collider.GetComponent<CabinetController>();
                if (cabinet != null)
                {
                    cabinet.ToggleCabinet();
                    return;
                }
            }
        }

        Collider[] colliders = Physics.OverlapSphere(transform.position, pickupRange);
        foreach (Collider col in colliders)
        {
            if (!col.CompareTag("Item")) continue;

            // Túi đồ theo ô: đồ loot có LootItem sẽ tự bay vào túi, không cầm tay
            LootItem loot = col.GetComponentInParent<LootItem>();
            var bag = PlayerLootBag.Instance;
            if (loot != null && bag != null)
            {
                if (!bag.TryTake(loot))
                {
                    Debug.LogWarning($"Túi đầy ({bag.UsedSlots}/{bag.Capacity}) — không nhặt được {loot.itemName} (chiếm {loot.slotCost} ô)");
                    break;
                }

                Debug.Log($"Nhặt {loot.itemName} ({loot.valueInMillions}tr) vào túi ({bag.UsedSlots}/{bag.Capacity})");
                Destroy(loot.gameObject);
                return;
            }

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

            // Lấy component Light của vật phẩm để điều khiển
            itemLight = heldItem.GetComponentInChildren<Light>();
            if (itemLight != null)
            {
                defaultLightRotation = itemLight.transform.localRotation;
            }

            isInspecting = false;
            break;
        }
    }

    private void DropItem()
    {
        if (heldItem == null) return;

        if (itemLight != null)
        {
            itemLight.transform.localRotation = defaultLightRotation;
            itemLight = null;
        }

        ResetItemPhysics();

        Rigidbody rb = heldItem.GetComponent<Rigidbody>();
        if (rb != null) rb.AddForce(transform.forward * 2f, ForceMode.Impulse);

        heldItem = null;
        SetInspectingState(false);
    }

    private void TryPlaceItem()
    {
        if (mainCamera == null || heldItem == null) return;

        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, placeRange, placeSurfaceMask, QueryTriggerInteraction.Ignore))
        {
            if (itemLight != null)
            {
                itemLight.transform.localRotation = defaultLightRotation;
                itemLight = null;
            }

            heldItem.transform.SetParent(null);
            heldItem.transform.position = hit.point;
            heldItem.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);

            ResetItemPhysics();
            heldItem = null;
            SetInspectingState(false);
        }
    }

    private void ToggleInspect()
    {
        SetInspectingState(!isInspecting);

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
                if (itemLight != null)
                {
                    itemLight.transform.localRotation = defaultLightRotation;
                }
                heldItem.transform.SetParent(holdPoint);
                heldItem.transform.localPosition = Vector3.zero;
                heldItem.transform.localRotation = Quaternion.identity;
            }
        }
    }

    private void SetInspectingState(bool inspecting)
    {
        isInspecting = inspecting;
        SetInspectingScriptsEnabled(!inspecting);
    }

    private void SetInspectingScriptsEnabled(bool enabled)
    {
        if (scriptsToDisableWhileInspecting == null) return;

        foreach (var script in scriptsToDisableWhileInspecting)
        {
            if (script != null && !(script is Camera))
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

    private void ResetItemPhysics()
    {
        if (heldItem == null) return;

        Rigidbody rb = heldItem.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;

        Collider itemCollider = heldItem.GetComponent<Collider>();
        if (itemCollider != null) itemCollider.enabled = true;

        heldItem.transform.SetParent(null);
    }
}