// Tương tác theo VÙNG: player đứng trong vùng collider là đủ, không cần nhìn vào.
// Interactor quét fallback các trigger chồng lên player; chỉ các IInteractable
// hiện thực interface này được chọn, nên các cửa/tủ bình thường không bị ảnh hưởng.
public interface IProximityInteractable
{
    bool ReadyForProximityPrompt { get; }
}
