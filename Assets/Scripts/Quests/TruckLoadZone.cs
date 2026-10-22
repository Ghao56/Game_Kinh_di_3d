using UnityEngine;

// Vùng chất đồ CHỈNH SỬA ĐƯỢC trên sàn: tạo một Cube, kéo to/vị trí thoải mái, thêm component
// này rồi kéo ô chất đồ (object có script TruckLoadCell) vào trường "Cell".
// Cube tự bật isTrigger + tự gắn material phát sáng nhấp nháy khi cửa xe mở và còn ô trống.
// Player đứng trong vùng => prompt "Giữ E để chất đồ lên xe" hiện, không cần nhìn vào ô.
// Có vùng tùy chỉnh này thì quad tự sinh "VungChatDoTrenXe" của các ô sẽ tự ẩn.
[RequireComponent(typeof(Collider))]
public class TruckLoadZone : MonoBehaviour, IInteractable, IInteractableAvailability, IProximityInteractable
{
    // Số vùng tùy chỉnh đang bật trong scene: >0 thì các ô không sinh quad fallback.
    public static int ActiveCount { get; private set; }

    [Header("Tham chiếu")]
    [Tooltip("Object có script TruckLoadCell (ô chất đồ) mà vùng này phục vụ.")]
    [SerializeField] private TruckLoadCell cell;

    [Header("Cài đặt")]
    [Tooltip("Màu phát sáng của vùng (hỗ trợ khi chưa tự gán material khác).")]
    [SerializeField] private Color zoneColor = new Color(0f, 1f, 0.3f, 0.55f);

    private Collider zoneCollider;
    private Renderer zoneRenderer;
    private Material zoneMaterial;

    // Latch chống chớp: chỉ tắt sau khi điều kiện sai liên tục holdOffSeconds.
    private const float HoldOffSeconds = 0.4f;
    private float offTimer;
    private bool shown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => ActiveCount = 0;

    // ------------------------------------------------------------ IInteractable (ủi thẳng cho ô)

    public string PromptText => cell != null ? cell.PromptText : string.Empty;
    public bool HoldToInteract => true;
    public float HoldDuration => cell != null ? cell.HoldDuration : 0f;

    public bool IsAvailable
    {
        get
        {
            if (cell == null) return false;
            return cell.IsAvailable && PlayerInside();
        }
    }

    // Player ĐỨNG trong vùng là đủ điều kiện prompt (Interactor nhặt fallback theo interface này).
    public bool ReadyForProximityPrompt => IsAvailable;

    public void OnInteract(Interactor interactor)
    {
        if (cell != null) cell.OnInteract(interactor);
    }

    // ------------------------------------------------------------ vùng / hiển thị

    // Player đứng trong cube này: nới biên 0.3m cho dễ đứng tròn.
    public bool PlayerInside()
    {
        if (zoneCollider == null) return false;

        Bounds bounds = zoneCollider.bounds;
        bounds.Expand(0.3f);

        PlayerGrab grab = FindFirstObjectByType<PlayerGrab>();
        return grab != null && bounds.Contains(grab.transform.position);
    }

    private void Awake()
    {
        // Không kéo cell cũng được: tự tìm ô chất đồ đầu tiên trong scene (chủ yếu chỉ có MỘT xe).
        if (cell == null) cell = FindFirstObjectByType<TruckLoadCell>();

        zoneCollider = GetComponent<Collider>();
        if (zoneCollider != null && !zoneCollider.isTrigger)
            zoneCollider.isTrigger = true; // vùng chỉ để phát hiện player đứng trong, không cản di chuyển

        zoneRenderer = GetComponent<Renderer>();
        if (zoneMaterial == null)
        {
            zoneMaterial = new Material(Shader.Find("Sprites/Default")) { color = zoneColor };
            if (zoneRenderer != null) zoneRenderer.sharedMaterial = zoneMaterial;
        }
        if (zoneRenderer != null) zoneRenderer.enabled = false;
    }

    private void OnEnable() => ActiveCount++;
    private void OnDisable() => ActiveCount--;

    private void Update()
    {
        if (cell == null) cell = FindFirstObjectByType<TruckLoadCell>();
        if (zoneRenderer == null || zoneMaterial == null) return; // Awake chưa chạy (component mới được thêm vào object OFF)

        if (cell == null)
        {
            if (shown) { shown = false; zoneRenderer.enabled = false; }
            return;
        }

        if (cell.ZoneReady)
        {
            offTimer = 0f;
            shown = true;
        }
        else
        {
            offTimer += Time.deltaTime;
            if (offTimer >= HoldOffSeconds) shown = false;
        }

        if (zoneRenderer != null && zoneRenderer.enabled != shown)
        {
            zoneRenderer.enabled = shown;
            Debug.Log($"[TruckLoadZone] '{name}' SHOWN = {shown} (doorOpen = {cell.DoorOpenNow}, allFilled = {cell.AllCellsFilled}, cell = '{cell.name}')", this);
        }

        if (!shown) return;

        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 3f);
        Color c = zoneColor;
        c.r *= pulse; c.g *= pulse; c.b *= pulse;
        zoneMaterial.color = c;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.3f, 0.6f);
        if (zoneCollider != null) Gizmos.DrawCube(zoneCollider.bounds.center, zoneCollider.bounds.size);
    }
}
