using UnityEngine;

// Gắn script này vào bất kỳ vật phẩm nào đặt trong scene để nhặt được
// (túi muối, chai rượu, vật phẩm tương lai...). Object cần có Collider
// với "Is Trigger" = true.
public class SaltBagPickup : MonoBehaviour
{
    // Tên vật phẩm - đặt khác nhau cho từng loại, ví dụ "Salt", "Wine"...
    public string itemId = "Salt";

    public int amount = 1;

    void OnTriggerEnter(Collider other)
    {
        PlayerInventory inventory = other.GetComponent<PlayerInventory>();

        if (inventory != null)
        {
            inventory.AddItem(itemId, amount);

            Destroy(gameObject);
        }
    }
}