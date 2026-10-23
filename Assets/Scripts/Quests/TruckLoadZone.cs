using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Vùng chất đồ DUY NHẤT trên xe: đứng trong vùng là hiện prompt, không cần nhìn vào.
// GIỮ E đủ holdDuration: chất món đang cầm + toàn bộ túi (PlayerLootBag) vào các chỗ trống của vùng.
// Không cần ô TruckLoadCell nào: vùng tự chia slotCount chỗ đặt và tự báo nhiệm vụ.
[RequireComponent(typeof(Collider))]
public class TruckLoadZone : MonoBehaviour, IInteractable, IInteractableAvailability, IProximityInteractable
{
    private enum HeldState { None, Invalid, Valid }

    [Header("Tham chiếu")]
    [Tooltip("Cửa hậu cánh chính (DoorInteractable).")]
    [SerializeField] private DoorInteractable rearDoor;

    [Tooltip("Player để đọc món đang cầm trên tay.")]
    [SerializeField] private PlayerGrab playerGrab;

    [Header("Cài đặt")]
    [Tooltip("Số giây phải giữ E để chất. Thả E giữa chừng = hủy, đồ không mất gì.")]
    [SerializeField, Min(0.1f)] private float holdDuration = 1.2f;

    [Tooltip("Số món tối đa vùng nhận.")]
    [SerializeField, Min(1)] private int slotCount = 3;

    [Tooltip("Khoảng cách giữa các chỗ đặt món (theo trục X của vùng).")]
    [SerializeField] private float slotSpacing = 0.9f;

    [Tooltip("Transform trống trong thùng xe; món được xếp quanh đây. Để trống = xếp quanh vùng.")]
    [SerializeField] private Transform placeAnchor;

    [Tooltip("Độ cao đặt món so với điểm đặt.")]
    [SerializeField] private float placeHeight = 0.15f;

    [Tooltip("Danh sách itemId vùng nhận. Để trống = mọi món có LootItem/itemId đều dùng được.")]
    [SerializeField] private string[] validItemIds = new string[0];

    [Header("Báo nhiệm vụ")]
    [Tooltip("Mỗi món chất xong cộng tiến độ cho quest/mục tiêu này.")]
    [SerializeField] private string loadQuestId = "loot_truck";
    [SerializeField] private string loadObjectiveId = "chat_do";

    [Header("Sự kiện")]
    [Tooltip("Bung khi một món đồ vừa nằm hẳn trong vùng.")]
    [SerializeField] private UnityEvent onFilled;

    [Header("Prompt")]
    [SerializeField] private string promptWhenReady = "Giữ E để chất đồ lên xe";
    [SerializeField] private string promptBagOnly = "Giữ E để cất đồ trong túi lên xe";
    [SerializeField] private string promptDoorClosed = "Hãy mở cửa hậu trước";
    [SerializeField] private string promptInvalid = "Món đồ này không đáng giá.";

    [Header("Vùng sáng")]
    [Tooltip("Màu phát sáng của vùng khi cửa hậu mở và còn chỗ.")]
    [SerializeField] private Color zoneColor = new Color(0f, 1f, 0.3f, 0.55f);

    // Mỗi itemId chỉ được tính đã chất một lần trong cả xe.
    private static readonly HashSet<string> loadedIds = new HashSet<string>();

    // Latch chống chớp: chỉ tắt sau khi điều kiện sai liên tục HoldOffSeconds.
    private const float HoldOffSeconds = 0.4f;
    private float offTimer;
    private bool shown;

    private Collider zoneCollider;
    private Renderer zoneRenderer;
    private Material zoneMaterial;
    private int filled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => loadedIds.Clear();

    private bool DoorFullyOpen => rearDoor != null && rearDoor.IsFullyOpen;
    private bool HasEmptySlot => filled < slotCount;

    // ------------------------------------------------------------ IInteractable

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

    // Đứng trong vùng + có thứ để chất (hoặc cầm đồ sai để báo lý do).
    public bool IsAvailable => PlayerInside() && CanDeposit;
    // Chỉ "giành" prompt khi thật sự chất được: cửa chưa mở thì nhường cho cửa hậu.
    public bool ReadyForProximityPrompt => IsAvailable && DoorFullyOpen;

    private bool CanDeposit
    {
        get
        {
            // Cửa chưa mở hết: chỉ để hiện prompt cảnh báo "Hãy mở cửa hậu trước" / "không đáng giá".
            if (!DoorFullyOpen) return HasAnythingToDeposit();
            if (EvaluateHeld() != HeldState.None) return true; // hợp lệ: chất; không hợp lệ: hiện "không đáng giá"
            return HasBagGoods();
        }
    }

    public void OnInteract(Interactor interactor)
    {
        if (!DoorFullyOpen) return;

        // 1) Món trên tay.
        if (EvaluateHeld() == HeldState.Valid && HasEmptySlot)
            PlaceLoot(playerGrab.TakeHeld());

        // 2) Toàn bộ túi: mỗi món vào một chỗ trống.
        PlayerLootBag bag = PlayerLootBag.Instance;
        if (bag == null) return;

        List<LootItem> snapshot = new List<LootItem>(bag.Carried);
        foreach (LootItem loot in snapshot)
        {
            if (!HasEmptySlot) break; // hết chỗ: phần còn lại ở nguyên trong túi
            if (loot == null) continue;

            GameObject item = loot.gameObject;
            PlaceLoot(item);
            bag.Remove(loot);
        }
    }

    // ------------------------------------------------------------ đặt đồ

    private void PlaceLoot(GameObject item)
    {
        if (item == null || !HasEmptySlot) return;

        LootItem loot = item.GetComponent<LootItem>();
        if (loot != null && !string.IsNullOrEmpty(loot.ItemId))
            loadedIds.Add(loot.ItemId);

        int index = filled;
        filled++;

        item.SetActive(true);
        item.transform.SetParent(PlacePoint, true);
        item.transform.position = SlotPosition(index);
        item.transform.rotation = PlacePoint.rotation;

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        Collider itemCollider = item.GetComponent<Collider>();
        if (itemCollider != null) itemCollider.enabled = false;

        QuestManager manager = QuestManager.Instance;
        if (manager != null && !string.IsNullOrEmpty(loadQuestId) && !string.IsNullOrEmpty(loadObjectiveId))
            manager.ReportProgress(loadQuestId, loadObjectiveId, Mathf.Max(1, loot != null ? loot.valueInMillions : 1));

        onFilled?.Invoke();
        Debug.Log($"[TruckLoadZone] {item.name} đã nằm trong {name} ({filled}/{slotCount}).", this);
    }

    private Transform PlacePoint => placeAnchor != null ? placeAnchor : transform;

    // Chia chỗ thành một hàng ngang theo trục X của điểm đặt.
    private Vector3 SlotPosition(int index)
    {
        float x = (index - (slotCount - 1) * 0.5f) * slotSpacing;
        return PlacePoint.position + PlacePoint.rotation * new Vector3(x, placeHeight, 0f);
    }

    // ------------------------------------------------------------ điều kiện

    private HeldState EvaluateHeld()
    {
        if (playerGrab == null) playerGrab = FindFirstObjectByType<PlayerGrab>();

        GameObject held = playerGrab != null ? playerGrab.HeldItem : null;
        if (held == null) return HeldState.None;

        return IsAccepted(held) ? HeldState.Valid : HeldState.Invalid;
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

    // ------------------------------------------------------------ vùng / hiển thị

    // Player đứng trong vùng: quyết định bằng trigger — CharacterController/đồ trên người player
    // chạm vào box của vùng là coi như đứng trong, không lệ thuộc pivot hay biên AABB.
    public bool PlayerInside() => playerIn || StandingInXZ();

    private bool StandingInXZ()
    {
        if (zoneCollider == null) return false;
        if (playerGrab == null) playerGrab = FindFirstObjectByType<PlayerGrab>();
        if (playerGrab == null) return false;

        Bounds bounds = zoneCollider.bounds;
        Vector3 p = playerGrab.transform.position;
        return p.x >= bounds.min.x - 0.3f && p.x <= bounds.max.x + 0.3f
            && p.z >= bounds.min.z - 0.3f && p.z <= bounds.max.z + 0.3f;
    }

    private bool playerIn;

    private void OnTriggerEnter(Collider other) { if (IsPlayerCollider(other)) playerIn = true; }
    private void OnTriggerStay(Collider other) { if (IsPlayerCollider(other)) playerIn = true; }
    private void OnTriggerExit(Collider other) { if (IsPlayerCollider(other)) playerIn = false; }
    private void OnDisable() => playerIn = false;

    private bool IsPlayerCollider(Collider other)
    {
        if (playerGrab == null) playerGrab = FindFirstObjectByType<PlayerGrab>();
        return playerGrab != null && other.transform.root == playerGrab.transform.root;
    }

    private void Awake()
    {
        if (playerGrab == null) playerGrab = FindFirstObjectByType<PlayerGrab>();

        zoneCollider = GetComponent<Collider>();
        if (zoneCollider != null && !zoneCollider.isTrigger)
            zoneCollider.isTrigger = true; // vùng chỉ để phát hiện player đứng trong, không cản di chuyển

        zoneRenderer = GetComponent<Renderer>();
        if (zoneMaterial == null)
        {
            zoneMaterial = new Material(Shader.Find("Sprites/Default")) { color = zoneColor };
            if (zoneRenderer != null) zoneRenderer.sharedMaterial = zoneMaterial;
        }
        if (zoneRenderer != null) zoneRenderer.enabled = false;
    }

    private void Update()
    {
        if (zoneRenderer == null || zoneMaterial == null) return;

        if (DoorFullyOpen && HasEmptySlot)
        {
            offTimer = 0f;
            shown = true;
        }
        else
        {
            offTimer += Time.deltaTime;
            if (offTimer >= HoldOffSeconds) shown = false;
        }

        if (zoneRenderer.enabled != shown) zoneRenderer.enabled = shown;
        if (!shown) return;

        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 3f);
        Color c = zoneColor;
        c.r *= pulse; c.g *= pulse; c.b *= pulse;
        zoneMaterial.color = c;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.3f, 0.6f);
        if (zoneCollider != null) Gizmos.DrawCube(zoneCollider.bounds.center, zoneCollider.bounds.size);
    }
}
