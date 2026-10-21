using UnityEngine;

// Vùng kích hoạt lớn tính tiền: player đi vào mang đồ => tiền được cộng, túi trống.
// Gắn collider mới trên một object "Điểm an toàn", bật isTrigger, kích thước khoảng 3x3x3.
public class LootSafeZone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        var bag = PlayerLootBag.Instance;
        if (bag == null || !other.CompareTag("Player")) return;

        int payout = bag.CashOut();
        if (payout > 0)
            Debug.Log($"Về an toàn! Nhận {payout} triệu. Tổng: {bag.BankedMillions} triệu.");
    }
}
