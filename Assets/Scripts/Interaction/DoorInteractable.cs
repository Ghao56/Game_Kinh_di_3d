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

    public string PromptText => open ? promptWhenOpen : promptWhenClosed;
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
    public bool IsAvailable
    {
        get
        {
            if (isSlave) return false;
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
    }

    public void OnInteract(Interactor interactor)
    {
        if (isSlave) return;
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
        }
    }

    private void ReportFirstOpened()
    {
        if (reportedFirstOpen || string.IsNullOrEmpty(reportQuestId)) return;

        reportedFirstOpen = true;
        QuestManager manager = QuestManager.Instance;
        if (manager == null || string.IsNullOrEmpty(reportObjectiveId)) return;

        manager.ReportProgress(reportQuestId, reportObjectiveId);
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

    private void OnValidate()
    {
        if (hinge == null && pivot == null) hinge = transform;
    }
}
