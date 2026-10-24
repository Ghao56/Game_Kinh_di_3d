using System.Collections.Generic;
using UnityEngine;

// Gắn script này vào bất kỳ vật phẩm nào đặt trong scene để nhặt được
// (túi muối, chai rượu, vật phẩm tương lai...). Object cần có Collider
// với "Is Trigger" = true.
//
// Vẫn giữ "chạm là nhặt", nhưng có 2 lớp bảo vệ để không mất đồ im lặng:
// 1) Hotbar KHÔNG còn ô trống cho loại đồ CHƯA CÓ Ô => KHÔNG cộng kho, KHÔNG
//    Destroy (đồ nằm nguyên trên sàn) + báo thoại "Tay mình hết chỗ rồi!"
//    (cooldown 1.5s, dùng DialogueManager sẵn có). Dùng OnTriggerStay thay
//    OnTriggerEnter: người chơi giải phóng một ô rồi ĐỨNG YÊN trong vùng vẫn
//    tự nhặt lại được, không cần bước ra rồi vào lại.
// 2) Nhặt thành công => hiện 1 dòng thoại ngắn tự tắt kiểu toast:
//    "Nhặt <tên> (×số lượng)" - tên đồng bộ với hotbar qua GetDisplayName,
//    cooldown theo từng loại đồ để đứng giữa đống chai không bị chồng chữ.
// DialogueManager.ShowLine KHÔNG khóa nhân vật (AcquireControl chỉ ẩn prompt
// + khóa cursor) nên thông báo không làm game khựng.
public class SaltBagPickup : MonoBehaviour
{
    // Tên vật phẩm - đặt khác nhau cho từng loại, ví dụ "Salt", "Wine"...
    public string itemId = "Salt";

    public int amount = 1;

    // Cooldown chung cho thông báo "hết chỗ" lẫn toast nhặt.
    private const float NotifyCooldownSeconds = 1.5f;

    // Cooldown toast NHẶT theo từng loại đồ (chung giữa các instance để đứng
    // giữa nhiều chai trong cùng frame không bị chồng chữ).
    private static readonly Dictionary<string, float> nextToastTime =
        new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);

    // Cooldown thông báo "hết chỗ" (dùng chung mọi túi/chai trong scene).
    private static float nextFullHandsTime;

    private ThrowableSelector hotbar;

    // Chặn trường hợp trigger trúng nhiều collider của player trong cùng frame
    // (AddItem 2 lần cho 1 món).
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

    // Dùng OnTriggerStay thay OnTriggerEnter: nếu lần trước bị chặn vì hotbar
    // hết chỗ, người chơi giải phóng một ô rồi đứng yên trong vùng vẫn tự nhặt.
    private void OnTriggerStay(Collider other)
    {
        if (consumed) return;

        PlayerInventory inventory = other.GetComponent<PlayerInventory>();
        if (inventory == null) return;

        if (hotbar == null) hotbar = FindFirstObjectByType<ThrowableSelector>();

        // Hotbar còn nhận thêm loại này không? (đã có stack -> luôn nhận;
        // chưa có stack -> cần 1 ô trống). Scene không có hotbar: giữ hành vi cũ.
        if (hotbar != null && !hotbar.CanAcceptItem(itemId))
        {
            // KHÔNG AddItem, KHÔNG Destroy: đồ nằm nguyên trên sàn.
            if (Time.unscaledTime >= nextFullHandsTime &&
                TryShowLine("Tay mình hết chỗ rồi!", "Player"))
            {
                nextFullHandsTime = Time.unscaledTime + NotifyCooldownSeconds;
            }
            return;
        }

        inventory.AddItem(itemId, amount);
        consumed = true; // chặn callback trigger lặp lại trong cùng frame

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
