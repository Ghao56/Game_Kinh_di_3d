using UnityEngine;

public class AnxietySource : MonoBehaviour
{
    public enum Mode { PerSecondInRadius, OneShotOnEnter }

    [SerializeField] Mode mode = Mode.PerSecondInRadius;
    [Header("lo au moi giay")]
    [SerializeField] public float amount = 1f;              // mỗi giây, hoặc 1 lần tùy mode
    [SerializeField] float radius = 10f;
    [SerializeField] AnimationCurve falloff = AnimationCurve.Linear(0, 1, 1, 0); // x = khoảng cách/radius
    [SerializeField] bool repeatOneShot = false;       // ra khỏi vùng rồi vào lại thì cộng tiếp

    [Header("Âm thanh")]
    [SerializeField] bool requireAudioPlaying = false; // chỉ tính khi có ít nhất 1 nhạc đang phát
    [Tooltip("Danh sách AudioSource. Chỉ cần 1 cái đang phát là tính. Để trống = tự lấy tất cả AudioSource trên object này và object con")]
    [SerializeField] AudioSource[] audioSources;

    [Header("Gỡ lỗi")]
    [SerializeField] bool debugLog = false;            // in trạng thái mỗi giây ra Console

    bool triggered;
    bool externalPlaying;
    float nextLog;

    /// Script phát nhạc gọi hàm này để báo trực tiếp (dùng khi nhạc phát bằng PlayOneShot,
    /// vì isPlaying luôn là false với PlayOneShot).
    public void SetPlaying(bool value) => externalPlaying = value;

    /// Có ít nhất một AudioSource trong mảng đang phát (hoặc được báo bằng SetPlaying)
    bool IsPlaying
    {
        get
        {
            if (externalPlaying) return true;
            if (audioSources == null) return false;

            for (int i = 0; i < audioSources.Length; i++)
                if (audioSources[i] != null && audioSources[i].isPlaying) return true;

            return false;
        }
    }

    int PlayingCount()
    {
        int n = 0;
        if (audioSources == null) return 0;
        foreach (var a in audioSources) if (a != null && a.isPlaying) n++;
        return n;
    }

    void Awake()
    {
        // Mảng trống thì tự lấy tất cả AudioSource trên object và object con
        if (audioSources == null || audioSources.Length == 0)
            audioSources = GetComponentsInChildren<AudioSource>();
    }

    void OnEnable() { if (AnxietyManager.Instance) AnxietyManager.Instance.Register(this); }
    void OnDisable() { if (AnxietyManager.Instance) AnxietyManager.Instance.Unregister(this); }
    void Start() { if (AnxietyManager.Instance) AnxietyManager.Instance.Register(this); }

    public float Evaluate(Vector3 playerPos, float dt)
    {
        float dist = Vector3.Distance(transform.position, playerPos);
        bool playing = IsPlaying;

        if (debugLog && Time.time >= nextLog)
        {
            nextLog = Time.time + 1f;
            int total = audioSources != null ? audioSources.Length : 0;
            Debug.Log($"[AnxietySource] {name}: dist={dist:F1}/{radius}, playing={playing} " +
                      $"({PlayingCount()}/{total} nguồn phát), require={requireAudioPlaying}", this);
        }

        if (requireAudioPlaying && !playing) return 0f;

        if (dist > radius)
        {
            if (repeatOneShot) triggered = false;
            return 0f;
        }

        float k = falloff.Evaluate(dist / radius);

        if (mode == Mode.PerSecondInRadius) return amount * k * dt;

        if (triggered) return 0f;
        triggered = true;
        return amount * k;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}