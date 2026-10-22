using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Ô chất đồ lên xe tải. Cửa hậu mở hết thì cube xanh hiện (Ready).
// Cầm món loot hợp lệ hoặc túi có đồ, đứng gần ô, GIỮ E đủ holdDuration:
// một lần giữ = chất hết món trên tay VÀ toàn bộ túi (PlayerLootBag), chia vào các ô trống.
// Rượu/muối (PlayerInventory/không LootItem) không thuộc bị từ chối "Món đồ này không đáng giá."
public class TruckLoadCell : MonoBehaviour, IInteractable, IInteractableAvailability
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => loadedIds.Clear();

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
        foreach (TruckLoadCell cell in FindObjectsByType<TruckLoadCell>(FindObjectsSortMode.None))
        {
            if (cell == null || cell.filled || !cell.gameObject.activeInHierarchy) continue;
            return cell;
        }
        return null;
    }

    private void Awake()
    {
        cubeRenderer = GetComponentInChildren<Renderer>();
        detectCollider = GetComponent<Collider>();
        if (detectCollider != null && !detectCollider.isTrigger)
            detectCollider.isTrigger = true; // ô chỉ để phát hiện player đứng gần, không cản di chuyển
    }

    private void Start()
    {
        if (cubeRenderer != null) cubeRenderer.enabled = false;
        if (detectCollider != null) detectCollider.enabled = false;
    }

    private void Update()
    {
        if (cubeRenderer != null) cubeRenderer.enabled = DoorFullyOpen && !filled;
        // Collider bật khi còn gì để chất (tay/túi), kể cả cửa đóng để prompt cảnh báo hiện nổi.
        if (detectCollider != null) detectCollider.enabled = !filled || HasBagGoods();
    }

    private void RefreshVisuals()
    {
        if (cubeRenderer != null) cubeRenderer.enabled = DoorFullyOpen && !filled;
        if (detectCollider != null) detectCollider.enabled = !filled || HasBagGoods();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
        Gizmos.DrawCube(transform.position, Vector3.one * 0.4f);
    }
}
