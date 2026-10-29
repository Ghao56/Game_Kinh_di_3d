using System.Collections.Generic;
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

    [Header("Bỏ muối/rượu ra đất bằng Q")]
    [Tooltip("Danh sách itemId vứt được ra đất (canonical: Salt, Alcohol). Thứ tự khớp luôn với danh sách prefab bên dưới.")]
    [SerializeField] private string[] consumableDropIds = { "Salt", "Alcohol" };
    [Tooltip("Prefab model vứt ra đất cho từng itemId trên. Món vứt ra tự được gắm SaltBagPickup (itemId, ×1) + collider để có thể nhặt lại bằng E.")]
    [SerializeField] private GameObject[] consumableDropPrefabs = new GameObject[0];

    [Header("Giao diện Hướng dẫn Tương tác")]
    [SerializeField] private GameObject promptUI;

    [Header("Chốt cửa xe (khóa nhặt đồ trong khoang)")]
    [Tooltip("Quest ĐÓNG CỬA XE. Khi quest này đang chạy hoặc đã xong, món loot nằm trong khoang xe KHÔNG được nhặt nữa — bấm nhầm E gần cửa hậu sẽ nhặt trộm món đã chất và tụt tiến độ nhiệm vụ.")]
    [SerializeField] private string truckClosingQuestId = "dong_cua";

    private GameObject heldItem;
    private Interactor interactor;
    private Camera mainCamera;
    private ThrowableSelector hotbar;

    // Món tiêu dùng (muối/rượu) đang được ngắm qua ray tâm màn hình.
    // Chỉ dùng để CHẶN VỨT nhầm khi bấm E (nhặt thực tế do Interactor lo);
    // TruckLoadZone đọc để không giành prompt khi người chơi NGẮM muối/rượu.
    private GameObject aimedConsumable;

    // Kho player (lấy số lượng/trừ khi vứt muối/rượu).
    private PlayerInventory inventoryCache;

    // Cảnh báo thiếu prefab vứt chỉ 1 lần mỗi loại đồ.
    private readonly HashSet<string> warnedMissingDropPrefab = new();

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

        CompensateHoldPointScale();
    }

    // ================= FIX: nhặt đồ thì scale "nhảy" 1 -> 1.25 =================
    // HoldPoint là con của PlayerCharacter có scale không đều (0.8, 1, 0.8).
    // SetParent mặc định của Unity giữ nguyên KÍCH THƯỚC WORLD nên tự tính lại
    // localScale món đồ = worldScale / scale-cha (1 -> 1/0.8 = 1.25): số trong
    // Inspector nhảy dù kích thước thật không đổi, và scale lạ có thể dính lại
    // trên món đồ về sau.
    // Fix: bù NGƯỢC scale không đều lên chính HoldPoint (scale local nhân 1/x
    // theo từng trục) để chuỗi cha của nó thu về (1,1,1) — localScale món đồ
    // giữ nguyên tuyệt đối khi nhặt, cất, thả, chất lên xe. HoldPoint vẫn là
    // con của PlayerCharacter nên bám theo nhân vật như cũ.
    private void CompensateHoldPointScale()
    {
        if (holdPoint == null) return;

        Vector3 lossy = holdPoint.lossyScale;
        bool uniform = Mathf.Approximately(lossy.x, lossy.y)
                    && Mathf.Approximately(lossy.y, lossy.z);
        if (uniform) return;

        Vector3 local = holdPoint.localScale;
        holdPoint.localScale = new Vector3(
            lossy.x > 0.0001f ? local.x / lossy.x : local.x,
            lossy.y > 0.0001f ? local.y / lossy.y : local.y,
            lossy.z > 0.0001f ? local.z / lossy.z : local.z);
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

    // Đoạn kết: cất MỌI THỨ đang cầm trên tay để tay trống (giữ nguyên ô hotbar,
    // đồ ẩn đi như đã bỏ vào túi — không vứt, không mất tiến độ nhiệm vụ).
    // Gọi một lần NGAY khi đoạn kết bắt đầu, TRƯỚC khi PlayerGrab bị khoá;
    // nếu khoá PlayerGrab trước thì món kẹt nguyên trên tay suốt màn đen.
    public void StashHandsClear()
    {
        GameObject item = heldItem;
        if (item == null) return;

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

    // Trạng thái của một lượt chơi: quest "đóng cửa xe" đang CHẠY hoặc ĐÃ XONG
    // => giai đoạn loot coi như chốt, món nằm trong khoang xe bị khóa nhặt.
    private bool IsTruckClosingPhase()
    {
        QuestManager manager = QuestManager.Instance;
        if (manager == null) return false;

        QuestState state = manager.GetState(truckClosingQuestId);
        return state == QuestState.Active || state == QuestState.Completed;
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

        // Ray tâm màn hình tìm muối/rượu (SaltBagPickup) mỗi frame: chỉ dùng
        // để chặn vứt đồ khi bấm E lên món tiêu dùng, và để TruckLoadZone
        // nhường prompt cho món tiêu dùng đang được ngắm.
        aimedConsumable = FindAimedConsumable();

        if (Keyboard.current == null) return;

        // 1. Phím E: Tương tác tủ hoặc Nhặt đồ (KHÔNG còn vứt - vứt là Q)
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            // ƯU TIÊN 1: đang nhắm vào 1 CỬA (cửa hậu xe, cửa xe lái, cửa nhà - Interactor
            // đã chọn nó làm target sẵn sàng) -> E thuộc về CỬA (đóng/mở). KHÔNG nhặt
            // món chắn/ngay sau cửa (đặc biệt món đã chất trong khoang xe) — đây là
            // nguyên nhân "bấm E đóng cửa thì nó cứ nhặt luôn món đồ của xe".
            if (IsTargetingDoor())
            {
                // Không làm gì: Interactor tự xử lý đóng/mở cửa bằng cùng phím E.
                // Muốn nhặt thì nhắm thẳng vào món ở chỗ cửa không che (hoặc làm khi
                // quest đóng cửa chưa lên).
            }
            // Ô chất đồ trên xe vẫn giành ưu tiên như cũ.
            else if (IsTargetingCell())
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
            else if (IsHoldingLoot() && IsAimingAtLoot())
            {
                TryPickUp(toggleCabinetFirst: false);
            }
            // Giữ loot + ngắm muối/rượu: Interactor tự nhặt muối/rượu. Giữ loot
            // + không ngắm gì: KHÔNG hành động - bỏ đồ là phím Q.
        }

        // 1.5. Phím Q: bỏ đồ đang cầm, hoặc vứt 1 món muối/rượu ra đất.
        if (Keyboard.current.qKey.wasPressedThisFrame)
            HandleQ();

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

        // Hint "[Q] Bỏ đồ" trên canvas.
        UpdateHintUI();
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

    // Đang ngắm một món tiêu dùng (muối/rượu có SaltBagPickup)? TruckLoadZone
    // đọc để nhường prompt khi người chơi có ý định NHẶT món trên sàn.
    public bool IsAimingAtConsumable() => aimedConsumable != null;

    // "Cầm tương đương loot" cho đồ tiêu dùng: ô đang chọn trên hotbar là
    // muối/rượu (IThrowable). Vùng xe dùng để hiện "không bán được" thay cho
    // "Giữ E để chất đồ lên xe" - đồ hỗ trợ không phải hàng bán.
    public bool IsCarryingConsumable
    {
        get
        {
            if (hotbar == null) hotbar = FindFirstObjectByType<ThrowableSelector>();

            HotbarEntry entry = hotbar != null ? hotbar.SelectedEntry : null;
            return entry != null && entry.Throwable != null;
        }
    }

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

        // Ưu tiên món đang ngắm (tag "Item") qua ray tâm màn hình.
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

            // Đã đủ 50M (quest đóng cửa đang chạy/xong): món trong khoang xe là
            // "đã bán" — không nhặt lại. Tránh bấm E gần cửa hậu nhặt trộm món
            // vừa chất và làm tụt tiến độ nhiệm vụ (ReclaimTakenOutLoot).
            if (IsTruckClosingPhase() && TruckLoadZone.IsItemInLoadZone(col.transform.position))
                continue;

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

    // ------------------------- Q: bỏ đồ / vứt muối-rượu --------------------------

    // Loot đang cầm -> thả xuống đất (như E lần trước, chỉ đổi phím). Đang
    // chọn muối/rượu trên hotbar -> vứt đúng 1 món ra đất, NHẶT LẠI ĐƯỢC.
    private void HandleQ()
    {
        if (heldItem != null)
        {
            DropItem();
            return;
        }

        HotbarEntry entry = hotbar != null ? hotbar.SelectedEntry : null;
        if (entry == null || entry.Throwable == null) return; // không chọn đồ tiêu dùng

        PlayerInventory inventory = EnsureInventory();
        if (inventory == null) return;

        string id = entry.ItemId;
        if (inventory.GetCount(id) <= 0) return;

        // Model để sinh món vứt ra: ưu tiên prefab gán trong Inspector; không
        // có thì lấy 1 món CÙNG itemId còn nằm trên scene làm hình dạng (món
        // gốc không đụng tới, chỉ dùng làm khuôn). Cả hai không thấy: KHÔNG
        // trừ kho, cảnh báo 1 lần - không cho "mất đồ im lặng".
        GameObject model = FindDropPrefabFor(id) ?? FindSceneModelFor(id);
        if (model == null)
        {
            string canonical = PlayerInventory.CanonicalId(id);
            if (warnedMissingDropPrefab.Add(canonical))
            {
                Debug.LogWarning(
                    $"[PlayerGrab] Không có model để vứt '{canonical}' ra đất " +
                    "(PlayerGrab > Bỏ muối/rượu ra đất bằng Q > Consumable Drop " +
                    "Prefabs trống và scene không còn món cùng loại). Q không " +
                    "vứt, đồ không mất.", this);
            }
            return;
        }

        // Trừ 1 đơn vị rồi mới sinh vật thể ra đất (nhặt lại được bằng E).
        if (!inventory.UseItem(id)) return;

        SpawnConsumablePickup(id, model);
    }

    private PlayerInventory EnsureInventory()
    {
        if (inventoryCache == null)
            inventoryCache = GetComponentInParent<PlayerInventory>() ?? FindFirstObjectByType<PlayerInventory>();
        return inventoryCache;
    }

    // Prefab model theo itemId (so sánh qua CanonicalId, không lệ tên gọi).
    private GameObject FindDropPrefabFor(string itemId)
    {
        if (consumableDropIds == null || consumableDropPrefabs == null)
            return null;

        string id = PlayerInventory.CanonicalId(itemId);
        for (int i = 0; i < consumableDropIds.Length && i < consumableDropPrefabs.Length; i++)
        {
            if (consumableDropPrefabs[i] == null) continue;

            if (string.Equals(PlayerInventory.CanonicalId(consumableDropIds[i]), id,
                    System.StringComparison.OrdinalIgnoreCase))
                return consumableDropPrefabs[i];
        }

        return null;
    }

    // Tiện đường (fallback): tìm 1 món SaltBagPickup CÙNG itemId đang nằm
    // trên scene để làm khuôn khi người chơi chưa gán prefab nào. Món gốc
    // KHÔNG bị đụng vào - chỉ dùng Instantiate để nhân bản hình dạng.
    private GameObject FindSceneModelFor(string itemId)
    {
        string id = PlayerInventory.CanonicalId(itemId);

        foreach (SaltBagPickup candidate in
                 FindObjectsByType<SaltBagPickup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (candidate == null) continue;

            if (string.Equals(PlayerInventory.CanonicalId(candidate.itemId), id,
                    System.StringComparison.OrdinalIgnoreCase))
                return candidate.gameObject;
        }

        return null;
    }

    // Sinh một món tiêu dùng rơi ra đất: prefab model + SaltBagPickup(itemId,
    // ×1) + collider + vật lý ném nhẹ. Bỏ SaltBagProjectile nếu có (không còn
    // là đạn stun, chỉ là đồ nhặt lại được).
    private void SpawnConsumablePickup(string itemId, GameObject model)
    {
        string id = PlayerInventory.CanonicalId(itemId);

        Vector3 dropPos = transform.position + transform.forward * 1f + Vector3.up * 0.5f;
        GameObject item = Instantiate(model, dropPos, Quaternion.identity);

        item.tag = "Untagged"; // không tag "Item" để không giành route nhặt loot

        SaltBagProjectile projectile = item.GetComponent<SaltBagProjectile>();
        if (projectile != null) Destroy(projectile);

        SaltBagPickup pickup = item.GetComponent<SaltBagPickup>();
        if (pickup == null) pickup = item.GetComponentInChildren<SaltBagPickup>(true);
        if (pickup == null) pickup = item.AddComponent<SaltBagPickup>();
        pickup.itemId = id;
        pickup.amount = 1;

        // Ray prompt "[E] Nhặt ..." cần collider; prefabs thường có sẵn, nhưng
        // model thuần mesh không có -> tự thêm hộ 1 capsule theo bounds.
        if (item.GetComponent<Collider>() == null && item.GetComponentInChildren<Collider>(false) == null)
        {
            Renderer renderer = item.GetComponentInChildren<Renderer>(false);
            CapsuleCollider capsule = item.AddComponent<CapsuleCollider>();
            if (renderer != null)
            {
                Bounds bounds = renderer.bounds;
                capsule.center = item.transform.InverseTransformPoint(bounds.center);
                Vector3 scale = item.transform.lossyScale;
                capsule.radius = Mathf.Max(0.05f, Mathf.Max(bounds.size.x, bounds.size.z) * 0.4f / Mathf.Max(0.0001f, Mathf.Max(scale.x, scale.z)));
                capsule.height = Mathf.Max(0.15f, bounds.size.y / Mathf.Max(0.0001f, scale.y));
                capsule.direction = 1;
            }
        }

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb == null) rb = item.AddComponent<Rigidbody>();
        rb.mass = 0.2f;
        rb.linearVelocity = transform.forward * 2f + Vector3.up * 1.5f;
    }

    // ----------------------------- Canvas hint ----------------------------------

    // Đẩy chữ cho QDropHintUI (Canvas tạo bằng menu Tools). Chỉ 1 dòng cố
    // định "[Q] Bỏ đồ" như bạn chốt: hiện khi cầm loot hoặc đang chọn
    // muối/rượu trên hotbar, tay trống/không có canvas -> ẩn.
    private void UpdateHintUI()
    {
        QDropHintUI hint = QDropHintUI.Instance;
        if (hint == null) return;

        HotbarEntry selected = hotbar != null ? hotbar.SelectedEntry : null;
        bool canDrop = heldItem != null || (selected != null && selected.Throwable != null);

        hint.Show(canDrop ? "[Q] Bỏ đồ" : null);
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

    // Đang nhắm ô chất đồ (nhấn E phải là "giữ để chất") hoặc vùng thoát trên
    // cửa xe lái của đoạn kết: không nhặt/vứt đồ cho khỏi phá thao tác giữ E.
    private bool IsTargetingCell()
    {
        if (interactor == null) interactor = FindFirstObjectByType<Interactor>();
        if (interactor == null || interactor.CurrentTarget == null) return false;

        return interactor.CurrentTarget is TruckLoadZone
            || interactor.CurrentTarget is TruckEscapeDoor;
    }

    // Đang nhắm 1 CỬA (DoorInteractable) mà cửa sẵn sàng nhận E: Interactor đã
    // chọn cửa làm target -> E phải là mở/đóng cửa, không phải nhặt món gần,
    // sau cửa hay trong khoang xe ("đóng cửa mà tay vẫn vơ lấy đồ").
    private bool IsTargetingDoor()
    {
        if (interactor == null) interactor = FindFirstObjectByType<Interactor>();
        if (interactor == null) return false;

        // CurrentTarget chắc chắn còn IsAvailable (Interactor lọc trước khi chọn),
        // nên chỉ cần nhìn loại: trúng cửa = E dồn hết cho cửa, xoá sạch nhặt/đổi đồ.
        return interactor.CurrentTarget is DoorInteractable;
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