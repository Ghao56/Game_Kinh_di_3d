using System;
using UnityEngine;
using UnityEngine.Events;

// Cua mo/dong bang E qua Interactor. Cua chinh co the ghep voi cac canh "linkedDoors"
// (ceradoi cua hau xe: mot lan E mo/dong ca hai cung luc). Canh phu (isSlave) khong nhan E rieng.
[RequireComponent(typeof(Collider))]
public class DoorInteractable : MonoBehaviour, IInteractable, IInteractableAvailability
{
    [Header("Cua")]
    [Tooltip("Phan hoc quay quanh ban le. De trong la dung chinh GameObject nay.")]
    [SerializeField] private Transform pivot;
    [Tooltip("Goc mo (do) quanh truc doc ben the gioi. Cua mo phai thi -90, mo trai thi 90.")]
    [SerializeField] private float openAngle = 90f;
    [Tooltip("Thoi gian quay xong (giay).")]
    [SerializeField, Range(0.1f, 5f)] private float openDuration = 0.6f;

    [Header("Ghep canh (cua hai canh)")]
    [Tooltip("Cac canh mo/dong CUNG luc voi canh chinh. Canh trong danh sach nen bat isSlave.")]
    [SerializeField] private DoorInteractable[] linkedDoors = new DoorInteractable[0];

    [Tooltip("Canh phu: chi quay theo canh chinh, tu no khong nhan E (khong bi raycast bat lam interactable doc lap).")]
    [SerializeField] private bool isSlave = false;

    [Header("Khoa nhiem vu (bo trong neu khong khoa)")]
    [Tooltip("Chi cho tuong tac khi quest nay da bat dau. De trong = luon cho tuong tac.")]
    [SerializeField] private string gateQuestId = string.Empty;
    [Tooltip("Chi cho tuong tac khi muc tieu nay chuoi hoan thanh. De trong = khong xet muc tieu.")]
    [SerializeField] private string gateObjectiveId = string.Empty;

    [Header("Bao nhiem vu khi mo lan dau")]
    [Tooltip("Sau khi lan mo dau tien xong (ca hai canh mo hoc), bao quest/muc tieu nay xong. De trong = khong bao.")]
    [SerializeField] private string reportQuestId = string.Empty;
    [SerializeField] private string reportObjectiveId = string.Empty;

    [Header("Bao nhiem vu khi dong (cua sang glow goi y trong luc quest chay)")]
    [Tooltip("Bung voi moi lan canh dong hoc xong khi quest/muc tieu nay DANG CHAY (Dong cua xe). Trong luc quest nay chay, cua sang glow goi y cho toi khi dong han. Khong gia han khi quest chua bat dau; de trong = khong bao.")]
    [SerializeField] private string closeReportQuestId = string.Empty;
    [SerializeField] private string closeReportObjectiveId = string.Empty;

    [Header("Khoa ngoai (doan ket: cua xe lai)")]
    [Tooltip("Bat = cua tu choi moi tuong tac mo/dong bang E (IsAvailable false). Cua van quay theo lenh code (ForceOpen) va onFullyOpened van bung dung hen.")]
    [SerializeField] private bool interactionLocked;

    [Header("Prompt hiển thị")]
    [Tooltip("Chữ hiện khi cửa đang đóng (nhấn E để mở).")]
    [SerializeField] private string promptWhenClosed = "Mở cửa";
    [Tooltip("Chữ hiện khi cửa đang mở (nhấn E để đóng).")]
    [SerializeField] private string promptWhenOpen = "Đóng cửa";

    [Header("Su kien")]
    [Tooltip("Bung ngay khi bam E (gan am thanh keo cua).")]
    [SerializeField] private UnityEvent onToggle;
    [Tooltip("Bung khi canh chinh VA toan bo canh lien ket deu mo hoc het.")]
    [SerializeField] private UnityEvent onFullyOpened;
    [Tooltip("Bung khi canh chinh VA toan bo canh lien ket deu dong hoc het.")]
    [SerializeField] private UnityEvent onFullyClosed;

    public event Action FullyOpened;
    public event Action FullyClosed;

    private Transform hinge;
    private Quaternion closedRot;
    private Quaternion openRot;
    private bool open;
    private bool unlockedOnce;
    private bool reportedFirstOpen;
    private bool firedFullyOpen;
    private bool firedFullyClosed;
    private InteractableGlow glow;
    private InteractableGlow[] linkedDoorsGlows;
    private bool hintOn;
    // Canh phu: hoi canh chinh (IsAvailable, PromptText, E) de trong hai canh cung loai khong bi "canh phu cam dao cuoc".
    private DoorInteractable masterDoor;

    // InteractableGlow doc de glow canh chinh keo glow canh ghep sang theo.
    public DoorInteractable[] LinkedDoors => linkedDoors;

    public string PromptText
    {
        get
        {
            DoorInteractable source = this;
            if (isSlave && masterDoor != null)
                source = masterDoor;
            string p = source.open ? source.promptWhenOpen : source.promptWhenClosed;
            if (string.IsNullOrEmpty(p))
                p = source.open ? "Đóng cửa" : "Mở cửa";
            return p;
        }
    }
    public bool HoldToInteract => false;
    public float HoldDuration => 0f;

    // Goc con lai so voi vi tri dich anh phai nho hon cai nay coi nhu la da ton vi.
    private const float FullyTolerance = 0.5f;

    /// Canh chinh VA toan bo canh lien ket deu o vi tri mo hoc, va y dinh la MO.
    public bool IsFullyOpen
    {
        get
        {
            if (!open) return false;
            return FullyReached(isOpened: true);
        }
    }

    /// Interactor: canh phu khong duoc chon, canh dang quay khong nhan E, chua duoc khoa thi khong nhan E.
    /// Da bi khoa ngoai (SetInteractionLocked) thi khong nhan E duoi bat ky hinh thuc nao.
    public bool IsAvailable
    {
        get
        {
            if (interactionLocked) return false;
            if (isSlave) return masterDoor != null && masterDoor.IsAvailable;
            if (Moving) return false;
            return GateOpen();
        }
    }

    private bool Moving
    {
        get
        {
            float remain = Quaternion.Angle(hinge.rotation, open ? openRot : closedRot);
            return remain > FullyTolerance;
        }
    }

    private bool GateOpen()
    {
        if (string.IsNullOrEmpty(gateQuestId)) return true;
        if (unlockedOnce) return true; // da mo tu mot lan: cho dong/mo lai binh thuong

        QuestManager manager = QuestManager.Instance;
        if (manager == null) return true; // khong co manager thi khong khoa (giu hanh vi cua cua cu)

        if (string.IsNullOrEmpty(gateObjectiveId))
            return manager.GetState(gateQuestId) == QuestState.Active;

        return manager.IsObjectiveOpen(gateQuestId, gateObjectiveId);
    }

    private void Awake()
    {
        hinge = pivot != null ? pivot : transform;
        closedRot = hinge.rotation;
        // Quay quanh y ben the gioi *truoc khi* xoac rot hien tai -> swing dung truc doc khong phu thuoc truc cua model.
        openRot = Quaternion.AngleAxis(openAngle, Vector3.up) * closedRot;

        // Du phong glow cho nghe vu khoa: glow canh chinh sang thi glow canh ghep cung sang.
        glow = GetComponent<InteractableGlow>();
        linkedDoorsGlows = new InteractableGlow[linkedDoors.Length];
        for (int i = 0; i < linkedDoors.Length; i++)
            linkedDoorsGlows[i] = linkedDoors[i] != null ? linkedDoors[i].GetComponent<InteractableGlow>() : null;

        // Canh phu tim canh chinh de hoi, nhan dam truyet mo cap hai cua.
        if (isSlave)
        {
            for (int i = 0; i < linkedDoors.Length; i++)
            {
                DoorInteractable door = linkedDoors[i];
                if (door == null || door.isSlave) continue;
                masterDoor = door;
                break;
            }
        }
    }

    public void OnInteract(Interactor interactor)
    {
        // Canh phu dam truyet den canh chinh: mot lan E mo/dong ca hai canh, khong lieu ro canh rieng le.
        if (isSlave)
        {
            if (masterDoor != null) masterDoor.OnInteract(interactor);
            return;
        }
        if (!IsAvailable) return;

        open = !open;
        unlockedOnce = true;

        // Canh ghep quay theo canh chinh: neu thi doi mo/dong ca cum (bug cu: khong co ai doi canh ghep).
        for (int i = 0; i < linkedDoors.Length; i++)
        {
            DoorInteractable door = linkedDoors[i];
            if (door == null) continue;
            door.open = open;
        }

        onToggle?.Invoke();
        Debug.Log($"[DoorInteractable] {name} {(open ? "MO" : "DONG")} (kem {linkedDoors.Length} canh ghep)", this);
    }

    private void Update()
    {
        Quaternion target = open ? openRot : closedRot;
        Quaternion current = hinge.rotation;
        if (Quaternion.Angle(current, target) >= 0.05f)
            hinge.rotation = Quaternion.Slerp(current, target, Time.deltaTime / openDuration);

        if (isSlave) return;

        // Glow goi y: cua co khoa quest (gateQuestId) sang xanh khi quest/muc tieu mo, tat sau lan mo dau.
        UpdateQuestHint();

        // Su kien mo/dong HOC chi tu canh chinh; canh phu quay rieng cung toc do nhung khong ban su kien.
        bool reachedOpen = open && FullyReached(isOpened: true);
        bool reachedClosed = !open && FullyReached(isOpened: false);
        if (reachedOpen && !firedFullyOpen)
        {
            firedFullyOpen = true;
            firedFullyClosed = false;
            ReportFirstOpened();
            onFullyOpened?.Invoke();
            FullyOpened?.Invoke();
        }
        else if (reachedClosed && !firedFullyClosed)
        {
            firedFullyClosed = true;
            firedFullyOpen = false;
            onFullyClosed?.Invoke();
            FullyClosed?.Invoke();
            MaybeReportClosed();
        }
    }

    private void UpdateQuestHint()
    {
        // Sang khi: quest MỞ cửa (gate) còn mở và chưa mở lần nào, HOẶC quest
        // ĐÓNG cửa (closeReport) ĐANG CHẠY mà chưa đóng hẳn. Vậy là 2 cánh cửa
        // hậu sáng ngay khi quest "dong_cua" bắt đầu cho người chơi biết.
        bool on = (!unlockedOnce && HintGateOpen()) || CloseHintOpen();
        if (on == hintOn) return;

        hintOn = on;
        if (glow != null) glow.SetQuestHint(on);
        for (int i = 0; i < linkedDoorsGlows.Length; i++)
        {
            if (linkedDoorsGlows[i] != null) linkedDoorsGlows[i].SetQuestHint(on);
        }
    }

    private bool HintGateOpen()
    {
        if (string.IsNullOrEmpty(gateQuestId)) return false; // cua khong khoa thi khong can glow goi y

        QuestManager manager = QuestManager.Instance;
        if (manager == null) return false;

        if (string.IsNullOrEmpty(gateObjectiveId))
            return manager.GetState(gateQuestId) == QuestState.Active;

        return manager.IsObjectiveOpen(gateQuestId, gateObjectiveId);
    }

    // Cửa có BÁO TIẾN ĐỘ khi đóng (closeReportQuestId): sáng xuyên suốt khi
    // quest đó ĐANG CHẠY mà cửa (kèm các cánh ghép) chưa đóng hẳn — không cần
    // có gateQuestId. Đóng xong tự tắt qua IsClosedDoor.
    private bool CloseHintOpen()
    {
        if (string.IsNullOrEmpty(closeReportQuestId)) return false;

        QuestManager manager = QuestManager.Instance;
        if (manager == null) return false;

        return manager.GetState(closeReportQuestId) == QuestState.Active && !IsClosedDoor;
    }

    private void ReportFirstOpened()
    {
        if (reportedFirstOpen || string.IsNullOrEmpty(reportQuestId)) return;

        reportedFirstOpen = true;
        QuestManager manager = QuestManager.Instance;
        if (manager == null || string.IsNullOrEmpty(reportObjectiveId)) return;

        manager.ReportProgress(reportQuestId, reportObjectiveId);
    }

    // Bao quest khi canh chinh VA cac canh ghep dong hoc het VA quest bao DO
    // DANG CHAY. Khong lap: quest chua bat dau thi khong bao, va QuestManager
    // clamp so tien do bang required nen bao them cung vo hai.
    private void MaybeReportClosed()
    {
        if (string.IsNullOrEmpty(closeReportQuestId) || string.IsNullOrEmpty(closeReportObjectiveId)) return;

        QuestManager manager = QuestManager.Instance;
        if (manager == null) return;
        if (manager.GetState(closeReportQuestId) != QuestState.Active) return;

        manager.ReportProgress(closeReportQuestId, closeReportObjectiveId);
    }

    // Cho EndingSequence hoi: cua nao dang bao quest "dong cua" (ide lay dung
    // tham chieu thay vi tim bang ten).
    public string CloseReportQuestId => closeReportQuestId;
    public string CloseReportObjectiveId => closeReportObjectiveId;

    /// Canh chinh VA toan bo canh ghep deu da dong hoc (dung de phat hien
    /// truong hop quest 'dong_cua' bat dau khi cua da dong tu truoc).
    public bool IsClosedDoor
    {
        get
        {
            if (open) return false;
            return FullyReached(isOpened: false);
        }
    }

    private bool FullyReached(bool isOpened)
    {
        Quaternion target = isOpened ? openRot : closedRot;
        if (Quaternion.Angle(hinge.rotation, target) > FullyTolerance) return false;

        for (int i = 0; i < linkedDoors.Length; i++)
        {
            DoorInteractable door = linkedDoors[i];
            if (door == null) continue;
            if (door.open != isOpened) return false;
            Quaternion other = isOpened ? door.openRot : door.closedRot;
            if (Quaternion.Angle(door.hinge.rotation, other) > FullyTolerance) return false;
        }

        return true;
    }

    // ------------------------------------------------- dieu khien bang code (doan ket)

    // Mo cua cho doan ket: dung dung may mo cua hien co (Update quay quanh ban le,
    // onFullyOpened/FullyOpened van bung dung hen). Khong chu dong dong cua.
    public void ForceOpen()
    {
        open = true;
        unlockedOnce = true;
    }

    // Khoa mo/dong bang phim E nhung van giu Update chay de cua quay tiep den vi tri mo.
    // Chi dung cho cua xe lai o doan ket; cac cua khac mac dinh interactionLocked = false.
    public void SetInteractionLocked(bool locked)
    {
        interactionLocked = locked;
    }

    private void OnValidate()
    {
        if (hinge == null && pivot == null) hinge = transform;
    }
}
