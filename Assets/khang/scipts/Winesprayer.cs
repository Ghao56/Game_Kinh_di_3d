using UnityEngine;

// Script RIÊNG cho việc phun rượu - tách biệt hoàn toàn với SaltBagThrower
// để sau này dễ thêm các hiệu ứng đặc thù cho rượu (khói, lửa, hiệu ứng khác...)
// mà không ảnh hưởng gì tới muối.
public class WineSprayer : MonoBehaviour, IThrowable
{
    public string displayName = "Chai rượu";
    public string DisplayName => displayName;

    public PlayerInventory inventory;

    public string itemId = "Wine";

    // Prefab giọt/chai rượu bay ra (cần có Rigidbody + Collider + script WineProjectile)
    public GameObject projectilePrefab;

    public Transform throwPoint;

    public Transform cameraTransform;

    public float throwForce = 12f;

    void Start()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    public bool TryThrow()
    {
        if (inventory == null || projectilePrefab == null || throwPoint == null)
        {
            Debug.LogWarning($"WineSprayer ({itemId}) thiếu tham chiếu (inventory/prefab/throwPoint)!");
            return false;
        }

        if (!inventory.UseItem(itemId))
        {
            Debug.Log($"Hết {itemId}, không thể phun!");
            return false;
        }

        Vector3 throwDirection =
            cameraTransform != null ? cameraTransform.forward : throwPoint.forward;

        GameObject projectile = Instantiate(
            projectilePrefab,
            throwPoint.position,
            Quaternion.LookRotation(throwDirection)
        );

        Collider projectileCollider = projectile.GetComponent<Collider>();
        Collider[] playerColliders = GetComponentsInChildren<Collider>();

        if (projectileCollider != null)
        {
            foreach (Collider col in playerColliders)
            {
                Physics.IgnoreCollision(projectileCollider, col);
            }
        }

        Rigidbody rb = projectile.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.AddForce(throwDirection * throwForce, ForceMode.VelocityChange);
        }
        else
        {
            Debug.LogWarning($"Prefab {itemId} không có Rigidbody nên sẽ không bay!");
        }

        Debug.Log($"Đã phun {itemId}, còn lại: {inventory.GetCount(itemId)}");

        return true;
    }

    public int GetCount()
    {
        return inventory != null ? inventory.GetCount(itemId) : 0;
    }
}