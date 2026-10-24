using UnityEngine;

// Gắn script này vào vật phẩm tiêu dùng (túi muối, chai rượu...). Object cần
// Collider (trigger hay thường đều được - SphereCast của Interactor bắt cả
// trigger). NHẮM qua tâm màn hình là hiện prompt "[E] Nhặt <tên>", bấm E để
// nhặt - giống hệt loot (PlayerGrab không đụng vào đường này).
//
// Nhặt = cộng số lượng vào kho (stack ×N trên 1 ô hotbar, bấm F để ném/phun
// như cũ), rồi vật thể tự hủy. KHÔNG hiện thông báo thoại khi nhặt thành công
// - người chơi nhìn thấy trực tiếp badge ×N trên hotbar.
//
// Bảo vệ không mất đồ im lặng:
// - Hotbar KHÔNG còn ô trống cho loại đồ CHƯA CÓ Ô => KHÔNG cộng kho, KHÔNG
//   Destroy (đồ nằm nguyên trên sàn) + báo "Tay mình hết chỗ rồi!" khi bấm E
//   (cooldown 1.5s, một lời mỗi lần bấm, không spam).
// DialogueManager.ShowLine KHÔNG khóa nhân vật (AcquireControl chỉ ẩn prompt
// + khóa cursor) nên thông báo không làm game khựng.
public class SaltBagPickup : MonoBehaviour, IInteractable
{
    // Tên vật phẩm - đặt khác nhau cho từng loại, ví dụ "Salt", "Wine"...
    public string itemId = "Salt";

    public int amount = 1;

    // Cooldown thông báo "hết chỗ" (dùng chung mọi túi/chai trong scene).
    private const float NotifyCooldownSeconds = 1.5f;
    private static float nextFullHandsTime;

    private ThrowableSelector hotbar;

    // Kho tìm được qua lần tương tác gần nhất (fallback cho lần sau).
    private PlayerInventory cachedInventory;

    // Chặn cộng kho 2 lần nếu cùng frame có nhiều lời gọi (E + route khác).
    private bool consumed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => nextFullHandsTime = 0f;

    private void Awake()
    {
        if (hotbar == null) hotbar = FindFirstObjectByType<ThrowableSelector>();
    }

    // ------------------------------------------------------------ IInteractable

    // Interactor tự gắn tiền tố "[E] ", chỉ cần đoạn văn bản.
    public string PromptText
    {
        get
        {
            string ten = hotbar != null ? hotbar.GetDisplayName(itemId) : itemId;
            return $"Nhặt {ten}";
        }
    }

    public bool HoldToInteract => false; // bấm E nhặt ngay, không giữ
    public float HoldDuration => 0f;

    public void OnInteract(Interactor interactor)
    {
        PlayerInventory inventory = interactor != null
            ? interactor.GetComponentInParent<PlayerInventory>()
            : null;

        if (inventory == null && cachedInventory == null)
            cachedInventory = FindFirstObjectByType<PlayerInventory>();

        TryPickUpConsumable(inventory != null ? inventory : cachedInventory);
    }

    // ------------------------------------------------------------ nhặt target

    // PlayerGrab (route tương tác) gọi khi người chơi bấm E lên món này.
    // Trả về TRUE nếu đã nhặt (kho được cộng, đồ tự hủy).
    // Trả về FALSE nếu hết chỗ (đã báo thoại, đồ còn nguyên trên sàn)
    // hoặc thiếu tham chiếu kho.
    public bool TryPickUpConsumable(PlayerInventory inventory)
    {
        if (consumed || inventory == null) return false;

        if (hotbar == null) hotbar = FindFirstObjectByType<ThrowableSelector>();

        // Hotbar còn nhận thêm loại này không? (đã có stack -> luôn nhận;
        // chưa có stack -> cần 1 ô trống). Scene không có hotbar: giữ hành vi cũ.
        if (hotbar != null && !hotbar.CanAcceptItem(itemId))
        {
            if (Time.unscaledTime >= nextFullHandsTime &&
                TryShowLine("Tay mình hết chỗ rồi!", "Player"))
            {
                nextFullHandsTime = Time.unscaledTime + NotifyCooldownSeconds;
            }
            return false;
        }

        inventory.AddItem(itemId, amount);
        consumed = true;

        Destroy(gameObject);
        return true;
    }

    // Hiện 1 dòng thoại ngắn tự tắt qua DialogueManager. Trả về TRUE nếu vừa
    // hiển thị (caller tự đặt cooldown). Bỏ qua khi không có DialogueManager
    // hoặc đang có thoại khác mở (không chèn dòng vào giữa thoại cốt truyện).
    private static bool TryShowLine(string text, string speaker)
    {
        DialogueManager dlg = DialogueManager.Instance;
        if (dlg == null || dlg.IsActive) return false;

        dlg.ShowLine(text, speaker, autoAdvance: true);
        return true;
    }
}
