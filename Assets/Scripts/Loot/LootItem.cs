using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Gắn lên vật phẩm loot (tag "Item"). Nhặt sẽ tự vào túi đồ của player.
// Ponx: value đơn vị "triệu", size = số ô chiếm trong túi (1..3).
public class LootItem : MonoBehaviour
{
    [Header("Item info")]
    public string itemName = "Đồ cổ";
    [Tooltip("Giá trị (triệu đồng) khi mang về an toàn")]
    public int valueInMillions = 1;
    [Range(1, 3)]
    public int slotCost = 1;

    [Header("Nhận diện khi chất lên xe tải")]
    [Tooltip("Khóa duy nhất của món đồ (ví dụ loot_clock). Để trống thì ô chất đồ từ chối món này.")]
    public string itemId = string.Empty;

    [Header("Collider & vật lý")]
    [Tooltip("Tự căn BoxCollider theo TOÀN BỘ mesh gốc (kể cả ăng-ten) mỗi lần Play → sẽ GHI ĐÈ collider đã chỉnh tay. Mặc định TẮT để giữ size tự chỉnh, ôm sát TV bỏ ăng-ten. Muốn căn lại nhanh: chuột phải component -> Fit Collider.")]
    [SerializeField] private bool autoFitCollider = false;

    /// Khóa nhận diện (TruckLoadZone / PlayerGrab đọc). Rượu/muối không có món này nên không chất được.
    public string ItemId => itemId;

    // Nút chuột phải trên component: căn collider khít model NGAY TRONG EDIT MODE
    // và ghi vào scene (cần Save sau đó). Khác với autofit trong Awake (chỉ runtime).
    [ContextMenu("Fit Collider")]
    private void FitColliderNow()
    {
        AutoFitColliderToModel();
        EditorUtility.SetDirty(gameObject); // đánh dấu scene/prefab cần Save
    }

    private void Awake()
    {
        // Mọi món loot tự có glow khi ngắm + nhãn giá tiền trên đầu.
        if (GetComponent<LootValueLabel>() == null)
            gameObject.AddComponent<LootValueLabel>();

        // Trọng lực để món loot rơi xuống đất thay vì lơ lửng.
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.useGravity = true;
        // Món spawn ở độ cao lớn (VD y ~3.6m): collider nhỏ rơi nhanh dễ xuyên
        // sàn giữa 2 lần FixedUpdate. Continuous để không tunneling.
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Autofit MẶC ĐỊNH TẮT: không đè lên collider đã chỉnh tay trong Editor
        // (ví dụ thu nỏ để ôm sát thân TV, bỏ qua ăng-ten). Chỉ chạy khi tick.
        if (autoFitCollider)
            AutoFitColliderToModel();
    }

    // Căn BoxCollider vừa khít model gốc. Mesh và collider cùng thuộc hệ trục
    // local của object nên lấy local bounds của mesh là khít từng mm (TV đời cũ
    // đứng bằng root xoay -90 cũng đúng: bounds tính theo local mesh, không bị
    // ảnh hưởng rotation). Không có mesh ở root thì quy bounds world về local.
    private void AutoFitColliderToModel()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) box = gameObject.AddComponent<BoxCollider>();

        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds b = mf.sharedMesh.bounds;
            box.center = b.center;
            box.size = b.size;
            return;
        }

        Renderer r = GetComponent<Renderer>();
        if (r == null) r = GetComponentInChildren<Renderer>();
        if (r == null) return;

        Bounds wb = r.bounds;
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = transform.InverseTransformPoint(
                        wb.center + Vector3.Scale(wb.extents, new Vector3(x, y, z)));
                    min = Vector3.Min(min, corner);
                    max = Vector3.Max(max, corner);
                }
        box.center = (min + max) * 0.5f;
        box.size = max - min;
    }

    void Reset()
    {
        // Khi gắn mới trong editor: auto đặt tag Item
        if (!gameObject.CompareTag("Item"))
            gameObject.tag = "Item";
    }
}
