#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Events;

// Vùng chất đồ DUY NHẤT trên xe: đứng trong vùng là hiện prompt, không cần nhìn vào.
// GIỮ E đủ holdDuration: thả món trên tay vào KHOANG ĐỔ (ItemSlot).
//
// Khoang đổ = MỘT GameObject con "ItemSlot" có BoxCollider (màu vàng khi chọn):
// món chất vào rơi từ điểm gần tâm range, bật vật lý thật (Rigidbody + collider)
// nên đồ chồng/đè/độn lên nhau tự nhiên như đổ hàng vào xe, không nằm tượng
// tại từng slot cứng. Sửa chỗ đổ = kéo/rong ItemSlot trong tab Scene.
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

    [Header("Khoang đổ: ItemSlot")]
    [Tooltip("MỘT GameObject con 'ItemSlot' có BoxCollider làm khoang đổ. Kéo vị trí/scale " +
             "collider trong tab Scene để chỉnh chỗ đồ rơi vào. Món được thả từ một điểm " +
             "ngẫu nhiên trong range rồi để vật lý tự xếp chồng. Thiếu tự tạo khi chọn " +
             "object (Scene view) hoặc lúc Play.")]
    [SerializeField] private float slotMarkerSize = 0.6f;

    [Tooltip("Độ cao món được thả so với tâm range (để nó rơi xuống và chồng lên nhau).")]
    [SerializeField, Min(0f)] private float dropHeight = 0.3f;

    [Tooltip("Bật = món có vật lý thật (tự chồng/độn vậy). Tắt = đứng im như cũ.")]
    [SerializeField] private bool stackWithPhysics = true;

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

    [Tooltip("Cửa hậu đã mở, tay cầm loot hợp lệ nhưng ô chất đồ đã đầy.")]
    [SerializeField] private string promptFull = "Ô chất đồ đã đầy.";

    // Text cố định trong code: muối/rượu là đồ hỗ trợ, không phải hàng bán.
    private const string promptNotSellable = "Đồ không bán được.";

    [Header("Vùng sáng")]
    [Tooltip("Màu phát sáng của vùng khi cửa hậu mở và còn chỗ.")]
    [SerializeField] private Color zoneColor = new Color(0f, 1f, 0.3f, 0.55f);

    // itemId đang "nằm trên xe": chất vào thì thêm, món bị nhặt ra lại thì nhả
    // ô — số ô/full-slot luôn phản ánh sản phẩm hiện có của khoang đổ.
    private static readonly System.Collections.Generic.HashSet<string> loadedIds = new System.Collections.Generic.HashSet<string>();

    // Latch chống chớp: chỉ tắt sau khi điều kiện sai liên tục HoldOffSeconds.
    private const float HoldOffSeconds = 0.4f;
    private float offTimer;
    private bool shown;

    private Collider zoneCollider;
    private Renderer zoneRenderer;
    private Material zoneMaterial;
    private int filled;

    // Chỗ đổ món (MỘT cái duy nhất).
    private Transform dropMarker;

    // Các món ĐÃ TỪNG được chất vào ô này (quản lý theo đối tượng thật, không
    // phải id): người chơi nhặt món đã chất ra khỏi xe thì vẫn được chất lại,
    // nhưng không cộng lấp ô/tiến độ nhiệm vụ lần nữa (chống ăn gian).
    private readonly System.Collections.Generic.HashSet<LootItem> placedLoots =
        new System.Collections.Generic.HashSet<LootItem>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => loadedIds.Clear();

    private bool DoorFullyOpen => rearDoor != null && rearDoor.IsFullyOpen;
    private bool HasEmptySlot => filled < slotCount;

    // Chất được khi còn chỗ trống, HOẶC món này đã từng được chất rồi (bị
    // nhặt ra khỏi xe và cầm lại trên tay) — món đó đã từng chiếm ô nên cho
    // trả vào xuôi, kể cả khi mọi ô đều đã được tính.
    private bool FitsHeld(LootItem loot) =>
        HasEmptySlot || (loot != null && placedLoots.Contains(loot));

    // ------------------------------------------------------------ IInteractable

    public string PromptText
    {
        get
        {
            // Cửa chưa mở = flow nhiệm vụ chưa tới màn xe: khuyến khích đi mở
            // cửa, không được báo điều gì khác.
            if (!DoorFullyOpen) return promptDoorClosed;

            // Đang chọn muối/rượu (đồ hỗ trợ, không phải hàng bán): báo rõ
            // để người chơi đỡ tưởng là đồ rác.
            if (CarryingConsumable()) return promptNotSellable;

            HeldState held = EvaluateHeld();
            if (held == HeldState.Invalid) return promptInvalid;
            if (!FitsHeld(HeldLootItem())) return promptFull;
            return promptWhenReady;
        }
    }

    // Chỉ là hành động "GIỮ" khi thật sự chất được đồ: cửa mở + cầm loot hợp
    // lệ + còn chỗ. Còn lại chỉ là DÒNG THÔNG BÁO (không bán được / cửa đóng)
    // -> tiền tố của Interactor buộc phải là "[E] ", không nhầm "Giữ E" khi
    // người chơi chưa cầm gì hay flow chưa mở.
    public bool HoldToInteract =>
        DoorFullyOpen && EvaluateHeld() == HeldState.Valid && FitsHeld(HeldLootItem());
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

        // Chỉ món trên tay; món đã từng chất (nhặt ra lại) được chất lại kể cả khi "đầy ô".
        if (EvaluateHeld() == HeldState.Valid && FitsHeld(HeldLootItem()))
            PlaceLoot(playerGrab.TakeHeld());
    }

    // ------------------------------------------------------------ đặt đồ

    private void PlaceLoot(GameObject item)
    {
        if (item == null) return;

        LootItem loot = item.GetComponent<LootItem>();
        bool rePlaced = loot != null && placedLoots.Contains(loot);
        if (!rePlaced && !HasEmptySlot) return;

        if (!rePlaced)
        {
            if (loot != null && !string.IsNullOrEmpty(loot.ItemId))
                loadedIds.Add(loot.ItemId);

            placedLoots.Add(loot); // HashSet nhận luôn cả loot == null, vô hại
            filled++;
        }

        item.SetActive(true);

        // Không parent vào PlacePoint (model xe scale cực lớn ~158x): vật lý dưới
        // parent scale lớn sẽ lệch/bão đổi tọa độ. Giữ món ở world space.
        item.transform.SetParent(null, true);

        item.transform.position = DropPosition(item);
        item.transform.rotation = PlacePoint.rotation;

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = !stackWithPhysics; // vật lý thật để đồ tự chồng lên nhau
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // QUAN TRỌNG: PlayerGrab cầm đồ bằng cách TẮT collider (isKinematic +
        // collider.enabled = false). Nếu không bật lại, món có RB vật lý nhưng
        // KHÔNG collider nào → rơi xuyên hết mọi bề mặt, mất khỏi thế giới.
        // Bật lại cả collider trên món lẫn collider con để đồ va nhau/đè nhau.
        Collider selfCollider = item.GetComponent<Collider>();
        if (selfCollider != null) selfCollider.enabled = true;

        Collider[] childColliders = item.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < childColliders.Length; i++)
        {
            if (childColliders[i] != null) childColliders[i].enabled = true;
        }

        // stackWithPhysics = false (đứng im như cũ): collider vẫn tắt để không
        // cản raycast, trừ khi bật vật lý stacking.
        if (!stackWithPhysics)
        {
            if (selfCollider != null) selfCollider.enabled = false;
            for (int i = 0; i < childColliders.Length; i++)
            {
                if (childColliders[i] != null) childColliders[i].enabled = false;
            }
        }

        // Chỉ cộng tiến độ nhiệm vụ MỘT LẦN cho mỗi món: chất lại món bị nhặt
        // ra không được cộng thêm, nếu không người chơi ăn gian được tiến độ.
        if (!rePlaced)
        {
            QuestManager manager = QuestManager.Instance;
            if (manager != null && !string.IsNullOrEmpty(loadQuestId) && !string.IsNullOrEmpty(loadObjectiveId))
                manager.ReportProgress(loadQuestId, loadObjectiveId, Mathf.Max(1, loot != null ? loot.valueInMillions : 1));
        }

        onFilled?.Invoke();
        Debug.Log($"[TruckLoadZone] {item.name} được thả vào {name} ({filled}/{slotCount}).", this);
    }

    private Transform PlacePoint => transform;

    // MỘT ItemSlot duy nhất: món rơi từ điểm random trong range (tránh chồng
    // tuyệt đối cùng 1 điểm), cộng dropHeight để nó rơi xuống rồi vật lý tự độn.
    private Vector3 DropPosition(GameObject item)
    {
        Vector3 basePoint = transform.position;

        if (dropMarker != null)
        {
            BoxCollider box = dropMarker.GetComponent<BoxCollider>();
            if (box != null)
            {
                // Random theo ĐƠN VỊ LOCAL của marker (box.center/size vốn là
                // tọa độ local). TransformPoint sẽ nhân đúng MỘT lần với scale
                // thật của marker — kể cả khi cha scale không đều như
                // VungChatDoTrenXe (2.9/0.01/4.95). Nếu nhân nửa size world
                // (box.size * lossyScale) rồi lại đưa qua TransformPoint như
                // cũ thì offset bị nhân scale HAI lần, điểm rơi tràn ra ngoài
                // khoang/x → đồ "nằm ở ngoài xe".
                Vector3 halfLocal = box.size * 0.5f;
                Vector3 localRandom = new Vector3(
                    Random.Range(-halfLocal.x, halfLocal.x),
                    Random.Range(-halfLocal.y, halfLocal.y),
                    Random.Range(-halfLocal.z, halfLocal.z)) * 0.6f; // 60% để không chạm thành
                basePoint = dropMarker.TransformPoint(box.center + localRandom);
            }
        }

        return basePoint + Vector3.up * dropHeight;
    }

    // ------------------------------------------------------------ khoang đổ (ItemSlot)

    private void Awake()
    {
        if (playerGrab == null) playerGrab = FindFirstObjectByType<PlayerGrab>();

        EnsureDropMarker(); // dựng/kiểm tra ItemSlot trước khi chất món trong Play

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

    // Tạo đúng MỘT ItemSlot còn thiếu. Collider là trigger tắt Enable lúc Play:
    // chỉ để CHỈ VỊ TRÍ + soi size trong Scene, không tham gia vật lý.
    private void EnsureDropMarker()
    {
        dropMarker = transform.Find("ItemSlot");

        if (dropMarker == null || dropMarker.GetComponent<Collider>() == null)
        {
            GameObject go = new GameObject("ItemSlot");
            if (Application.isPlaying)
            {
                go.transform.SetParent(transform, false);
            }
            else
            {
                go.transform.SetParent(transform, false);
                Undo.RegisterCreatedObjectUndo(go, "Tạo ItemSlot"); // Ctrl+Z được
            }

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(slotMarkerSize, slotMarkerSize, slotMarkerSize);

            // Mặc định nằm ngay trên PlacePoint, người làm kéo đi chỗ khác.
            go.transform.position = transform.position + Vector3.up * placeHeightFallback();
            go.transform.rotation = transform.rotation;

            dropMarker = go.transform;
        }

        // Lúc Play: tắt collider marker — chỉ là chỉ thị vị trí, không đàm phán
        // raycast/vật lý với player hay quái.
        if (Application.isPlaying)
        {
            Collider col = dropMarker.GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }
    }

    // Độ cao khởi tạo marker; dùng khi backdrop không có placeAnchor nào cả.
    private float placeHeightFallback() => 0.15f;

    // ------------------------------------------------------------ điều kiện

    // Món loot đang cầm trên tay (null khi tay trống). Dùng GetComponentInParent
    // cho an toàn như PlayerGrab vì mon có thể là collider con của chỗ gắn LootItem.
    private LootItem HeldLootItem()
    {
        if (playerGrab == null) playerGrab = FindFirstObjectByType<PlayerGrab>();
        GameObject held = playerGrab != null ? playerGrab.HeldItem : null;
        return held != null ? held.GetComponentInParent<LootItem>() : null;
    }

    private HeldState EvaluateHeld()
    {
        LootItem loot = HeldLootItem();
        if (loot == null) return HeldState.None;

        // Tay trống = None (không gợi ý), món sai loại = Invalid (để hiện lý do).
        return IsAccepted(loot) ? HeldState.Valid : HeldState.Invalid;
    }

    // Rượu/muối (không có LootItem.ItemId) hoặc id đã được chất lần nào rồi →
    // không đáng giá. NGOẠI LỆ: chính chiếc món đã được chất từ ô này — người
    // chơi nhặt nó ra khỏi xe thì vẫn được chất lại (xem PlaceLoot), không
    // bị chặn vì id của nó đã nằm trong loadedIds từ lần chất trước.
    private bool IsAccepted(LootItem loot)
    {
        if (loot == null) return false;
        string id = loot.ItemId;
        if (string.IsNullOrEmpty(id)) return false;
        if (placedLoots.Contains(loot)) return true;
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

    private void Update()
    {
        ReclaimTakenOutLoot(); // món đang bị cầm ra khỏi xe = đã lấy ra, hoàn ô/tiến độ

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

    // ------------------------------------------------------------ lấy đồ ra khỏi xe

    // Người chơi NHẶT một món đã chất lên tay (hoặc món đó bị hủy): coi như
    // món đã rời xe — giải phóng ô, nhả itemId và TRỪ LẠI đúng số tiến độ đã
    // cộng khi chất. Nhờ vậy UI nhiệm vụ/ô chất đồ luôn phản ánh CHÍNH XÁC
    // những gì đang nằm trên xe: chất lại sau đó được tính như lần chất mới.
    private void ReclaimTakenOutLoot()
    {
        if (placedLoots.Count == 0) return;
        if (playerGrab == null) playerGrab = FindFirstObjectByType<PlayerGrab>();

        LootItem held = HeldLootItem();

        // Chọn danh sách cần thu hồi TRƯỚC, sửa tập sau khi duyệt xong
        // (HashSet không cho thêm/bớt ngay trong vòng foreach).
        System.Collections.Generic.List<LootItem> reclaimed = null;
        foreach (LootItem loot in placedLoots)
        {
            if (loot == null || (held != null && loot == held) || !IsStillInTruck(loot))
                (reclaimed ?? (reclaimed = new System.Collections.Generic.List<LootItem>())).Add(loot);
        }
        if (reclaimed == null) return;

        foreach (LootItem loot in reclaimed)
        {
            placedLoots.Remove(loot);

            string id = loot != null ? loot.ItemId : null;
            if (!string.IsNullOrEmpty(id)) loadedIds.Remove(id); // ô id trống lại

            filled = Mathf.Max(0, filled - 1);

            // Trừ đúng số đã cộng khi chất (valueInMillions tối thiểu 1).
            int amount = Mathf.Max(1, loot != null ? loot.valueInMillions : 1);
            QuestManager manager = QuestManager.Instance;
            if (manager != null && !string.IsNullOrEmpty(loadQuestId) && !string.IsNullOrEmpty(loadObjectiveId))
                manager.ReportProgress(loadQuestId, loadObjectiveId, -amount);
        }
    }

    // Món vẫn "đang trong xe" khi vật lý/khác đẩy nó rời khoang đổ quá xa:
    // trục Z khu khoang chỉ độ sâu ~1m, cho phép trượt nhẹ 2m cho an toàn.
    private bool IsStillInTruck(LootItem loot)
    {
        if (loot == null || dropMarker == null) return true;
        return Vector3.Distance(loot.transform.position, dropMarker.position) < 2f;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.3f, 0.6f);
        if (zoneCollider == null) zoneCollider = GetComponent<Collider>();
        if (zoneCollider != null) Gizmos.DrawCube(zoneCollider.bounds.center, zoneCollider.bounds.size);

        // Trong edit-mode: tự tạo ItemSlot thiếu (có Undo) để người làm kéo
        // collider khoang đổ trực tiếp trong Scene view.
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EnsureDropMarker();
        }
#endif

        // Viền vàng quanh ItemSlot với mũi tên chỉ hướng rơi đồ.
        if (dropMarker != null)
        {
            Collider col = dropMarker.GetComponent<Collider>();
            Bounds b = col != null ? col.bounds : new Bounds(dropMarker.position, Vector3.one * 0.5f);
            Gizmos.color = new Color(1f, 0.85f, 0.1f, 1f);
            Gizmos.DrawWireCube(b.center, b.size);

            // Chỉ vẽ "vùng đồ rơi" một chút cao hơn cho trực quan.
            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.35f);
            Gizmos.DrawLine(b.center, b.center + Vector3.up * dropHeight);
            Gizmos.DrawWireCube(b.center + Vector3.up * dropHeight, b.size);
        }
    }
}
