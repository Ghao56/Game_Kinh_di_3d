using UnityEngine;
using UnityEngine.Events;

// Cua / tu lanh mo dong bang E. Nhan Interactor: gan thi PromptUI hien "[E] Mo cua", bam E la quay quanh ban le.
[RequireComponent(typeof(Collider))]
public class DoorInteractable : MonoBehaviour, IInteractable
{
    [Header("Cua")]
    [Tooltip("Phan hoc quay quanh ban le. De trong la dung chinh GameObject nay.")]
    [SerializeField] private Transform pivot;
    [Tooltip("Goc mo (do) quanh truc doc ben the gioi. Cua mo phai thi -90, mo trai thi 90.")]
    [SerializeField] private float openAngle = 90f;
    [Tooltip("Thoi gian quay xong (giay).")]
    [SerializeField, Range(0.1f, 5f)] private float openDuration = 0.6f;

    [Header("Su kien")]
    [Tooltip("Bung sau khi bam E (gan am thanh ke chua tai day).")]
    [SerializeField] private UnityEvent onToggle;

    private Transform hinge;
    private Quaternion closedRot;
    private Quaternion openRot;
    private bool open;

    public string PromptText => open ? "Dong cua" : "Mo cua";
    public bool HoldToInteract => false;
    public float HoldDuration => 0f;

    private void Awake()
    {
        hinge = pivot != null ? pivot : transform;
        closedRot = hinge.rotation;
        // Quay quanh y ben the gioi * truoc khi * xoac rot hien tai -> swing dung truc doc khong phu thuoc truc cua model.
        openRot = Quaternion.AngleAxis(openAngle, Vector3.up) * closedRot;
    }

    public void OnInteract(Interactor interactor)
    {
        open = !open;
        onToggle?.Invoke();
        // Log de kiem tra nhanh, xa luc an dinh.
        Debug.Log($"[DoorInteractable] {name} {(open ? "MO" : "DONG")}", this);
    }

    private void Update()
    {
        Quaternion target = open ? openRot : closedRot;
        Quaternion current = hinge.rotation;
        if (Quaternion.Angle(current, target) < 0.05f) return;
        hinge.rotation = Quaternion.Slerp(current, target, Time.deltaTime / openDuration);
    }

    private void OnValidate()
    {
        if (hinge == null && pivot == null) hinge = transform;
    }
}
