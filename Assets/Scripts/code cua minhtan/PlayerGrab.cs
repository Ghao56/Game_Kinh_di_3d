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
    private ThrowableSelector hotbar;

    // Món tiêu dùng (muối/rượu) đang được ngắm qua ray tâm màn hình.
    // TruckLoadZone đọc để báo rõ "không bán được" khi đứng gần xe.
    private GameObject aimedConsumable;

    // Kho dùng chung của player (cộng số lượng muối/rượu).
    private PlayerInventory inventoryRef;

    // Biến quản lý đèn pin
    private Light itemLight;
    private Quaternion defaultLightRotation;

    // ===== Thoại ngắn khi kho/hotbar hết chỗ trống liên tiếp =====
    // Dùng lại DialogueManager sẵn có của project (không tạo hệ thoại mới).
    private const string FullHandsDialogueLine = "Tay mình hết chỗ rồi!";
    private const string PlayerSpeakerName = "Player";

    // Chống spam: mỗi lần nhặt thất bại chỉ báo một lần, không báo lặp khi
    // người chơi bấm/giữ E liên tục.
    private const float FullHandsDialogueCooldown = 1.5f;
    private float nextFullHandsDialogueTime;

    private void Awake()
    {
        mainCamera = Camera.main;
        if (promptUI != null) promptUI.SetActive(false);
        interactor = GetComponentInParent<Interactor>();
        if (interactor == null) interactor = FindFirstObjectByType<Interactor>();

        hotbar = FindFirstObjectByType<ThrowableSelector>();
    }

    // ================= FIX BUG 1: chọn hotbar <-> đồ trên tay =================
    // Trước đây bấm 1-4 chỉ đổi ô đang chọn trên UI, đồ trên tay không đổi:
    // giữ loot suốt. Giờ đây lựa chọn hotbar điều khiển tay:
    //   - Slot chứa loot       -> nhấc đúng vật thể đó lên tay.
    //   - Slot throwable/trống -> CẤT loot về hotbar (ẩn đi, vật thể còn
    //                            nguyên, không vứt, không nhân đôi).
    private bool selectionHooked;

    private void EnsureHotbarHooked()
    {
        if (hotbar == null) hotbar = FindFirstObjectByType<ThrowableSelector>();
        if (hotbar == null || selectionHooked) return;

        hotbar.OnSelectionChanged += OnHotbarSelectionChanged;
        selectionHooked = true;

        SyncHeldWithSelection();
    }

    private void OnEnable() => EnsureHotbarHooked();

    private void OnDisable()
    {
        if (hotbar != null && selectionHooked)
            hotbar.OnSelectionChanged -= OnHotbarSelectionChanged;

        selectionHooked = false;
    }

    private void OnHotbarSelectionChanged() => SyncHeldWithSelection();

    private LootItem GetSelectedLoot()
    {
        if (hotbar == null) return null;

        HotbarEntry entry = hotbar.SelectedEntry;
        return entry != null ? entry.Loot : null;
    }

    // Giữ tay khớp với lựa chọn hotbar. An toàn gọi nhiều lần: cùng một loot
    // trên tay thì bỏ qua; đồ KHÔNG thuộc hotbar thì không tự ý cất/vứt.
    private void SyncHeldWithSelection()
    {
        LootItem target = GetSelectedLoot();

        if (target == null)
        {
            // Không chọn loot nào: cất loot đang cầm về hotbar (nếu có).
            StashHeldLoot();
            return;
        }

        if (heldItem != null)
        {
            if (heldItem == target.gameObject)
                return; // đã giữ đúng món này rồi

            // Tay đang giữ món không do hotbar quản lý: đừng đụng vào.
            if (heldItem.GetComponentInParent<LootItem>() == null)
                return;

            StashHeldLoot();
        }

        PresentLoot(target.gameObject);
    }

    // Cất món loot đang cầm vào "túi": ẩn vật thể, đông cứng, GIỮ NGUYÊN ô
    // hotbar của nó. Chỉ loot ĐÃ đăng ký trong hotbar mới được cất tự động;
    // đồ lạ (không do hotbar quản lý) không bao giờ bị ẩn mất.
    private void StashHeldLoot()
    {
        GameObject item = heldItem;
        if (item == null) return;

        LootItem loot = item.GetComponentInParent<LootItem>();
        if (loot == null || hotbar == null || hotbar.GetEntryOf(loot) == null)
            return;

        heldItem = null;
        RestoreItemLight();

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        Collider itemCollider = item.GetComponent<Collider>();
        if (itemCollider != null) itemCollider.enabled = false;

        if (holdPoint != null && item.transform.parent != holdPoint)
            item.transform.SetParent(holdPoint, true);

        item.SetActive(false);
    }

    // Nhấc một món loot lên tay: bật lại, đóng băng, gắn vào holdPoint.
    private void PresentLoot(GameObject item)
    {
        if (item == null) return;

        item.SetActive(true);

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        Collider itemCollider = item.GetComponent<Collider>();
        if (itemCollider != null) itemCollider.enabled = false;

        if (holdPoint != null)
        {
            item.transform.SetParent(holdPoint);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
        }

        heldItem = item;

        // Lấy component Light của vật phẩm để điều khiển
        itemLight = item.GetComponentInChildren<Light>();
        if (itemLight != null)
        {
            defaultLightRotation = itemLight.transform.localRotation;
        }
    }

    // Trả đèn (nếu có) của món về trạng thái gốc và bỏ tham chiếu.
    private void RestoreItemLight()
    {
        if (itemLight == null) return;

        itemLight.transform.localRotation = defaultLightRotation;
        itemLight = null;
    }

    // Đăng ký món loot THẬT vào hotbar ngay khi nhặt được. Nếu không đủ dãy ô
    // liên tiếp, trả false và PlayerGrab sẽ không nhặt (vật vẫn còn nguyên).
    private bool TryRegisterHeldLoot(GameObject item)
    {
        if (item == null) return true;

        LootItem loot = item.GetComponentInParent<LootItem>();
        if (loot == null) return true;

        if (hotbar == null) hotbar = FindFirstObjectByType<ThrowableSelector>();
        if (hotbar == null) return true; // scene không có hotbar: giữ hành vi cũ

        EnsureHotbarHooked();
        return hotbar.TryRegisterLoot(loot);
    }

    // Thả/chất/huỷ món chỉ định -> giải phóng mọi ô hotbar của món đó.
    private void ReleaseHeldLoot(GameObject item)
    {
        if (item == null) return;

        LootItem loot = item.GetComponentInParent<LootItem>();
        if (loot == null) return;

        if (hotbar == null) hotbar = FindFirstObjectByType<ThrowableSelector>();
        if (hotbar != null) hotbar.RemoveLoot(loot);
    }

    // Chỉ báo thoại khi nhặt thất bại ĐÚNG vì kho/hotbar thiếu dãy ô trống
    // liên tiếp (không báo cho các lỗi nhặt khác). Kiểm tra phân bổ ô thật
    // của kho bằng HasSpaceForLoot, không dựa vào việc tay đang cầm gì.
    private void ShowFullHandsDialogue(GameObject item)
    {
        LootItem loot = item != null ? item.GetComponentInParent<LootItem>() : null;
        if (loot == null || hotbar == null || hotbar.HasSpaceForLoot(loot))
            return;

        if (Time.unscaledTime < nextFullHandsDialogueTime)
            return;

        DialogueManager dialogue = DialogueManager.Instance;
        if (dialogue == null || dialogue.IsActive)
            return;

        nextFullHandsDialogueTime = Time.unscaledTime + FullHandsDialogueCooldown;
        dialogue.ShowLine(FullHandsDialogueLine, PlayerSpeakerName, autoAdvance: true);
    }

    private void Update()
    {
        CheckInteractableLook();

        // Ray tâm màn hình tìm muối/rượu (SaltBagPickup) - dùng cho cả phím E
        // lẫn prompt "không bán được" của vùng xe. Đặt trước để TruckLoadZone
        // đọc được giá trị mới nhất của frame.
        aimedConsumable = FindAimedConsumable();

        if (Keyboard.current == null) return;

        // 1. Phím E: Tương tác tủ hoặc Nhặt/Vứt đồ
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            // Ô chất đồ trên xe vẫn giành ưu tiên như cũ.
            if (IsTargetingCell())
            {
                // Đang giữ đồ: để Interactor xử lý giữ E chất, không vứt.
                if (heldItem == null)
                    TryPickUp(toggleCabinetFirst: true);
            }
            else if (heldItem == null)
            {
                TryPickUp(toggleCabinetFirst: true);
            }
            // FIX BUG 2: đang giữ loot vẫn nhặt thêm được nếu đang ngắm đúng
            // một món loot khác (hotbar sẽ quyết định còn đủ dãy ô hay không).
            // Muối/rượu nhặt kiểu tiêu dùng (cộng stack) cũng được ngắm để bấm E.
            else if (IsHoldingLoot() && (IsAimingAtLoot() || IsAimingAtConsumable()))
            {
                TryPickUp(toggleCabinetFirst: false);
            }
            else
            {
                DropItem();
            }
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

    // Đang giữ một món loot ĐÃ đăng ký trong hotbar (rượu/muối không tính).
    private bool IsHoldingLoot()
    {
        if (heldItem == null || hotbar == null) return false;

        LootItem loot = heldItem.GetComponentInParent<LootItem>();
        return loot != null && hotbar.GetEntryOf(loot) != null;
    }

    // Đang ngắm đúng một món loot (tag "Item") trong tầm tương tác.
    private bool IsAimingAtLoot()
    {
        if (mainCamera == null) return false;

        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactMask, QueryTriggerInteraction.Ignore))
            return hit.collider != null && hit.collider.CompareTag("Item");

        return false;
    }

    // Đang ngắm một món tiêu dùng (muối/rượu có SaltBagPickup).
    private bool IsAimingAtConsumable() => aimedConsumable != null;

    // Ray tâm màn hình CHỈ bắt collider của muối/rượu. Collider của chúng là
    // trigger nên ray thường (QueryTriggerInteraction.Ignore) không thể trúng,
    // phải dùng ray riêng với Collide rồi lọc đúng SaltBagPickup - không cướp
    // ray của cửa/vùng xe/tủ (những cái không có SaltBagPickup đều bị bỏ qua).
    private GameObject FindAimedConsumable()
    {
        if (mainCamera == null) return null;

        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactMask, QueryTriggerInteraction.Collide))
        {
            if (hit.collider != null)
            {
                SaltBagPickup bag = hit.collider.GetComponentInParent<SaltBagPickup>();
                if (bag != null) return bag.gameObject;
            }
        }

        return null;
    }

    private void EnsurePlayerInventory()
    {
        if (inventoryRef == null) inventoryRef = FindFirstObjectByType<PlayerInventory>();
    }

    // Món tiêu dùng (muối/rượu) đang ngắm - TruckLoadZone đọc để báo "không bán được".
    public GameObject AimedConsumable => aimedConsumable;

    // Trúng tủ tương tác qua ray tâm màn hình -> mở/đóng ngay. Trả về true nếu đã xử lý.
    private bool TryToggleAimedCabinet()
    {
        if (mainCamera == null) return false;

        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactMask, QueryTriggerInteraction.Ignore))
        {
            CabinetController cabinet = hit.collider.GetComponent<CabinetController>();
            if (cabinet != null)
            {
                cabinet.ToggleCabinet();
                return true;
            }
        }

        return false;
    }

    private void TryPickUp(bool toggleCabinetFirst)
    {
        if (toggleCabinetFirst && TryToggleAimedCabinet())
            return;

        // Ưu tiên món đang ngắm: loot (tag "Item", ray thường) hoặc muối/rượu
        // (SaltBagPickup, ray bắt trigger). Ngắm loot thì ưu tiên loot.
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

        GameObject aimedBag = aimedItem != null ? null : FindAimedConsumable();
        GameObject aimTarget = aimedItem != null ? aimedItem : aimedBag;

        EnsurePlayerInventory();

        Collider[] colliders = Physics.OverlapSphere(transform.position, pickupRange);
        foreach (Collider col in colliders)
        {
            SaltBagPickup bag = col.GetComponentInParent<SaltBagPickup>();
            bool isConsumable = bag != null;
            bool isLoot = !isConsumable && col.CompareTag("Item");
            if (!isConsumable && !isLoot) continue;

            // Chỉ nhận đúng món đang ngắm (nếu có ngắm mục nào đó).
            if (aimTarget != null)
            {
                GameObject colObj = isConsumable ? bag.gameObject : col.gameObject;
                if (colObj != aimTarget) continue;
            }

            if (isConsumable)
            {
                // Muối/rượu: nhặt kiểu tiêu dùng - cộng stack ×N ngay (không
                // cầm lên tay). Hết chỗ -> SaltBagPickup đã báo thoại; dừng ở
                // đây, không âm thầm nhặt món khác cho đến khi hết chỗ.
                if (!bag.TryPickUpConsumable(inventoryRef)) return;
                break;
            }

            // Hotbar phải nhận được món này trước khi ta nhấc nó lên.
            // Không đủ ô liên tiếp -> bỏ qua, không làm mất vật phẩm.
            if (!TryRegisterHeldLoot(col.gameObject))
            {
                Debug.LogWarning($"[PlayerGrab] Hotbar không đủ ô cho {col.gameObject.name}.");
                // Đúng nguyên nhân hết chỗ: hiện thoại người chơi (một lần).
                ShowFullHandsDialogue(col.gameObject);
                return;
            }

            // FIX: đang giữ loot khác -> cất nó về hotbar (giữ nguyên ô của nó),
            // rồi nhấc món mới lên tay. Món cũ không bị huỷ/vứt/nhân đôi.
            if (heldItem != null && heldItem != col.gameObject)
                StashHeldLoot();

            PresentLoot(col.gameObject);

            // Giữ lựa chọn hotbar khớp với món vừa nhấc lên tay.
            LootItem pickedLoot = col.gameObject.GetComponentInParent<LootItem>();
            if (hotbar != null && pickedLoot != null)
            {
                HotbarEntry entry = hotbar.GetEntryOf(pickedLoot);
                if (entry != null)
                    hotbar.SelectSlot(entry.StartSlot);
            }

            break;
        }
    }

    private void DropItem()
    {
        if (heldItem == null) return;

        // Null-out TRƯỚC khi RemoveLoot: việc bỏ món khỏi hotbar có thể bung
        // sự kiện đổi lựa chọn (nhấc món khác lên tay) — không được cầm/vứt
        // lẫn món đang thả xuống đất.
        GameObject item = heldItem;
        heldItem = null;

        RestoreItemLight();
        ReleaseHeldLoot(item);

        ResetItemPhysics(item);

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null) rb.AddForce(transform.forward * 2f, ForceMode.Impulse);
    }

    private void TryPlaceItem()
    {
        if (mainCamera == null || heldItem == null) return;

        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, placeRange, placeSurfaceMask, QueryTriggerInteraction.Ignore))
        {
            // Null-out trước khi RemoveLoot (xem chú thích trong DropItem).
            GameObject item = heldItem;
            heldItem = null;

            RestoreItemLight();
            ReleaseHeldLoot(item);

            item.transform.SetParent(null);
            item.transform.position = hit.point;
            item.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);

            ResetItemPhysics(item);
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
        heldItem = null;

        // Null-out trước khi RemoveLoot (xem chú thích trong DropItem):
        // sự kiện đổi lựa chọn có thể nhấc loot khác lên tay ngay sau đó.
        RestoreItemLight();
        ReleaseHeldLoot(item);

        // isKinematic + collider tắt giữ nguyên như lúc cầm: đồ nằm yên trong ô.
        return item;
    }

    // Đang nhắm ô chất đồ: nhấn E phải là "giữ để chất", không được vứt đồ.
    private bool IsTargetingCell()
    {
        if (interactor == null) interactor = FindFirstObjectByType<Interactor>();
        return interactor != null && interactor.CurrentTarget is TruckLoadZone;
    }

    private void ResetItemPhysics(GameObject item)
    {
        if (item == null) return;

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;

        Collider itemCollider = item.GetComponent<Collider>();
        if (itemCollider != null) itemCollider.enabled = true;

        item.transform.SetParent(null);
        if (!item.activeSelf) item.SetActive(true);
    }
}