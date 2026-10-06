using UnityEngine;

// Gắn script này vào object Player.
// Không còn tự nghe phím bấm nữa - ThrowableSelector sẽ gọi TryThrow() khi cần.
public class SaltBagThrower : MonoBehaviour, IThrowable
{
    public string displayName = "Muối";
    public string DisplayName => displayName;

    public PlayerInventory inventory;

    public string itemId = "Salt";

    // Prefab viên đạn sẽ bay ra (cần có Rigidbody + Collider + script SaltBagProjectile)
    public GameObject projectilePrefab;

    // Vị trí xuất phát khi ném (ví dụ object gắn ở tay player)
    public Transform throwPoint;

    // Camera để lấy hướng ném. Nếu để trống, script tự lấy Camera.main lúc Start.
    public Transform cameraTransform;

    public float throwForce = 15f;

    void Start()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    public bool TryThrow()
    {
        if (inventory == null || projectilePrefab == null || throwPoint == null)
        {
            Debug.LogWarning($"SaltBagThrower ({itemId}) thiếu tham chiếu (inventory/prefab/throwPoint)!");
            return false;
        }

        if (!inventory.UseItem(itemId))
        {
            Debug.Log($"Hết {itemId}, không thể ném!");
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

        Debug.Log($"Đã ném {itemId}, còn lại: {inventory.GetCount(itemId)}");

        return true;
    }

    public int GetCount()
    {
        return inventory != null ? inventory.GetCount(itemId) : 0;
    }
}