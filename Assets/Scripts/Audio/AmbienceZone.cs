using UnityEngine;

/// Vùng trigger: bước vào thì crossfade sang ambience riêng của vùng,
/// rời ra thì trả về ambience mặc định. Nhận diện người chơi giống QuestZone.
///
/// Lưu ý: hiện chưa xử lý vùng lồng nhau — rời vùng trong khi đang ở vùng ngoài
/// sẽ trả về ambience mặc định. Cần stack zone thì xử lý ở phase sau.
[RequireComponent(typeof(Collider))]
public class AmbienceZone : MonoBehaviour
{
    [Header("Nguồn âm thanh")]
    [Tooltip("Ưu tiên hơn clip gán trực tiếp bên dưới.")]
    [SerializeField] private string ambienceKey;

    [SerializeField] private AudioClip ambienceClip;

    [Header("Chuyển động")]
    [SerializeField] [Min(0f)] private float fadeInTime = 2f;
    [SerializeField] [Min(0f)] private float fadeOutTime = 2.5f;

    [Header("Khi rời vùng")]
    [Tooltip("Trả về clip này khi rời vùng. Để trống = trả về ambience mặc định của AudioManager.")]
    [SerializeField] private AudioClip restoreClip;

    [Tooltip("Âm 1 = dùng fade mặc định của AudioManager.")]
    [SerializeField] private float restoreFadeTime = -1f;

    [Tooltip("Chạy một lần rồi khoá vùng.")]
    [SerializeField] private bool fireOnce;

    [Header("Debug")]
    [SerializeField] private bool logTransitions;

    private bool playerInside;
    private bool fired;

    private void Awake()
    {
        Collider zoneCollider = GetComponent<Collider>();
        if (zoneCollider != null && !zoneCollider.isTrigger)
            Debug.LogWarning($"[{nameof(AmbienceZone)}] Collider chưa bật Is Trigger, zone sẽ không nhận OnTriggerEnter.", this);
    }

    private void Reset()
    {
        Collider zoneCollider = GetComponent<Collider>();
        if (zoneCollider != null) zoneCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;

        playerInside = true;
        Enter();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;

        playerInside = false;
        Exit();
    }

    private void Enter()
    {
        if (fireOnce && fired) return;

        AudioManager manager = AudioManager.Instance;

        if (manager == null)
        {
            Debug.LogError($"[{nameof(AmbienceZone)}] Không có AudioManager trong scene.", this);
            return;
        }

        if (ambienceClip != null)
        {
            manager.SetAmbience(ambienceClip, fadeInTime);
            fired = true;

            if (logTransitions)
                Debug.Log($"[{nameof(AmbienceZone)}] Vào '{name}' -> crossfade '{ambienceClip.name}'.", this);
            return;
        }

        if (!string.IsNullOrEmpty(ambienceKey))
        {
            if (!manager.TryGetClip(ambienceKey, out AudioClip clip))
            {
                if (logTransitions)
                    Debug.Log($"[{nameof(AmbienceZone)}] '{name}': ambienceKey '{ambienceKey}' chưa sẵn sàng.", this);
                return;
            }

            manager.SetAmbience(clip, fadeInTime);
            fired = true;

            if (logTransitions)
                Debug.Log($"[{nameof(AmbienceZone)}] Vào '{name}' -> crossfade '{clip.name}'.", this);
            return;
        }

        if (logTransitions)
            Debug.Log($"[{nameof(AmbienceZone)}] '{name}' chưa gán ambienceKey hay ambienceClip.", this);
    }

    private void Exit()
    {
        if (!playerInside && !fired) return;

        AudioManager manager = AudioManager.Instance;
        if (manager == null) return;

        if (restoreClip != null)
        {
            float fade = restoreFadeTime < 0f ? fadeOutTime : restoreFadeTime;
            manager.SetAmbience(restoreClip, fade);
        }
        else
        {
            manager.RestoreDefaultAmbience(fadeOutTime);
        }

        if (logTransitions) Debug.Log($"[{nameof(AmbienceZone)}] Rời '{name}'.", this);
    }

    private static bool IsPlayer(Collider other)
    {
        return other != null && other.GetComponentInParent<ThirdPersonController>() != null;
    }
}