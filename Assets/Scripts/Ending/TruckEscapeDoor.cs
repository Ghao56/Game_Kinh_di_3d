using UnityEngine;
using UnityEngine.Events;

// Vùng "GIỮ E ĐỂ THOÁT" trên cửa xe lái (Puerta_Fron.001) của đoạn kết.
// EndingSequence tự tạo GameObject con của cửa và AddComponent vùng này khi
// quest 've_thoi' (VỀ THÔI) bắt đầu — tức là SAU khi người chơi đã chất đủ 50M
// VÀ đóng cửa xe — nên trước đó vùng không tồn tại, không thể kích hoạt sớm. Vùng kế thừa đúng máy Hold E của
// Interactor (HoldToInteract + HoldDuration): thả E giữa chừng, Interactor tự
// reset tiến độ, không bao giờ kích hoạt nếu chưa giữ đủ lâu.
//
// KHÔNG gắn cùng GameObject với DoorInteractable (hai IInteractable trên một
// vật sẽ làm Interactor không đoán được cái nào thắng): vùng là GameObject con
// riêng, ray trúng collider trigger của con thì chỉ vùng này được chọn; tay
// nhắm cửa khi đứng ngoài vùng thì cửa đã bị interactionLocked nên không hiện
// prompt mở/đóng.
public class TruckEscapeDoor : MonoBehaviour, IInteractable, IInteractableAvailability, IProximityInteractable
{
    [Header("Tham chiếu")]
    [Tooltip("Cửa xe lái: vùng chỉ sẵn sàng khi cửa đã mở hẳn.")]
    [SerializeField] private DoorInteractable driverDoor;

    [Header("Cấu hình")]
    [SerializeField, Min(0.2f)] private float holdDuration = 1.5f;
    [Tooltip("Hiển thị đúng cơ chế prompt của Interactor, không thêm tiền tố khác.")]
    [SerializeField] private string promptText = "GI\u1EEE E \u0110\u1EC2 THO\u00C1T";

    [Header("Sự kiện")]
    [Tooltip("Bung ngay khi giữ E thành công, trước khi fade bắt đầu.")]
    [SerializeField] private UnityEvent onEscaped;

    // EndingSequence gọi khi dựng vùng.
    public void Initialize(DoorInteractable door, float holdSeconds)
    {
        driverDoor = door;
        holdDuration = Mathf.Max(0.2f, holdSeconds);
    }

    // Chỉ tương tác được khi: đoạn kết đã mở khoá, kết thúc chưa chạy, cửa đã mở hẳn.
    public bool IsAvailable
    {
        get
        {
            if (EndingSequence.Instance == null) return false;
            if (!EndingSequence.Instance.EscapeUnlocked) return false;
            if (EndingSequence.Instance.EndingStarted) return false;
            return driverDoor != null && driverDoor.IsFullyOpen;
        }
    }

    public string PromptText => promptText;
    public bool HoldToInteract => true;
    public float HoldDuration => holdDuration;

    // Vùng theo quy chuẩn proximity: đứng trong vùng collider là hiện prompt,
    // không cần nhìn vào. Interactor ưu tiên proximity nên không bị ray "cướp".
    public bool ReadyForProximityPrompt => IsAvailable;

    public void OnInteract(Interactor interactor)
    {
        if (!IsAvailable) return;

        if (onEscaped != null) onEscaped.Invoke();

        Debug.Log($"[TruckEscapeDoor] Giữ E tại {(driverDoor != null ? driverDoor.name : name)} đủ thời gian - bắt đầu đoạn kết.", this);

        // EndingSequence tự chặn lặp lại, ẩn prompt/khoá gameplay và chạy fade.
        if (EndingSequence.Instance != null) EndingSequence.Instance.BeginEnding();
    }
}
