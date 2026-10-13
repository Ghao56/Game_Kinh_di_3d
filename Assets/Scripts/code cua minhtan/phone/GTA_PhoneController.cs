using UnityEngine;
using UnityEngine.InputSystem;

public class GTA_PhoneController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform phoneRect;
    [SerializeField] private GameObject messageScreen;
    [SerializeField] private CanvasGroup canvasGroup; // tùy chọn, chặn click khi ẩn

    [Header("Khóa di chuyển & xoay Camera khi mở điện thoại")]
    [Tooltip("Kéo TẤT CẢ script liên quan đến xoay camera, di chuyển nhân vật vào đây (có thể kéo nhiều cái)")]
    [SerializeField] private Behaviour[] scriptsToDisableWhenPhoneOpen;

    [Header("Settings")]
    [SerializeField] private Key openKey = Key.F;
    [SerializeField] private Key messageKey = Key.T;
    [SerializeField] private float speed = 10f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Positions (anchoredPosition)")]
    [SerializeField] private Vector2 hiddenPos = new Vector2(-150, -600);
    [SerializeField] private Vector2 visiblePos = new Vector2(-150, 400);

    private bool isOpen;
    private bool isAnimating;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (phoneRect == null)
            phoneRect = GetComponent<RectTransform>();

        if (phoneRect == null)
        {
            Debug.LogError($"{nameof(GTA_PhoneController)}: chưa gán phoneRect!", this);
            enabled = false;
            return;
        }

        if (canvasGroup == null)
            TryGetComponent(out canvasGroup);

        if (messageScreen == null)
            Debug.LogWarning($"{nameof(GTA_PhoneController)}: chưa gán messageScreen, phím T sẽ không có tác dụng.", this);

        if (scriptsToDisableWhenPhoneOpen == null || scriptsToDisableWhenPhoneOpen.Length == 0)
            Debug.LogWarning($"{nameof(GTA_PhoneController)}: chưa gán scriptsToDisableWhenPhoneOpen, camera/player sẽ KHÔNG bị khóa khi mở điện thoại.", this);
    }

    private void Start()
    {
        phoneRect.anchoredPosition = hiddenPos;
        ApplyInteractable(false);
        if (messageScreen != null)
            messageScreen.SetActive(false);

        // Lúc mới vào game: khóa và ẩn con trỏ chuột để xoay góc nhìn 3D
        SetCursorState(false);
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Bấm F: bật / tắt điện thoại
        if (keyboard[openKey].wasPressedThisFrame)
            TogglePhone();

        // Bấm T: bật / tắt màn hình tin nhắn (chỉ khi điện thoại đang mở)
        if (keyboard[messageKey].wasPressedThisFrame && isOpen && messageScreen != null)
            messageScreen.SetActive(!messageScreen.activeSelf);

        if (!isAnimating) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float t = 1f - Mathf.Exp(-speed * dt); // độc lập framerate, không vượt quá 1

        Vector2 target = isOpen ? visiblePos : hiddenPos;
        Vector2 pos = Vector2.Lerp(phoneRect.anchoredPosition, target, t);

        if ((pos - target).sqrMagnitude < 0.01f)
        {
            pos = target;
            isAnimating = false;
            if (!isOpen) ApplyInteractable(false);
        }

        phoneRect.anchoredPosition = pos;
    }

    public void TogglePhone() => SetOpen(!isOpen);
    public void OpenPhone() => SetOpen(true);
    public void ClosePhone() => SetOpen(false);

    public void SetOpen(bool open)
    {
        isOpen = open;
        isAnimating = true;

        SetCursorState(isOpen);

        if (open)
        {
            ApplyInteractable(true);
        }
        else if (messageScreen != null)
        {
            messageScreen.SetActive(false);
        }
    }

    private void SetCursorState(bool phoneIsOpen)
    {
        if (phoneIsOpen)
        {
            // MỞ ĐIỆN THOẠI: hiện con trỏ chuột, thả tự do để bấm UI
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // ĐÓNG ĐIỆN THOẠI: khóa con trỏ vào giữa màn hình để xoay camera
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Khóa/mở TẤT CẢ script trong danh sách cùng lúc
        if (scriptsToDisableWhenPhoneOpen != null)
        {
            foreach (var script in scriptsToDisableWhenPhoneOpen)
            {
                if (script != null)
                    script.enabled = !phoneIsOpen;
            }
        }
    }

    private void ApplyInteractable(bool value)
    {
        if (canvasGroup == null) return;
        canvasGroup.interactable = value;
        canvasGroup.blocksRaycasts = value;
    }
}