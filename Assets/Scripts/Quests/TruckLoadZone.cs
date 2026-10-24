using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Vùng chất đồ DUY NHẤT trên xe: đứng trong vùng là hiện prompt, không cần nhìn vào.
// GIỮ E đủ holdDuration: chất món đang cầm trên tay vào các chỗ trống của vùng.
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
    [SerializeField] private string promptDoorClosed = "Hãy mở cửa hậu trước";
    [SerializeField] private string promptInvalid = "Món đồ này không đáng giá.";

    // Text cố định trong code: muối/rượu là đồ hỗ trợ, không phải hàng bán.
    // Bản giá trị cũ "Muối và rượu..." serialize trong scene từ bản cũ sẽ tự
    // bị bỏ qua khi deserialize - không cần đụng file scene.
    private const string promptNotSellable = "Đồ không bán được.";

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
            // Cửa chưa mở = flow nhiệm vụ chưa tới màn xe: khuyến khích đi mở
            // cửa, không được báo điều gì khác.
            if (!DoorFullyOpen) return promptDoorClosed;

            // Đang chọn muối/rượu (đồ hỗ trợ, không phải hàng bán): báo rõ
            // để người chơi đỡ tưởng là đồ rác. (Không thể vừa cầm loot vừa
            // chọn muối/rượu - hai trạng thái kia không xung đột.)
            if (CarryingConsumable()) return promptNotSellable;

            HeldState held = EvaluateHeld();
            if (held == HeldState.Invalid) return promptInvalid;
            return promptWhenReady;
        }
    }

    // Chỉ là hành động "GIỮ" khi thật sự chất được đồ: cửa mở + cầm loot hợp
    // lệ + còn chỗ. Còn lại chỉ là DÒNG THÔNG BÁO (không bán được / cửa đóng)
    // -> tiền tố của Interactor buộc phải là "[E] ", không nhầm "Giữ E" khi
    // người chơi chưa cầm gì hay flow chưa mở.
    public bool HoldToInteract =>
        DoorFullyOpen && EvaluateHeld() == HeldState.Valid && HasEmptySlot;
    public float HoldDuration => holdDuration;

    // Cầm món hợp lệ (hoặc món sai để hiện lý do) hoặc đang chọn muối/rượu
    // mới giành prompt.
    public bool IsAvailable => PlayerInside() && (EvaluateHeld() != HeldState.None || CarryingConsumable());

    // Chỉ "giành" prompt khi thật sự chất được NHƯNG phải khi cửa đã mở (flow
    // đã tới màn xe). Cửa đóng thì prompt riêng của cửa hậu tự lo, vùng xe
    // không giành — muối/rượu không hiện prompt nào khi flow chưa mở.
    public bool ReadyForProximityPrompt => IsAvailable && DoorFullyOpen;

    // Người chơi đang "cầm" loại đồ hỗ trợ (muối/rượu trên hotbar)? Nhường
    // prompt khi NGẮM một món muối/rượu trên sàn: ý định là nhặt, prompt nhặt
    // của món đó phải thắng.
    private bool CarryingConsumable()
    {
        if (playerGrab == null) playerGrab = FindFirstObjectByType<PlayerGrab>();
        return playerGrab != null
            && playerGrab.IsCarryingConsumable
            && !playerGrab.IsAimingAtConsumable();
    }

    public void OnInteract(Interactor interactor)
    {
        if (!DoorFullyOpen) return;

        // Chỉ món trên tay.
        if (EvaluateHeld() == HeldState.Valid && HasEmptySlot)
            PlaceLoot(playerGrab.TakeHeld());
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
