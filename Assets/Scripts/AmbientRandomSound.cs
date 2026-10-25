using UnityEngine;

/// <summary>
/// Cứ mỗi khoảng thời gian ngẫu nhiên (mặc định tối đa 3 phút) thì phát ngẫu nhiên 1 âm thanh trong mảng,
/// sau đó đặt hẹn giờ mới và lặp lại. Có thể giới hạn theo mức lo âu.
/// </summary>
public class AmbientRandomSound : MonoBehaviour
{
    [Header("Âm thanh")]
    [Tooltip("Thêm bao nhiêu âm thanh cũng được, mỗi lần sẽ chọn ngẫu nhiên 1 cái")]
    [SerializeField] AudioClip[] clips;
    [SerializeField] AudioSource audioSource;           // để trống = tự thêm AudioSource vào object này
    [SerializeField, Range(0f, 1f)] float volume = 1f;
    [SerializeField] bool avoidRepeat = true;           // không phát lại đúng âm vừa phát (khi có từ 2 âm trở lên)

    [Header("Thời gian (giây)")]
    [SerializeField] float minInterval = 30f;           // tối thiểu giữa 2 lần phát
    [SerializeField] float maxInterval = 180f;          // tối đa = 3 phút, tự chỉnh được


    [Header("Điều kiện phát")]
    [SerializeField] bool useAnxietyCondition = false;  // chỉ phát khi lo âu nằm trong khoảng bên dưới
    [SerializeField, Range(0, 100)] float minAnxiety = 0f;
    [SerializeField, Range(0, 100)] float maxAnxiety = 100f;
    [SerializeField] bool waitUntilFinished = true;     // chưa phát xong âm trước thì chưa đếm tiếp

    [Header("Vị trí phát")]
    [SerializeField] bool playAroundPlayer = false;     // true: âm vang lên từ hướng ngẫu nhiên quanh người chơi
    [SerializeField] float minDistance = 5f;
    [SerializeField] float maxDistance = 15f;

    [Header("Ảnh hưởng lo âu")]
    [SerializeField] float anxietyOnPlay = 5f;          // cộng lo âu mỗi lần phát (0 = không cộng)
    [Tooltip("Tùy chọn: báo cho AnxietySource biết đang phát nhạc")]
    [SerializeField] AnxietySource anxietySource;

    [Header("Gỡ lỗi")]
    [SerializeField] bool debugLog = false;

    float nextPlayTime;
    int lastIndex = -1;
    bool wasPlaying;

    void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    void Start()
    {
        if (clips == null || clips.Length == 0)
            Debug.LogWarning("AmbientRandomSound: chưa có âm thanh nào trong mảng Clips.", this);
        if (maxInterval < minInterval)
            Debug.LogWarning("AmbientRandomSound: Max Interval nhỏ hơn Min Interval, sẽ dùng Min Interval.", this);
        if (playAroundPlayer && (AnxietyManager.Instance == null || AnxietyManager.Instance.Player == null))
            Debug.LogWarning("AmbientRandomSound: bật Play Around Player nhưng không tìm thấy Player.", this);
        if (useAnxietyCondition && AnxietyManager.Instance == null)
            Debug.LogWarning("AmbientRandomSound: không tìm thấy AnxietyManager nên điều kiện lo âu không hoạt động.", this);

        ScheduleNext();
    }

    void Update()
    {
        if (clips == null || clips.Length == 0) return;

        // Báo trạng thái phát cho AnxietySource (nếu có)
        bool playing = audioSource.isPlaying;
        if (anxietySource != null && playing != wasPlaying)
            anxietySource.SetPlaying(playing);
        wasPlaying = playing;

        if (waitUntilFinished && playing) return;
        if (Time.time < nextPlayTime) return;

        // Đến giờ nhưng chưa đủ điều kiện: kiểm tra lại sau 1 giây
        if (!ConditionMet())
        {
            nextPlayTime = Time.time + 1f;
            return;
        }

        PlayRandom();
        ScheduleNext();
    }

    bool ConditionMet()
    {
        if (!useAnxietyCondition) return true;
        if (AnxietyManager.Instance == null) return false;

        float c = AnxietyManager.Instance.Current;
        return c >= minAnxiety && c <= maxAnxiety;
    }

    void PlayRandom()
    {
        int index = Random.Range(0, clips.Length);
        if (avoidRepeat && clips.Length > 1)
            while (index == lastIndex) index = Random.Range(0, clips.Length);
        lastIndex = index;

        AudioClip clip = clips[index];
        if (clip == null)
        {
            Debug.LogWarning($"AmbientRandomSound: phần tử {index} trong mảng Clips đang trống.", this);
            return;
        }

        if (playAroundPlayer && AnxietyManager.Instance != null && AnxietyManager.Instance.Player != null)
        {
            float a = Random.value * Mathf.PI * 2f;
            float d = Random.Range(minDistance, maxDistance);
            Vector3 offset = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * d;
            transform.position = AnxietyManager.Instance.Player.position + offset;
            audioSource.spatialBlend = 1f;
        }

        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.Play();

        if (anxietyOnPlay != 0f && AnxietyManager.Instance != null)
            AnxietyManager.Instance.Add(anxietyOnPlay);

        if (debugLog) Debug.Log($"[AmbientSound] phát '{clip.name}'", this);
    }

    void ScheduleNext()
    {
        float wait = Random.Range(minInterval, Mathf.Max(minInterval, maxInterval));
        nextPlayTime = Time.time + wait;
        if (debugLog) Debug.Log($"[AmbientSound] lần phát tiếp theo sau {wait:F0}s", this);
    }

    [ContextMenu("Test: phát ngay")]
    void TestPlay() => PlayRandom();
}