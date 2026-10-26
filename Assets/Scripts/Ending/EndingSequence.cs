using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

// Bộ điều phối đoạn kết thắng của "Món Nợ Cuối Cùng".
//
// Luồng quest: loot_truck (50M VND) -> dong_cua (Đóng cửa xe lại) -> ve_thoi (VỀ THÔI).
//
// Role các mốc:
//  1) Đủ 50M: KHÔNG đụng vào cửa xe lái (cửa giữ nguyên hành vi thường).
//  2) Quest 'dong_cua' bắt đầu: kiểm tra nếu cửa xe đã đóng sẵn thì báo xong
//     ngay (DoorInteractable.MaybeReportClosed báo khi đóng đủ học).
//  3) Quest 've_thoi' bắt đầu -> UnlockEscape(): mở cửa xe lái Puerta_Fron.001
//     bằng cơ chế DoorInteractable hiện có, khoá tương tác mở/đóng thường,
//     bật glow của cửa, dựng vùng "GIỮ E ĐỂ THOÁT" (TruckEscapeDoor) quanh cửa.
//  4) Người chơi giữ E đủ lâu tại vùng -> TruckEscapeDoor gọi BeginEnding():
//     khoá gameplay, ẩn HUD/prompt, hoàn thành quest 've_thoi' cho StoryFlow,
//     fade đen rồi hiện "BẠN ĐÃ TRẢ HẾT NỢ" (EndScreenUI).
//
// Object này tự tạo bằng Bootstrap sau khi scene có QuestManager, không cần
// setup Inspector trong scene.
[DisallowMultipleComponent]
public sealed class EndingSequence : MonoBehaviour
{
    public static EndingSequence Instance { get; private set; }

    [Header("Quest Ids (trùng với hệ thống quest hiện có)")]
    [Tooltip("Quest lấy đồ 50M VND — xong thì nối quest đóng cửa.")]
    [SerializeField] private string lootQuestId = "loot_truck";
    [Tooltip("Quest 'ĐÓNG CỬA XE' giữa: bắt đầu sau 50M, hoàn thành khi đóng cửa xe.")]
    [SerializeField] private string closingQuestId = "dong_cua";
    [SerializeField] private string closingObjectiveId = "dong_cua_hau";
    [Tooltip("Quest 'VỀ THÔI' nối tiếp dong_cua; bắt đầu = mở khoá thoát, hoàn thành khi giữ E thoát.")]
    [SerializeField] private string escapeQuestId = "ve_thoi";
    [SerializeField] private string escapeObjectiveId = "thoat_xe";

    [Header("Cửa xe lái")]
    [Tooltip("GameObject cửa xe phía người lái trong scene MainGame.")]
    [SerializeField] private string driverDoorName = "Puerta_Fron.001";

    [Header("Cấu hình giữ E (ke thua dung may Hold E cua Interactor hien co)")]
    [SerializeField, Min(0.2f)] private float escapeHoldSeconds = 1.5f;

    [Header("Cấu hình kết thúc")]
    [Tooltip("Số giây fade sang đen (dùng unscaled time).")]
    [SerializeField, Range(0.5f, 6f)] private float fadeSeconds = 2f;
    [TextArea]
    [SerializeField] private string endText = "B\u1EA0N \u0110\u00C3 TR\u1EA2 H\u1EBET N\u1EE2";
    [Tooltip("Dừng toàn bộ gameplay phía sau (Time.timeScale = 0) khi màn hình đen xong.")]
    [SerializeField] private bool freezeTimeAfterFade = true;

    public bool EscapeUnlocked { get; private set; }
    public bool EndingStarted { get; private set; }

    private QuestManager hookedManager;
    private GameObject escapeZone;
    private DoorInteractable driverDoor;
    private InteractableGlow doorGlow;

    // ------------------------------------------------------------ vòng đời

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // Unity tắt Reload Domain: phải xoá instance cũ để không kẹt run trước.
        Instance = null;
    }

    // Sau khi scene load: tạo một instance để nghe QuestManager của scene hiện tại.
    // QuestManager chưa có ở scene menu thì Update() sau hooks lại khi vào scene game.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        GameObject root = new GameObject("EndingSequence");
        root.AddComponent<EndingSequence>();
        DontDestroyOnLoad(root); // sống qua việc load scene menu -> game
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        // Bám theo QuestManager của scene hiện tại: unsub cũ, sub mới.
        if (ReferenceEquals(hookedManager, QuestManager.Instance)) return;

        if (hookedManager != null)
        {
            hookedManager.QuestCompleted -= HandleQuestCompleted;
            hookedManager.QuestStarted -= HandleQuestStarted;
        }

        hookedManager = null;

        if (QuestManager.Instance != null)
        {
            hookedManager = QuestManager.Instance;
            hookedManager.QuestCompleted += HandleQuestCompleted;
            hookedManager.QuestStarted += HandleQuestStarted;

            // Bù cho quest đã bắt đầu/xong trước khi hook được (load scene muộn...).
            QuestState escapeState = hookedManager.GetState(escapeQuestId);
            if (escapeState == QuestState.Active || escapeState == QuestState.Completed)
                UnlockEscape();

            if (hookedManager.GetState(closingQuestId) == QuestState.Active)
                StartCoroutine(CheckDoorsAlreadyClosed());
        }
    }

    private void OnDestroy()
    {
        if (hookedManager != null)
        {
            hookedManager.QuestCompleted -= HandleQuestCompleted;
            hookedManager.QuestStarted -= HandleQuestStarted;
        }
        if (Instance == this) Instance = null;
    }

    private void HandleQuestStarted(QuestRuntime runtime)
    {
        if (runtime == null) return;

        // Quest 'dong_cua' bắt đầu khi cửa có thể đã đóng sẵn (người chơi đóng
        // truoc them khi quest kip chạy): đợi 1 frame rồi kiểm lại.
        if (runtime.QuestId == closingQuestId)
            StartCoroutine(CheckDoorsAlreadyClosed());

        // Quest 'VỀ THÔI' bắt đầu = giai đoạn thoát được mở khoá.
        if (runtime.QuestId == escapeQuestId) UnlockEscape();
    }

    private void HandleQuestCompleted(QuestRuntime runtime)
    {
        if (runtime == null || runtime.QuestId != lootQuestId) return;

        // Đủ 50M: KHÔNG mở cửa xe ở đây nữa. Cửa chỉ tương tác lại ở bước
        // 've_thoi' sau khi người chơi đóng cửa xe (UnlockEscape qua EventStarted).
    }

    // ------------------------------------------------------------ mở khoá thoát

    private void UnlockEscape()
    {
        if (EscapeUnlocked) return; // chống QuestCompleted bung nhiều lần
        EscapeUnlocked = true;

        GameObject doorGo = GameObject.Find(driverDoorName);
        if (doorGo == null)
        {
            Debug.LogError($"[Ending] Không tìm thấy cửa xe lái '{driverDoorName}' trong scene.", this);
            return;
        }

        driverDoor = doorGo.GetComponent<DoorInteractable>();
        if (driverDoor == null)
        {
            Debug.LogError($"[Ending] '{driverDoorName}' không có DoorInteractable.", this);
            return;
        }

        // Mở tự động bằng đúng cơ chế cửa hiện có (Update quay quanh bản lề) —
        // giờ được gọi TẠI quest 've_thoi' (sau khi người chơi đóng cửa xe),
        // không còn mở ngay khi đủ 50M nữa. Khoá E mở/đóng thường sau đó.
        driverDoor.ForceOpen();
        driverDoor.SetInteractionLocked(true);
        // Glow của cửa (component InteractableGlow có sẵn): sáng liên tục báo
        // "nơi này tương tác được". Interactor vẫn thêm highlight khi nhắm vào.
        doorGlow = doorGo.GetComponent<InteractableGlow>();
        if (doorGlow != null) doorGlow.SetQuestHint(true);
        else Debug.LogWarning("[Ending] Cửa xe lái không có InteractableGlow, bỏ qua glow gợi ý.", this);

        BuildEscapeZone(doorGo);

        Debug.Log($"[Ending] Quest '{escapeQuestId}' bật — '{driverDoorName}' tự mở cho lên xe, vùng 'GIỮ E ĐỂ THOÁT' sẵn sàng.", this);
    }

    // Quest 'dong_cua' bắt đầu nhưng cửa có thể đã đóng từ trước → nếu đúng là
    // đóng hẳn (cả cánh ghép) thì báo tiến độ luôn, không ép đóng lại lần nữa.
    private IEnumerator CheckDoorsAlreadyClosed()
    {
        if (doorsClosedChecked) yield break;
        doorsClosedChecked = true;
        yield return null; // đợi hệ thống/load ổn định 1 frame

        QuestManager manager = QuestManager.Instance;
        if (manager == null) yield break;

        DoorInteractable[] all = FindObjectsByType<DoorInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool foundReporter = false;
        for (int i = 0; i < all.Length; i++)
        {
            DoorInteractable door = all[i];
            if (door == null || door.CloseReportQuestId != closingQuestId) continue;

            foundReporter = true;
            if (door.IsClosedDoor)
            {
                manager.ReportProgress(closingQuestId, closingObjectiveId, 1);
                Debug.Log($"[Ending] '{door.name}' đã đóng sẵn — quest '{closingQuestId}' coi như xong.", this);
                break;
            }
        }

        if (!foundReporter)
            Debug.LogWarning($"[Ending] Không tìm thấy cửa nào báo quest '{closingQuestId}' (kiểm tra Close Report trên cửa hậu).", this);
    }

    private bool doorsClosedChecked;

    private void BuildEscapeZone(GameObject doorGo)
    {
        // Vùng là GameObject con của cửa: raycast của Interactor trúng collider
        // trigger riêng của con. Vùng xoay/mở theo cửa nên luôn bám đúng chỗ.
        escapeZone = new GameObject("EscapePoint")
        {
            layer = doorGo.layer,
            tag = doorGo.tag
        };
        escapeZone.transform.SetParent(doorGo.transform, false);

        TruckEscapeDoor trigger = escapeZone.AddComponent<TruckEscapeDoor>();
        trigger.Initialize(driverDoor, escapeHoldSeconds);

        // Kích thước vùng đo từ collider THẬT của cửa (model xe dùng scale lớn,
        // không thể đoán trước): 70% rộng cửa, cao gần hết cửa, dày thêm 0.4m
        // để OverlapSphere của Interactor chạm được từ hai phía cửa.
        BoxCollider box = escapeZone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        SizeZoneAroundDoor(box, doorGo);
    }

    private void SizeZoneAroundDoor(BoxCollider box, GameObject doorGo)
    {
        Collider doorCollider = doorGo.GetComponent<Collider>();
        Bounds doorBounds = doorCollider != null ? doorCollider.bounds : new Bounds(doorGo.transform.position, Vector3.one);

        // Kẹp kích thước thế giới trong khoảng hợp lý cho một cửa xe (m).
        float worldWidth = Mathf.Clamp(doorBounds.size.x * 0.7f, 0.6f, 2.4f);
        float worldHeight = Mathf.Clamp(doorBounds.size.y * 0.9f, 1.2f, 2.4f);
        float worldDepth = Mathf.Clamp(doorBounds.size.z * 1.5f + 0.4f, 0.5f, 1.4f);

        // Đổi từ thế giới về local (cửa nằm trong model scale lớn -> lossyScale lớn).
        Vector3 lossy = doorGo.transform.lossyScale;
        box.size = new Vector3(
            worldWidth / Safe(lossy.x),
            worldHeight / Safe(lossy.y),
            worldDepth / Safe(lossy.z));
        box.center = doorGo.transform.InverseTransformPoint(doorBounds.center);
    }

    private static float Safe(float value)
    {
        float v = Mathf.Abs(value);
        return v < 0.0001f ? 0.0001f : v;
    }

    // ------------------------------------------------------------ kết thúc

    /// <summary>
    /// Gọi từ TruckEscapeDoor sau khi giữ E đủ lâu. Chỉ chạy đúng một lần.
    /// </summary>
    public void BeginEnding()
    {
        if (EndingStarted) return; // chặn kích hoạt nhiều lần
        if (!EscapeUnlocked)       // chặn kết thúc khi chưa đủ 50M (vùng không nên tồn tại, thêm lớp khoá)
        {
            Debug.LogWarning("[Ending] Kết thúc bị chặn: chưa đủ 50M (quest 'loot_truck' chưa hoàn thành).", this);
            return;
        }

        EndingStarted = true;

        LockGameplay();

        // Hoàn thành quest 'VỀ THÔI' để StoryFlow (bước S5) và quest tracker đủ
        // điều kiện khép lại. Chỉ báo khi quest thật sự đang chạy.
        QuestManager manager = QuestManager.Instance;
        if (manager != null && manager.GetState(escapeQuestId) == QuestState.Active)
            manager.ReportProgress(escapeQuestId, escapeObjectiveId, 1);

        // Xoá vùng thoát để không còn tương tác nào có thể tái kích hoạt.
        if (escapeZone != null) Destroy(escapeZone);
        escapeZone = null;
        if (doorGlow != null) doorGlow.SetQuestHint(false);

        EndScreenUI screen = EndScreenUI.Create();
        StartCoroutine(screen.PlayRoutine(endText, fadeSeconds, freezeTimeAfterFade));
        Debug.Log($"[Ending] Bắt đầu kết thúc — fade {fadeSeconds:0.0}s rồi hiện '{endText}'.", this);
    }

    // ----------------------------------------------------------- khoá gameplay / ẩn HUD

    private void LockGameplay()
    {
        // 1) Khoá hành động của player: di chuyển, nhặt/đặt đồ, tương tác E,
        //    quay camera bằng chuột (Cinemachine).
        SetEnabled<ThirdPersonController>(false);
        SetEnabled<PlayerGrab>(false);
        SetEnabled<Interactor>(false);

        var mouseLook = FindObjectsByType<CinemachineInputAxisController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < mouseLook.Length; i++) mouseLook[i].enabled = false;

        // 2) Ngăn jumpscare/điện thoại chen giữa đoạn fade.
        SetEnabled<JumpscareManager>(false);
        SetEnabled<EnemyJumpscareTrigger>(false);
        SetEnabled<GTA_PhoneController>(false);

        // 3) ẩn mọi HUD & prompt còn hiển thị.
        HideUI<InteractPromptUI>();
        HideUI<HoldRingUI>();
        HideUI<CrosshairUI>();
        HideUI<QuestTrackerUI>();
        HideUI<StaminaBarUI>();
        HideUI<CaptionUI>();
        HideUI<SlotsHotbarUI>();
        HideUI<InventoryUI>();
        HideUI<NoteReaderUI>();
        HideUI<PhoneUIManager>();
    }

    private static void SetEnabled<T>(bool enabled) where T : MonoBehaviour
    {
        T[] all = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null) all[i].enabled = enabled;
        }
    }

    // Tắt component + ẩn chính GameObject của từng khối UI (không đụng root canvas
    // chung để không giết nhầm các UI khác).
    private static void HideUI<T>() where T : MonoBehaviour
    {
        T[] all = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Behaviour behaviour = all[i];
            if (behaviour == null) continue;

            behaviour.enabled = false;
            if (behaviour.gameObject.activeSelf) behaviour.gameObject.SetActive(false);
        }
    }
}
