using System.Collections.Generic;
using UnityEngine;

// Gắn script này vào vật phẩm tiêu dùng (túi muối, chai rượu...). Object cần
// Collider (dataset trigger hay thường đều được - ray nhắm dùng cả hai).
//
// Cách nhặt: NGẮM qua tâm màn hình + bấm E, giống cách nhặt đồ loot
// (PlayerGrab gọi vào TryPickUpConsumable). Đồ cộng số lượng vào kho
// (stack ×N trên 1 ô hotbar, bấm F để ném/phun như cũ) rồi vật thể tự hủy.
//
// Bảo vệ không mất đồ im lặng:
// - Hotbar KHÔNG còn ô trống cho loại đồ CHƯA CÓ Ô => KHÔNG cộng kho, KHÔNG
//   Destroy (đồ nằm nguyên trên sàn) + báo "Tay mình hết chỗ rồi!"
//   (cooldown 1.5s).
// - Nhặt thành công => toast ngắn tự tắt "Nhặt <tên> (×N)", cooldown theo
//   từng loại đồ để không chồng chữ.
// DialogueManager.ShowLine KHÔNG khóa nhân vật (AcquireControl chỉ ẩn prompt
// + khóa cursor) nên các thông báo không làm game khựng.
public class SaltBagPickup : MonoBehaviour
{
    // Tên vật phẩm - đặt khác nhau cho từng loại, ví dụ "Salt", "Wine"...
    public string itemId = "Salt";

    public int amount = 1;

    // Cooldown chung cho thông báo "hết chỗ" lẫn toast nhặt.
    private const float NotifyCooldownSeconds = 1.5f;

    // Cooldown toast NHẶT theo từng loại đồ (chung giữa các instance).
    private static readonly Dictionary<string, float> nextToastTime =
        new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);

    // Cooldown thông báo "hết chỗ" (dùng chung mọi túi/chai trong scene).
    private static float nextFullHandsTime;

    private ThrowableSelector hotbar;

    // Chặn cộng kho 2 lần nếu cùng frame có nhiều lời gọi (ray + overlap).
    private bool consumed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        nextToastTime.Clear();
        nextFullHandsTime = 0f;
    }

    private void Awake()
    {
        if (hotbar == null) hotbar = FindFirstObjectByType<ThrowableSelector>();
    }

    // PlayerGrab gọi khi người chơi NGẮM món này và bấm E.
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

        // Toast ngắn tự tắt "Nhặt Muối (×3)". Chỉ bỏ THÔNG BÁO khi trùng
        // cooldown / thoại đang mở, không bỏ lần nhặt.
        string canonical = PlayerInventory.CanonicalId(itemId);
        bool toastInCooldown = nextToastTime.TryGetValue(canonical, out float gate)
            && Time.unscaledTime < gate;

        if (!toastInCooldown)
        {
            string ten = hotbar != null ? hotbar.GetDisplayName(itemId) : itemId;
            int soLuong = inventory.GetCount(itemId);
            if (TryShowLine($"Nhặt {ten} (×{soLuong})", "Player"))
                nextToastTime[canonical] = Time.unscaledTime + NotifyCooldownSeconds;
        }

        Destroy(gameObject);
        return true;
    }

    // Hiện 1 dòng thoại ngắn tự tắt qua DialogueManager. Trả về TRUE nếu vừa
    // hiển thị (caller tự đặt cooldown). Bỏ qua khi không có DialogueManager
    // hoặc đang có thoại khác mở (không chèn dòng nhặt vào giữa thoại cốt truyện).
    private static bool TryShowLine(string text, string speaker)
    {
        DialogueManager dlg = DialogueManager.Instance;
        if (dlg == null || dlg.IsActive) return false;

        dlg.ShowLine(text, speaker, autoAdvance: true);
        return true;
    }
}
