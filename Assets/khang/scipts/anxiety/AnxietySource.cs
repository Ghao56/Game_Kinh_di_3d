using UnityEngine;

public class AnxietySource : MonoBehaviour
{
    public enum Mode { PerSecondInRadius, OneShotOnEnter }

    [SerializeField] Mode mode = Mode.PerSecondInRadius;
    [SerializeField] float amount = 5f;              // mỗi giây, hoặc 1 lần tùy mode
    [SerializeField] float radius = 10f;
    [SerializeField] AnimationCurve falloff = AnimationCurve.Linear(0, 1, 1, 0); // x = khoảng cách/radius
    [SerializeField] bool repeatOneShot = false;       // ra khỏi vùng rồi vào lại thì cộng tiếp

    [Header("Âm thanh")]
    [SerializeField] bool requireAudioPlaying = false; // chỉ tính khi nhạc đang phát
    [Tooltip("Kéo đúng AudioSource đang phát nhạc vào đây. Để trống = tự tìm trên object và object con")]
    [SerializeField] AudioSource audioSrc;

    [Header("Gỡ lỗi")]
    [SerializeField] bool debugLog = false;            // in trạng thái mỗi giây ra Console

    bool triggered;
    bool externalPlaying;
    float nextLog;

    /// Script phát nhạc gọi hàm này để báo trực tiếp (dùng khi nhạc phát bằng PlayOneShot,
    /// vì isPlaying luôn là false với PlayOneShot).
    public void SetPlaying(bool value) => externalPlaying = value;

    bool IsPlaying => externalPlaying || (audioSrc != null && audioSrc.isPlaying);

    void Awake()
    {
        if (audioSrc == null) audioSrc = GetComponentInChildren<AudioSource>();
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
            Debug.Log($"[AnxietySource] {name}: dist={dist:F1}/{radius}, playing={playing}, " +
                      $"audioSrc={(audioSrc ? audioSrc.name : "NULL")}, require={requireAudioPlaying}", this);
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