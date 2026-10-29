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
        isTriggered = false;

        // Panel nằm trong Canvas của scene nên bị phá hủy sau mỗi lần reset,
        // còn manager thì persistent -> bắt buộc phải tìm lại panel trong scene mới.
        RebindJumpscarePanel();

        if (jumpscarePanel != null)
            jumpscarePanel.SetActive(false);
        else
            Debug.LogWarning($"{nameof(JumpscareManager)}: jumpscarePanel đang NULL sau khi load scene. " +
                "Hãy chắc chắn scene có một object tên 'JumpscarePanel' (cả khi bị ẩn).", this);
    }

    private void RebindJumpscarePanel()
    {
        // Tham chiếu cũ trỏ vào object đã bị hủy (Unity fake-null) -> tìm lại.
        if (jumpscarePanel != null) return;

        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (RectTransform child in root.transform.GetComponentsInChildren<RectTransform>(true))
            {
                if (child.name == "JumpscarePanel")
                {
                    jumpscarePanel = child.gameObject;
                    return;
                }
            }
        }
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