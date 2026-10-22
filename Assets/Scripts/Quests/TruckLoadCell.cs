using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Ô chất đồ lên xe tải. Cửa hậu mở hết thì cube xanh và VÙNG SÀN sáng hiện (Ready).
// Vùng sáng muốn tự chỉnh kích thước: tạo Cube, thêm component TruckLoadZone, kéo ô vào.
// Không có vùng tùy chỉnh nào thì ô tự sinh quad phát sáng "VungChatDoTrenXe" làm dự phòng.
// Player đứng trong vùng là prompt "Giữ E để chất đồ lên xe" hiện luôn, không cần nhìn vào ô.
// Cầm món loot hợp lệ hoặc túi có đồ, GIỮ E đủ holdDuration:
// một lần giữ = chất hết món trên tay VÀ toàn bộ túi (PlayerLootBag), chia vào các ô trống.
// Rượu/muối (PlayerInventory/không LootItem) không thuộc bị từ chối "Món đồ này không đáng giá."
public class TruckLoadCell : MonoBehaviour, IInteractable, IInteractableAvailability, IProximityInteractable
{
    private enum HeldState { None, Invalid, Valid }

    [Header("Tham chiếu")]
    [Tooltip("Cửa hậu cánh chính (DoorInteractable có linkedDoors).")]
    [SerializeField] private DoorInteractable rearDoor;

    [Tooltip("Player để đọc món đang cầm trên tay.")]
    [SerializeField] private PlayerGrab playerGrab;

    [Tooltip("Điểm đặt món đồ trong ô. Để trống thì dùng chính vị trí ô này.")]
    [SerializeField] private Transform placeAnchor;

    [Header("Cài đặt")]
    [Tooltip("Số giây phải giữ E để chất. Thả E giữa chừng = hủy, đồ không mất gì.")]
    [SerializeField, Min(0.1f)] private float holdDuration = 1.2f;

    [Tooltip("Danh sách itemId ô này nhận. Để trống = mọi món có LootItem/itemId đều dùng được.")]
    [SerializeField] private string[] validItemIds = new string[0];

    [Header("Báo nhiệm vụ")]
    [Tooltip("Mỗi lần chất xong cộng 1 cho quest/mục tiêu này (Chất đồ lên xe 0/3).")]
    [SerializeField] private string loadQuestId = "loot_truck";
    [SerializeField] private string loadObjectiveId = "chat_do";

    [Header("Sự kiện")]
    [Tooltip("Bung khi một món đồ vừa nằm hẳn trong ô.")]
    [SerializeField] private UnityEvent onFilled;

    [Header("Prompt")]
    [SerializeField] private string promptWhenReady = "Giữ E để chất đồ lên xe";
    [SerializeField] private string promptBagOnly = "Giữ E để cất đồ trong túi lên xe";
    [SerializeField] private string promptDoorClosed = "Hãy mở cửa hậu trước";
    [SerializeField] private string promptInvalid = "Món đồ này không đáng giá.";

    [Header("Vùng chất đồ trên sàn")]
    [Tooltip("Màu quad phát sáng tự sinh khi cửa hậu mở. Đổi nếu muốn xanh khác.")]
    [SerializeField] private Color zoneColor = new Color(0f, 1f, 0.3f, 0.55f);

    [Tooltip("GameObject vùng tùy chỉnh (VD: Zoneputitem, được THÊM TruckLoadZone vào trước). Đặt OFF trong scene cũng được: ô sẽ tự bật khi cửa hậu mở + còn ô trống, tự tắt khi hết việc. Điền vào MỘT ô là đủ. Trống thì ô đầu tiên tự sinh quad dự phòng.")]
    [SerializeField] private GameObject loadZone;

    public string PromptText
    {
        get
        {
            if (!DoorFullyOpen) return promptDoorClosed;

            HeldState held = EvaluateHeld();
            if (held == HeldState.Valid) return promptWhenReady;
            if (held == HeldState.Invalid) return promptInvalid;
            if (HasBagGoods()) return promptBagOnly;
            return promptWhenReady;
        }
    }

    public bool HoldToInteract => true;
    public float HoldDuration => holdDuration;

    // Player ĐỨNG trong vùng (trigger của ô hiện enabled) là đủ điều kiện prompt.
    public bool ReadyForProximityPrompt => IsAvailable && ProximityContainsPlayer();

    // Đọc cho TruckLoadZone chốt hiển thị.
    public bool DoorOpenNow => DoorFullyOpen;
    public bool AllCellsFilled => !HasEmptyCell();

    public bool IsAvailable
    {
        get
        {
            // Cửa chưa mở hết: chỉ để hiện prompt cảnh báo "Hãy mở cửa hậu trước" / "không đáng giá".
            if (!DoorFullyOpen) return HasAnythingToDeposit();
            if (EvaluateHeld() != HeldState.None) return true; // hợp lệ: chất; không hợp lệ: hiện "không đáng giá"
            return HasBagGoods();
        }
    }

    // Mỗi itemId chỉ được tính đã chất một lần trong cả xe, kể cả món ở ô khác.
    private static readonly HashSet<string> loadedIds = new HashSet<string>();
    private static readonly List<TruckLoadCell> RegisteredCells = new List<TruckLoadCell>();

    // Ô nào trong xe còn trống để nhận đồ (dùng bởi vùng tùy chỉnh TruckLoadZone và fallback quad).
    public bool ZoneReady => DoorFullyOpen && HasEmptyCell();

    // Quad fallback tự sinh khi scene chưa có vùng tùy chỉnh (TruckLoadZone). Có vùng tùy chỉnh thì quad này ẩn hẳn.
    private Renderer zoneRenderer;
    private Material zoneMaterial;
    private bool ownsFallbackQuad;

    // Object vùng tùy chỉnh trong scene được nhận theo TÊN này (có thể để OFF sẵn trong scene).
    private const string CustomZoneName = "VungChatDoTrenXe";
    private const string CustomZoneName2 = "Zoneputitem";

    // Trạng thái đã chốt của vùng (latch): anti-flicker.
    private float zoneHoldSeconds = 0.4f;
    private float zoneOffTimer;
    private bool zoneShown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        loadedIds.Clear();
        RegisteredCells.Clear();
    }

    private bool filled;
    private Renderer cubeRenderer;
    private Collider detectCollider;

    private bool DoorFullyOpen => rearDoor != null && rearDoor.IsFullyOpen;

    // ------------------------------------------------------------ tương tác

    public void OnInteract(Interactor interactor)
    {
        if (!DoorFullyOpen) return;

        // 1) Món trên tay: vào ô này nếu trống, không thì ô trống kế bên.
        HeldState hand = EvaluateHeld();
        if (hand == HeldState.Valid)
        {
            TruckLoadCell handTarget = !filled ? this : FindEmptyCell();
            if (handTarget != null) PlaceLoot(handTarget, playerGrab.TakeHeld());
        }

        // 2) Toàn bộ túi: mỗi món vào một ô trống.
        PlayerLootBag bag = PlayerLootBag.Instance;
        if (bag != null)
        {
            List<LootItem> snapshot = new List<LootItem>(bag.Carried);
            foreach (LootItem loot in snapshot)
            {
                TruckLoadCell target = FindEmptyCell();
                if (target == null) break; // hết ô: phần còn lại ở nguyên trong túi

                target.PlaceLoot(target, loot.gameObject);
                bag.Remove(loot);
            }
        }
    }

    private void PlaceLoot(TruckLoadCell target, GameObject item)
    {
        if (target == null || item == null) return;

        LootItem loot = item.GetComponent<LootItem>();
        if (loot != null && !string.IsNullOrEmpty(loot.ItemId))
            loadedIds.Add(loot.ItemId);

        target.filled = true;
        Transform anchor = target.placeAnchor != null ? target.placeAnchor : target.transform;
        item.SetActive(true);
        item.transform.SetParent(anchor, false);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        Collider itemCollider = item.GetComponent<Collider>();
        if (itemCollider != null) itemCollider.enabled = false;

        target.RefreshVisuals();

        QuestManager manager = QuestManager.Instance;
        if (manager != null && !string.IsNullOrEmpty(loadQuestId) && !string.IsNullOrEmpty(loadObjectiveId))
            manager.ReportProgress(loadQuestId, loadObjectiveId);

        target.onFilled?.Invoke();
        Debug.Log($"[TruckLoadCell] {item.name} đã nằm trong {target.name}.", target);
    }

    // ------------------------------------------------------------ điều kiện / prompt

    private HeldState EvaluateHeld()
    {
        if (playerGrab == null) playerGrab = FindFirstObjectByType<PlayerGrab>();

        GameObject held = playerGrab != null ? playerGrab.HeldItem : null;
        if (held == null) return HeldState.None;

        if (IsAccepted(held)) return HeldState.Valid;
        return HeldState.Invalid;
    }

    // Rượu/muối (không có LootItem.ItemId) hoặc món đã chất rồi → không đáng giá.
    private bool IsAccepted(GameObject item)
    {
        LootItem loot = item != null ? item.GetComponent<LootItem>() : null;
        string id = loot != null ? loot.ItemId : null;
        if (string.IsNullOrEmpty(id)) return false;
        if (loadedIds.Contains(id)) return false;

        if (validItemIds != null && validItemIds.Length > 0 && System.Array.IndexOf(validItemIds, id) < 0)
            return false;

        return true;
    }

    private bool HasAnythingToDeposit()
    {
        // Cầm thứ gì đó (hợp lệ oặc không) hoặc túi còn đồ → collider phải bật để hiện prompt.
        bool handBusy = playerGrab != null && playerGrab.HeldItem != null;
        return handBusy || HasBagGoods();
    }

    private bool HasBagGoods()
    {
        PlayerLootBag bag = PlayerLootBag.Instance;
        if (bag == null || bag.Carried.Count == 0) return false;

        foreach (LootItem loot in bag.Carried)
        {
            if (loot != null && IsAccepted(loot.gameObject)) return true;
        }
        return false;
    }

    // ------------------------------------------------------------ nhóm ô / hiển thị

    private TruckLoadCell FindEmptyCell()
    {
        foreach (TruckLoadCell cell in RegisteredCells)
        {
            if (cell == null || cell.filled || !cell.gameObject.activeInHierarchy) continue;
            return cell;
        }
        return null;
    }

    private static bool HasEmptyCell()
    {
        foreach (TruckLoadCell cell in RegisteredCells)
        {
            if (cell != null && !cell.filled && cell.gameObject.activeInHierarchy) return true;
        }
        return false;
    }

    private void Awake()
    {
        cubeRenderer = GetComponentInChildren<Renderer>();
        detectCollider = GetComponent<Collider>();
        if (detectCollider != null && !detectCollider.isTrigger)
            detectCollider.isTrigger = true; // ô chỉ để phát hiện player đứng gần, không cản di chuyển
    }

    private void OnEnable() => RegisteredCells.Add(this);

    private void OnDisable() => RegisteredCells.Remove(this);

    private void OnDestroy()
    {
        if (ownsFallbackQuad && zoneRenderer != null)
            Destroy(zoneRenderer.gameObject);
    }

    private void Start()
    {
        if (cubeRenderer != null) cubeRenderer.enabled = false;
        if (detectCollider != null) detectCollider.enabled = false;
        if (zoneRenderer != null) zoneRenderer.enabled = false;

        // Vùng tùy chỉnh có sẵn trong scene (đặt OFF cũng được) thì tự nhận: ưu tiên component TruckLoadZone,
        // rồi mới tới object đúng tên. Log để biết vì sao vùng không hiện khi chơi.
        if (loadZone == null)
        {
            TruckLoadZone[] zones = FindObjectsByType<TruckLoadZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (zones.Length > 0) loadZone = zones[0].gameObject;

            if (loadZone == null)
            {
                foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (candidate.name != CustomZoneName && candidate.name != CustomZoneName2) continue;
                    loadZone = candidate.gameObject;
                    break;
                }
                if (loadZone != null && loadZone.GetComponent<TruckLoadZone>() == null)
                    loadZone.AddComponent<TruckLoadZone>(); // chưa có component thì tự thêm để có hiệu ứng sáng + vùng đứng
            }
        }

        Debug.Log($"[TruckLoadCell] loadZone = {(loadZone != null ? $"'{loadZone.name}' ({(loadZone.activeSelf ? "ON" : "OFF")})" : "KHÔNG tìm thấy → quad dự phòng")}", this);
    }

    private void Update()
    {
        // CHỐT trạng thái (latch): bật ngay khi sẵn sàng; chỉ tắt sau khi điều kiện sai LIÊN TỤC zoneHoldSeconds.
        // Tránh chớp tắt khi trạng thái cửa/bảng trạng thái biến chập chờn quanh ngưỡng.
        if (ZoneReady)
        {
            zoneOffTimer = 0f;
            zoneShown = true;
        }
        else
        {
            zoneOffTimer += Time.deltaTime;
            if (zoneOffTimer >= zoneHoldSeconds) zoneShown = false;
        }

        if (loadZone != null && loadZone.scene.IsValid() && loadZone.activeSelf != zoneShown)
            loadZone.SetActive(zoneShown);

        // Chỉ ô ĐẦU TIÊN đăng ký mới sinh quad dự phòng (1 cái duy nhất), và chỉ khi scene không có vùng tùy chỉnh nào.
        bool useFallbackZone = TruckLoadZone.ActiveCount == 0 && loadZone == null
            && RegisteredCells.Count > 0 && ReferenceEquals(RegisteredCells[0], this);
        if (useFallbackZone) BuildZone();

        if (cubeRenderer != null) cubeRenderer.enabled = DoorFullyOpen && !filled;
        // Vùng sàn phát sáng (nhấp nháy nhẹ) theo trạng thái đã chốt.
        if (zoneRenderer != null)
        {
            zoneRenderer.enabled = useFallbackZone && zoneShown;
            if (zoneRenderer.enabled)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 3f);
                Color c = zoneColor;
                c.r *= pulse; c.g *= pulse; c.b *= pulse;
                zoneMaterial.color = c;
            }
        }
        // Collider bật khi còn gì để chất (tay/túi), kể cả cửa đóng để prompt cảnh báo hiện nổi.
        if (detectCollider != null) detectCollider.enabled = !filled || HasBagGoods();
    }

    private void RefreshVisuals()
    {
        if (cubeRenderer != null) cubeRenderer.enabled = DoorFullyOpen && !filled;
        if (zoneRenderer != null) zoneRenderer.enabled = zoneShown && TruckLoadZone.ActiveCount == 0;
        if (detectCollider != null) detectCollider.enabled = !filled || HasBagGoods();
    }

    // ------------------------------------------------------------ vùng sàn phát sáng (fallback)

    // Tự sinh quad phát sáng trên sàn: dùng material không cần ánh sáng + raycast tìm mặt
    // sàn thật (bỏ qua trigger, cả sàn thùng xe nếu ô nằm trong xe) để quad không bị chôn dưới sàn.
    private void BuildZone()
    {
        if (zoneRenderer != null || detectCollider == null) return;
        ownsFallbackQuad = true;

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "VungChatDoTrenXe_DuPhong"; // tên khác object vùng tùy chỉnh trong scene để Start không nhầm
        Destroy(quad.GetComponent<Collider>());

        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        zoneMaterial = new Material(Shader.Find("Sprites/Default")) { color = zoneColor };
        renderer.sharedMaterial = zoneMaterial;
        renderer.enabled = false;
        zoneRenderer = renderer;

        Bounds bounds = detectCollider.bounds;
        // Ray từ đỉnh hitbox xong xuống 20m, bỏ qua trigger: trúng mặt phẳng nào (sàn thùng/đất)quad nằm ngay trên mặt đó.
        float y = bounds.min.y + 0.05f;
        if (Physics.Raycast(
                new Vector3(bounds.center.x, bounds.max.y + 0.2f, bounds.center.z),
                Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
            y = hit.point.y + 0.04f;

        quad.transform.SetPositionAndRotation(
            new Vector3(bounds.center.x, y, bounds.center.z),
            Quaternion.Euler(-90f, 0f, 0f));
        quad.transform.localScale = new Vector3(
            Mathf.Max(2f, bounds.size.x + 1.5f),
            Mathf.Max(2f, bounds.size.z + 1.5f),
            1f);
    }

        // Nới biên 0.3m cho dễ đứng tròn trong vùng.
    private bool ProximityContainsPlayer()
    {
        if (detectCollider == null || !detectCollider.enabled) return false;
        PlayerGrab grab = playerGrab != null ? playerGrab : FindFirstObjectByType<PlayerGrab>();
        if (grab == null) return false;

        Bounds bounds = detectCollider.bounds;
        bounds.Expand(0.3f);
        return bounds.Contains(grab.transform.position);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
        Gizmos.DrawCube(transform.position, Vector3.one * 0.4f);
    }
}
