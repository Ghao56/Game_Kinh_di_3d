using UnityEngine;

public interface IInteractable
{
    string PromptText { get; }        // VD: "Đọc giấy"
    bool HoldToInteract { get; }      // false = Press, true = giữ E
    float HoldDuration { get; }       // dùng khi HoldToInteract = true
    void OnInteract(Interactor interactor);
}