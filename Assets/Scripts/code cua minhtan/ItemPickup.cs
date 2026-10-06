using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    [Header("Loại vật phẩm")]
    public string itemName = "Coin";
    public int value = 1;

    // Hàm 3D: Tự động chạy khi có 1 Collider khác đi vào vùng Trigger
    private void OnTriggerEnter(Collider other)
    {
        // Kiểm tra xem đối tượng đi vào vùng này có Tag là "Player" không
        if (other.CompareTag("Player"))
        {
            NhatVatPham(other.gameObject);
        }
    }

    private void NhatVatPham(GameObject player)
    {
        Debug.Log("Đã nhặt: " + itemName);

        // Gọi script túi đồ trên người Player để cộng tiền/máu
        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory != null)
        {
            inventory.AddCoin(value);
        }

        // Tạo hiệu ứng hạt (Particle System) hoặc âm thanh tại đây nếu muốn
        // Instantiate(pickupEffect, transform.position, transform.rotation);

        // Hủy (xóa) vật phẩm khỏi Scene
        Destroy(gameObject);
    }
}