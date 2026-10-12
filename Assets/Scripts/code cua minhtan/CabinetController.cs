using UnityEngine;
using UnityEngine.Events;

public class CabinetController : MonoBehaviour
{
    public enum CabinetType { RotateDoor, SlideDrawer }

    [Header("Loại tủ: Cánh mở (Rotate) hoặc Ngăn kéo (Slide)")]
    [SerializeField] private CabinetType cabinetType = CabinetType.RotateDoor;

    [Header("Tốc độ mở/đóng")]
    [SerializeField] private float openSpeed = 5f;

    [Header("Góc xoay (Dành cho Cánh tủ mở)")]
    [SerializeField] private Vector3 openRotation; // Ví dụ: Y = 90 hoặc -90

    [Header("Vị trí (Dành cho Ngăn kéo trượt)")]
    [SerializeField] private Vector3 openPosition; // Ví dụ: Z hoặc X dịch chuyển khoảng 0.5

    [Header("Chống spam")]
    [SerializeField] private float toggleCooldown = 0.3f;

    [Header("Sự kiện khi tương tác")]
    [Tooltip("Bắn ra ở MỌI lần tương tác, không phân biệt mở/đóng")]
    public UnityEvent onInteract;
    [Tooltip("Chỉ bắn ra khi vừa MỞ")]
    public UnityEvent onOpened;
    [Tooltip("Chỉ bắn ra khi vừa ĐÓNG")]
    public UnityEvent onClosed;

    private Vector3 closedRotation;
    private Vector3 closedPosition;
    private bool isOpen = false;
    private bool isAnimating = false;
    private float lastToggleTime = -999f;

    public bool IsOpen => isOpen;

    private void Start()
    {
        if (cabinetType == CabinetType.RotateDoor)
            closedRotation = transform.localEulerAngles;
        else
            closedPosition = transform.localPosition;
    }

    private void Update()
    {
        if (!isAnimating) return;

        float t = 1f - Mathf.Exp(-openSpeed * Time.deltaTime); // độc lập framerate, không vượt quá 1

        if (cabinetType == CabinetType.RotateDoor)
        {
            Vector3 targetRot = isOpen ? openRotation : closedRotation;
            Quaternion target = Quaternion.Euler(targetRot);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, target, t);

            if (Quaternion.Angle(transform.localRotation, target) < 0.1f)
            {
                transform.localRotation = target;
                isAnimating = false;
            }
        }
        else
        {
            Vector3 targetPos = isOpen ? openPosition : closedPosition;
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, t);

            if ((transform.localPosition - targetPos).sqrMagnitude < 0.0001f)
            {
                transform.localPosition = targetPos;
                isAnimating = false;
            }
        }
    }

    public void ToggleCabinet()
    {
        if (Time.time - lastToggleTime < toggleCooldown) return;

        lastToggleTime = Time.time;
        isOpen = !isOpen;
        isAnimating = true;

        onInteract?.Invoke();

        if (isOpen)
            onOpened?.Invoke();
        else
            onClosed?.Invoke();
    }

    public void OpenCabinet()
    {
        if (isOpen) return;
        ToggleCabinet();
    }

    public void CloseCabinet()
    {
        if (!isOpen) return;
        ToggleCabinet();
    }
}