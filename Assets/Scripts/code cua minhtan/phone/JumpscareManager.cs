using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class JumpscareManager : MonoBehaviour
{
    public static JumpscareManager Instance;

    [Header("Cài đặt Jumpscare")]
    [SerializeField] private GameObject jumpscarePanelPrefab;
    [SerializeField] private AudioClip jumpscareSound;
    [SerializeField] private float displayDuration = 1.5f;

    private GameObject spawnedPanelInstance;
    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

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
        CreatePanelInstance();
    }

    private void CreatePanelInstance()
    {
        if (jumpscarePanelPrefab != null && spawnedPanelInstance == null)
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            Transform parentTransform = canvas != null ? canvas.transform : null;

            spawnedPanelInstance = Instantiate(jumpscarePanelPrefab, parentTransform);

            RectTransform rect = spawnedPanelInstance.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            spawnedPanelInstance.SetActive(false);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (spawnedPanelInstance != null)
        {
            Destroy(spawnedPanelInstance);
            spawnedPanelInstance = null;
        }

        CreatePanelInstance();
    }

    // Đã xóa điều kiện kiểm tra isTriggered để có thể gọi vô hạn số lần
    public void TriggerJumpscare()
    {
        StartCoroutine(JumpscareRoutine());
    }

    private IEnumerator JumpscareRoutine()
    {
        float previousTimeScale = Time.timeScale;

        if (spawnedPanelInstance != null)
            spawnedPanelInstance.SetActive(true);
        else
            CreatePanelInstance();

        if (jumpscareSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(jumpscareSound);
        }

        Time.timeScale = 0f;

        float timer = 0f;
        while (timer < displayDuration)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}