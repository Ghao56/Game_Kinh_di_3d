using UnityEngine;

public interface IInteractable
{
    string PromptText { get; }        // VD: "Đọc giấy"
    bool HoldToInteract { get; }      // false = Press, true = giữ E
    float HoldDuration { get; }       // dùng khi HoldToInteract = true
    void OnInteract(Interactor interactor);
}

/// Vật chưa được phép tương tác. Interactor bỏ qua nó mỗi frame nên prompt và
/// viền highlight tự ẩn, không cần tắt collider.
public interface IInteractableAvailability
{
    bool IsAvailable { get; }
}