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

        if (jumpscarePanel != null)
            jumpscarePanel.SetActive(false);
        else
            Debug.LogWarning($"{nameof(JumpscareManager)}: jumpscarePanel đang NULL sau khi load scene. " +
                "Nếu panel nằm trong Canvas của scene (không phải object persistent), tham chiếu này sẽ mất sau mỗi lần reload. " +
                "Hãy đặt JumpscarePanel làm con của cùng object persistent với JumpscareManager.", this);
    }

    public void TriggerJumpscare()
    {
        if (isTriggered) return;
        isTriggered = true;

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