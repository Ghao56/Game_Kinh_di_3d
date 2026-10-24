using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerGrab : MonoBehaviour
{
    [Header("Vị trí")]
    [SerializeField] private Transform holdPoint;

    [Header("Cài đặt")]
    [SerializeField] private float pickupRange = 3f;
    [SerializeField] private float interactRange = 5f;
    [SerializeField] private float placeRange = 3f;
    [SerializeField] private LayerMask placeSurfaceMask = ~0;
    [SerializeField] private LayerMask interactMask = ~0;

    [Header("Giao diện Hướng dẫn Tương tác")]
    [SerializeField] private GameObject promptUI;

    private GameObject heldItem;
    private Interactor interactor;
    private Camera mainCamera;

    // Biến quản lý đèn pin
    private Light itemLight;
    private Quaternion defaultLightRotation;

    private void Awake()
    {
        mainCamera = Camera.main;
        if (promptUI != null) promptUI.SetActive(false);
        interactor = GetComponentInParent<Interactor>();
        if (interactor == null) interactor = FindFirstObjectByType<Interactor>();
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
            else if (!IsTargetingCell())
                DropItem();
        }

        // 2. Chuột Trái: Bật/tắt đèn hoặc Đặt đồ xuống
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && heldItem != null)
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

    private void CheckInteractableLook()
    {
        if (mainCamera == null || promptUI == null) return;

        // Nếu dùng chung với InteractPromptUI của Interactor thì để Interactor lo
        if (promptUI.GetComponent<InteractPromptUI>() != null) return;

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

        // Ưu tiên món đang ngắm (khớp glow): raycast tâm màn hình trước.
        GameObject aimedItem = null;
        if (mainCamera != null)
        {
            Ray aimRay = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            if (Physics.Raycast(aimRay, out RaycastHit aimHit, interactRange, interactMask, QueryTriggerInteraction.Ignore)
                && aimHit.collider != null && aimHit.collider.CompareTag("Item"))
            {
                aimedItem = aimHit.collider.gameObject;
            }
        }

        Collider[] colliders = Physics.OverlapSphere(transform.position, pickupRange);
        foreach (Collider col in colliders)
        {
            if (!col.CompareTag("Item")) continue;
            if (aimedItem != null && col.gameObject != aimedItem) continue;

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
        }
    }

    /// Món đồ đang cầm trên tay (null nếu tay không).
    public GameObject HeldItem => heldItem;

    /// Bỏ món đang cầm ra khỏi tay mà KHÔNG thả xuống đất (dùng khi chất đồ lên xe).
    /// Trả về gameObject để ô chất đồ đặt vào chỗ; trả null nếu tay đang trống.
    public GameObject TakeHeld()
    {
        if (heldItem == null) return null;

        GameObject item = heldItem;

        if (itemLight != null)
        {
            itemLight.transform.localRotation = defaultLightRotation;
            itemLight = null;
        }

        // isKinematic + collider tắt giữ nguyên như lúc cầm: đồ nằm yên trong ô.
        heldItem = null;
        return item;
    }

    // Đang nhắm ô chất đồ: nhấn E phải là "giữ để chất", không được vứt đồ.
    private bool IsTargetingCell()
    {
        if (interactor == null) interactor = FindFirstObjectByType<Interactor>();
        return interactor != null && interactor.CurrentTarget is TruckLoadZone;
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