using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class JumpscareManager : MonoBehaviour
{
    public static JumpscareManager Instance;

    [Header("Cài đặt Jumpscare")]
    [SerializeField] private GameObject jumpscarePanel;
    [SerializeField] private AudioClip jumpscareSound;
    [SerializeField] private float displayDuration = 1.5f;

    private AudioSource audioSource;
    private bool isTriggered = false;

    // Panel dựng tạm khi scene không có JumpscarePanel (MHoang): panel scene
    // THẬT khi xuất hiện sẽ được ưu tiên dùng lại (xem OnSceneLoaded).
    private GameObject fallbackPanel;
    private bool usingFallback;

    // Auto-bootstrap: scene chapter (MHoang) không đặt JumpscareManager sẵn như
    // MainGame — tạo bản persistent để thua khi bị bắt hoạt động ở MỌI scene.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return; // bản DDOL từ scene trước còn sống
        if (FindFirstObjectByType<JumpscareManager>() != null) return; // scene tự đặt sẵn

        new GameObject("JumpscareManager (auto)").AddComponent<JumpscareManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Chỉ gọi DontDestroyOnLoad nếu đây là object GỐC (root).
        // Nếu object này đã là con của một object persistent khác có sẵn,
        // Unity sẽ tự giữ nó sống theo object cha, gọi lại ở đây sẽ bị cảnh báo và không có tác dụng.
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        if (jumpscarePanel != null)
            jumpscarePanel.SetActive(false);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Quan trọng: reset trạng thái mỗi khi scene được load lại,
        // để jumpscare có thể kích hoạt lại ở lần chơi tiếp theo.
        // (Scene additive không phải lượt chơi mới — đừng reset oan.)
        if (mode != LoadSceneMode.Additive) isTriggered = false;

        // Panel scene THẬT ("JumpscarePanel") luôn thắng panel dựng tạm: nếu
        // manager DDOL đang dùng fallback (MHoang) mà rời sang MainGame có sẵn
        // panel, gỡ fallback và vẫn dùng panel thật.
        RebindJumpscarePanel();

        if (jumpscarePanel != null)
            jumpscarePanel.SetActive(false);
    }

    private void RebindJumpscarePanel()
    {
        // Panel scene nằm trong Canvas của scene, bị phá hủy sau mỗi lần load —
        // manager persistent nên bắt buộc tìm lại trong scene mới.
        GameObject scenePanel = FindScenePanel();
        if (scenePanel != null)
        {
            usingFallback = false;
            if (fallbackPanel != null) Destroy(fallbackPanel);
            fallbackPanel = null;
            jumpscarePanel = scenePanel;
            return;
        }

        // Scene không có panel (MHoang...): dựng fallback nếu chưa có.
        if (usingFallback && fallbackPanel != null)
        {
            jumpscarePanel = fallbackPanel;
            return;
        }

        EnsureFallbackPanel();
        jumpscarePanel = fallbackPanel;
        usingFallback = true;
    }

    private static GameObject FindScenePanel()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (RectTransform child in root.transform.GetComponentsInChildren<RectTransform>(true))
            {
                if (child.name == "JumpscarePanel") return child.gameObject;
            }
        }

        return null;
    }

    // Panel dự phòng dựng bằng code (cùng stack Canvas overlay với EndScreenUI):
    // nền đỏ sẫm đủ để báo người chơi biết mình vừa bị bắt khi scene không
    // có panel scare image.
    private void EnsureFallbackPanel()
    {
        if (fallbackPanel != null) return;

        GameObject go = new GameObject("JumpscarePanel (fallback)", typeof(Canvas), typeof(CanvasGroup));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2500; // đè HUD, thua EndScreen (3000)

        CanvasGroup group = go.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        UnityEngine.UI.Image image = go.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.18f, 0.0f, 0.0f, 1f);
        image.raycastTarget = false;

        go.SetActive(false);
        fallbackPanel = go;
    }

    public void TriggerJumpscare()
    {
        if (isTriggered) return;
        isTriggered = true;

        Debug.LogWarning($"[Watchdog] JUMPSCARE kích hoạt — game sẽ đứng hình {displayDuration}s rồi load lại scene.", this);

        StartCoroutine(JumpscareRoutine());
    }

    private IEnumerator JumpscareRoutine()
    {
        float previousTimeScale = Time.timeScale;

        if (jumpscarePanel != null)
            jumpscarePanel.SetActive(true);

        if (jumpscareSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(jumpscareSound);
        }

        Time.timeScale = 0f;

        float timer = 0f;
        while (timer < displayDuration)
        {
            timer += Time.unscaledDeltaTime; // đã sửa: Time.unscaledDeltaTime thay vì Mathf.UnscaledDeltaTime
            yield return null;
        }

        // Khôi phục đúng tốc độ thời gian trước đó (phòng trường hợp game đang pause từ hệ thống khác)
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}