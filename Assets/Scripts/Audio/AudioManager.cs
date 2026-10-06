using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// Phát âm thanh: pool one-shot, loop có handle, ambience nền, bus âm lượng, caption.
/// Đặt thẳng trong scene (giống DialogueManager) — không DontDestroyOnLoad.
public class AudioManager : MonoBehaviour
{
    /// Handle trả về cho PlayLoop, dùng để Stop.
    public readonly struct AudioHandle
    {
        public readonly AudioSource Source;

        internal AudioHandle(AudioSource source)
        {
            Source = source;
        }

        public bool IsValid => Source != null;
        public bool IsPlaying => Source != null && Source.isPlaying;
    }

    public static AudioManager Instance { get; private set; }

    public SfxLibrary Library => library;

    [Header("Dữ liệu")]
    [Tooltip("Để trống thì mọi key đều bị bỏ qua (chỉ cảnh báo 1 lần).")]
    [SerializeField] private SfxLibrary library;

    [Header("Pool one-shot")]
    [SerializeField] [Min(1)] private int pool2DSize = 12;
    [SerializeField] [Min(1)] private int pool3DSize = 16;
    [SerializeField] [Min(1)] private int maxLoopSources = 8;

    [Header("Ambience nền")]
    [SerializeField] private AudioClip defaultAmbience;
    [SerializeField] [Min(0f)] private float defaultAmbienceFade = 2f;
    [SerializeField] private bool playDefaultAmbienceOnAwake = true;

    [Header("Âm lượng bus (dùng khi chưa gán AudioMixer)")]
    [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float ambienceVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float voiceVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float uiVolume = 1f;

    [Header("AudioMixer (tuỳ chọn)")]
    [Tooltip("Gán AudioMixer để định tuyến nhóm. Để trống thì dùng âm lượng bus ở trên.")]
    [SerializeField] private AudioMixer mixer;

    [Header("Caption")]
    [SerializeField] private CaptionUI captionUI;

    [Header("Debug")]
    [Tooltip("Log mỗi lần phát. Chỉ bật khi debug, tắt trước khi build.")]
    [SerializeField] private bool verboseLogging;

    private AudioSource[] pool2D;
    private AudioSource[] pool3D;
    private AudioSource ambienceA;
    private AudioSource ambienceB;
    private bool ambienceUsingA = true;

    private readonly List<AudioSource> loopSources = new List<AudioSource>();
    private readonly HashSet<AudioSource> loopBusy = new HashSet<AudioSource>();
    private readonly Dictionary<AudioSource, Coroutine> fadeRoutines = new Dictionary<AudioSource, Coroutine>();

    private readonly Dictionary<string, float> busVolumes = new Dictionary<string, float>();
    private readonly Dictionary<string, AudioMixerGroup> mixerGroups = new Dictionary<string, AudioMixerGroup>();
    private readonly Dictionary<string, float> lastPlayedTime = new Dictionary<string, float>();
    private readonly HashSet<string> warned = new HashSet<string>();

    /// Nhớ param mixer không tồn tại để không gọi lại AudioMixer mỗi lần phát.
    private readonly HashSet<string> missingMixerParams = new HashSet<string>();

    private Coroutine ambienceFadeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"[{nameof(AudioManager)}] Đã có AudioManager trong scene.", this);
            enabled = false;
            return;
        }

        Instance = this;

        if (captionUI == null) captionUI = FindFirstObjectByType<CaptionUI>();

        BuildBusTable();
        BuildPool();
        BuildAmbience();

        if (library != null) library.Rebuild();

        if (playDefaultAmbienceOnAwake && defaultAmbience != null)
            SetAmbience(defaultAmbience, 0f);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------------------------------------------------------------- setup

    private void BuildBusTable()
    {
        busVolumes[SfxLibrary.GroupMaster] = masterVolume;
        busVolumes[SfxLibrary.GroupMusic] = musicVolume;
        busVolumes[SfxLibrary.GroupAmbience] = ambienceVolume;
        busVolumes[SfxLibrary.GroupSfx] = sfxVolume;
        busVolumes[SfxLibrary.GroupVoice] = voiceVolume;
        busVolumes[SfxLibrary.GroupUi] = uiVolume;
    }

    private void BuildPool()
    {
        pool2D = CreateSources("Sfx2D", Mathf.Max(1, pool2DSize), 0f);
        pool3D = CreateSources("Sfx3D", Mathf.Max(1, pool3DSize), 1f);
    }

    private AudioSource[] CreateSources(string label, int count, float spatialBlend)
    {
        var sources = new AudioSource[count];

        for (int i = 0; i < count; i++)
        {
            sources[i] = CreateSource($"{label}_{i:00}", spatialBlend);
        }

        return sources;
    }

    private AudioSource CreateSource(string sourceName, float spatialBlend)
    {
        var go = new GameObject(sourceName);
        go.transform.SetParent(transform, false);

        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = spatialBlend;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        return source;
    }

    private void BuildAmbience()
    {
        ambienceA = CreateSource("Ambience_A", 0f);
        ambienceB = CreateSource("Ambience_B", 0f);
        ambienceA.loop = true;
        ambienceB.loop = true;
    }

    // ---------------------------------------------------------------- one-shot

    /// Phát 2D (không gắn vị trí). captionOverride null = dùng caption trong SfxLibrary.
    public bool TryPlay(string key, float volumeScale = 1f, string captionOverride = null)
    {
        return PlayInternal(key, null, volumeScale, captionOverride);
    }

    /// Phát 3D tại một vị trí trong scene.
    public bool TryPlayAt(string key, Vector3 position, float volumeScale = 1f, string captionOverride = null)
    {
        return PlayInternal(key, position, volumeScale, captionOverride);
    }

    private bool PlayInternal(string key, Vector3? position, float volumeScale, string captionOverride)
    {
        if (!TryResolve(key, out SfxLibrary.Entry entry, out AudioClip clip)) return false;
        if (!PassesCooldown(key, entry.cooldown)) return false;

        bool spatial = position.HasValue;
        AudioSource source = Acquire(spatial ? pool3D : pool2D);
        if (source == null) return false;

        source.clip = clip;
        source.loop = false;
        source.playOnAwake = false;
        source.spatialBlend = spatial ? Mathf.Clamp01(entry.spatialBlend) : 0f;
        source.volume = FinalVolume(entry, volumeScale);
        source.pitch = RandomPitch(entry);
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = Mathf.Max(0.01f, entry.minDistance);
        source.maxDistance = Mathf.Max(source.minDistance, entry.maxDistance);

        if (spatial) source.transform.position = position.Value;
        else source.transform.localPosition = Vector3.zero;

        ApplyMixerGroup(source, entry.group);
        source.Play();

        lastPlayedTime[key] = Time.unscaledTime;
        ShowCaption(entry, captionOverride);

        if (verboseLogging) Debug.Log($"[{nameof(AudioManager)}] Play '{key}' ({clip.name}).", this);
        return true;
    }

    /// Lấy source rảnh trong pool. Pool cố định nên không bao giờ phình to;
    /// hết chỗ thì cưỡng chế source nào sắp hết clip nhất.
    private static AudioSource Acquire(AudioSource[] pool)
    {
        if (pool == null) return null;

        AudioSource soonest = null;
        float soonestRemaining = float.MaxValue;

        foreach (AudioSource source in pool)
        {
            if (source == null) continue;
            if (!source.isPlaying) return source;

            float remaining = source.clip != null ? source.clip.length - source.time : 0f;
            if (remaining < soonestRemaining)
            {
                soonestRemaining = remaining;
                soonest = source;
            }
        }

        return soonest;
    }

    // ---------------------------------------------------------------- loop

    public AudioHandle PlayLoop(string key, Vector3 position, bool spatial = false, float volumeScale = 1f)
    {
        if (!TryResolve(key, out SfxLibrary.Entry entry, out AudioClip clip))
            return default;

        return PlayLoop(clip, position, spatial, entry, volumeScale);
    }

    public AudioHandle PlayLoop(AudioClip clip, Vector3 position, bool spatial = false, float volumeScale = 1f)
    {
        if (clip == null)
        {
            WarnOnce("loop_null_clip", $"[{nameof(AudioManager)}] PlayLoop nhận clip null.", this);
            return default;
        }

        var fallback = new SfxLibrary.Entry
        {
            key = clip.name,
            group = SfxLibrary.GroupSfx,
            spatialBlend = spatial ? 1f : 0f
        };

        return PlayLoop(clip, position, spatial, fallback, volumeScale);
    }

    private AudioHandle PlayLoop(AudioClip clip, Vector3 position, bool spatial, SfxLibrary.Entry entry, float volumeScale)
    {
        AudioSource source = AcquireLoop();
        if (source == null)
        {
            WarnOnce("loop_pool_full", $"[{nameof(AudioManager)}] Hết chỗ cho loop (maxLoopSources).", this);
            return default;
        }

        source.Stop();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = spatial ? Mathf.Clamp01(entry.spatialBlend) : 0f;
        source.volume = FinalVolume(entry, volumeScale);
        source.pitch = RandomPitch(entry);
        source.minDistance = Mathf.Max(0.01f, entry.minDistance);
        source.maxDistance = Mathf.Max(source.minDistance, entry.maxDistance);
        source.transform.position = position;

        ApplyMixerGroup(source, entry.group);
        source.Play();

        return new AudioHandle(source);
    }

    public void Stop(AudioHandle handle, float fadeOut = 0.25f)
    {
        AudioSource source = handle.Source;
        if (source == null) return;

        if (fadeOut <= 0f)
        {
            ReleaseLoop(source);
            return;
        }

        if (fadeRoutines.TryGetValue(source, out Coroutine running)) StopCoroutine(running);
        fadeRoutines[source] = StartCoroutine(FadeOutLoop(source, fadeOut));
    }

    private IEnumerator FadeOutLoop(AudioSource source, float duration)
    {
        float startVolume = source.volume;
        float elapsed = 0f;

        while (elapsed < duration && source != null)
        {
            elapsed += Time.deltaTime;
            source.volume = startVolume * (1f - Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        fadeRoutines.Remove(source);
        ReleaseLoop(source);
    }

    private AudioSource AcquireLoop()
    {
        foreach (AudioSource source in loopSources)
        {
            if (source != null && !loopBusy.Contains(source)) return source;
        }

        if (loopSources.Count < Mathf.Max(1, maxLoopSources))
        {
            AudioSource created = CreateSource($"Loop_{loopSources.Count:00}", 1f);
            loopSources.Add(created);
            loopBusy.Add(created);
            return created;
        }

        // Hết chỗ: loop không tự kết thúc nên không có "sắp hết", cứ cưỡng chế cái nào
        // đang chạy lâu nhất (time nhỏ nhất).
        AudioSource oldest = null;
        float oldestTime = float.MaxValue;

        foreach (AudioSource source in loopSources)
        {
            if (source == null) continue;
            if (source.time < oldestTime)
            {
                oldestTime = source.time;
                oldest = source;
            }
        }

        if (oldest == null) return null;

        if (fadeRoutines.TryGetValue(oldest, out Coroutine running))
        {
            StopCoroutine(running);
            fadeRoutines.Remove(oldest);
        }

        return oldest;
    }

    private void ReleaseLoop(AudioSource source)
    {
        if (source == null) return;

        source.Stop();
        source.clip = null;
        source.volume = 0f;
        loopBusy.Remove(source);
    }

    // ---------------------------------------------------------------- ambience

    /// Crossfade ambience nền. clip null = tắt hẳn ambience.
    public void SetAmbience(AudioClip clip, float fadeTime = -1f)
    {
        if (ambienceA == null) return;
        if (fadeTime < 0f) fadeTime = defaultAmbienceFade;

        AudioSource current = ambienceUsingA ? ambienceA : ambienceB;
        if (clip != null && current != null && current.clip == clip && current.isPlaying) return;

        AudioSource incoming = ambienceUsingA ? ambienceB : ambienceA;
        AudioSource outgoing = ambienceUsingA ? ambienceA : ambienceB;

        if (ambienceFadeRoutine != null)
        {
            StopCoroutine(ambienceFadeRoutine);
            ambienceFadeRoutine = null;
        }

        if (clip != null)
        {
            incoming.Stop();
            incoming.clip = clip;
            incoming.loop = true;
            incoming.playOnAwake = false;
            incoming.spatialBlend = 0f;
            incoming.volume = 0f;
            incoming.transform.localPosition = Vector3.zero;
            ApplyMixerGroup(incoming, SfxLibrary.GroupAmbience);
            incoming.Play();
        }

        ambienceUsingA = !ambienceUsingA;

        if (fadeTime <= 0f)
        {
            incoming.volume = clip != null ? CurrentAmbienceVolume() : 0f;
            if (outgoing != null) ReleaseAmbience(outgoing);
            return;
        }

        ambienceFadeRoutine = StartCoroutine(CrossfadeAmbience(incoming, outgoing, fadeTime));
    }

    public void SetAmbienceKey(string key, float fadeTime = -1f)
    {
        if (string.IsNullOrEmpty(key))
        {
            SetAmbience(null, fadeTime);
            return;
        }

        if (!TryResolve(key, out SfxLibrary.Entry entry, out AudioClip clip))
        {
            WarnOnce($"ambience:{key}", $"[{nameof(AudioManager)}] Ambience key '{key}' chưa sẵn sàng.", this);
            return;
        }

        SetAmbience(clip, fadeTime);
    }

    private IEnumerator CrossfadeAmbience(AudioSource incoming, AudioSource outgoing, float duration)
    {
        float inVolume = incoming.clip != null ? CurrentAmbienceVolume() : 0f;
        float outVolume = outgoing != null ? outgoing.volume : 0f;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);

            if (incoming != null) incoming.volume = inVolume * k;
            if (outgoing != null) outgoing.volume = outVolume * (1f - k);

            yield return null;
        }

        if (incoming != null) incoming.volume = inVolume;
        if (outgoing != null) ReleaseAmbience(outgoing);

        ambienceFadeRoutine = null;
    }

    private static void ReleaseAmbience(AudioSource source)
    {
        source.Stop();
        source.clip = null;
        source.volume = 0f;
    }

    private float CurrentAmbienceVolume()
    {
        return Mathf.Clamp01(GetBusVolume(SfxLibrary.GroupAmbience) * GetBusVolume(SfxLibrary.GroupMaster));
    }

    // ---------------------------------------------------------------- bus / mixer

    public float GetBusVolume(string group)
    {
        if (string.IsNullOrEmpty(group)) return 1f;
        return busVolumes.TryGetValue(group, out float value) ? value : 1f;
    }

    public void SetBusVolume(string group, float value)
    {
        if (string.IsNullOrEmpty(group)) return;

        value = Mathf.Clamp01(value);
        busVolumes[group] = value;

        string param = MixerParamName(group);
        if (TryMixerGet(param, out _)) mixer.SetFloat(param, value);

        if (group == SfxLibrary.GroupMaster || group == SfxLibrary.GroupAmbience)
            ApplyAmbienceVolume();
    }

    private void ApplyAmbienceVolume()
    {
        AudioSource current = ambienceUsingA ? ambienceA : ambienceB;
        if (current == null || current.clip == null) return;

        current.volume = CurrentAmbienceVolume();
    }

    private void ApplyMixerGroup(AudioSource source, string group)
    {
        if (mixer == null || source == null || string.IsNullOrEmpty(group)) return;

        if (!mixerGroups.TryGetValue(group, out AudioMixerGroup mixerGroup))
        {
            // FindMatchingGroups trả về mảng (một tên group có thể tồn tại ở nhiều mixer).
            AudioMixerGroup[] matches = mixer.FindMatchingGroups(group);
            mixerGroup = matches != null && matches.Length > 0 ? matches[0] : null;
            mixerGroups[group] = mixerGroup;

            if (mixerGroup == null)
                WarnOnce($"mixer_group:{group}", $"[{nameof(AudioManager)}] AudioMixer không có group '{group}'.", this);
        }

        if (mixerGroup != null) source.outputAudioMixerGroup = mixerGroup;
    }

    private static string MixerParamName(string group)
    {
        switch (group)
        {
            case SfxLibrary.GroupMaster: return "MasterVol";
            case SfxLibrary.GroupMusic: return "MusicVol";
            case SfxLibrary.GroupAmbience: return "AmbienceVol";
            case SfxLibrary.GroupSfx: return "SfxVol";
            case SfxLibrary.GroupVoice: return "VoiceVol";
            case SfxLibrary.GroupUi: return "UiVol";
            default: return null;
        }
    }

    // Nhớ những param không tồn tại để không gọi lại AudioMixer mỗi lần phát.
    private bool TryMixerGet(string paramName, out float value)
    {
        value = 1f;

        if (mixer == null || string.IsNullOrEmpty(paramName)) return false;
        if (missingMixerParams.Contains(paramName)) return false;

        try
        {
            if (mixer.GetFloat(paramName, out value)) return true;
        }
        catch (Exception)
        {
            // Mixer hỏng / param không hợp lệ -> coi như không có mixer.
        }

        missingMixerParams.Add(paramName);
        return false;
    }

    // ---------------------------------------------------------------- helpers

    public bool TryGetDuration(string key, out float duration)
    {
        duration = 0f;

        if (!TryResolve(key, out _, out AudioClip clip) || clip == null) return false;

        duration = clip.length;
        return true;
    }

    /// Lấy clip của một key, để kiểm tra trước khi setup zone / trigger.
    public bool TryGetClip(string key, out AudioClip clip)
    {
        clip = null;

        if (!TryResolve(key, out _, out AudioClip resolved)) return false;

        clip = resolved;
        return true;
    }

    /// Trở lại ambience mặc định đã gán ở Inspector.
    public void RestoreDefaultAmbience(float fadeTime = -1f)
    {
        if (defaultAmbience == null)
        {
            WarnOnce("no_default_ambience", $"[{nameof(AudioManager)}] Chưa gán Default Ambience.", this);
            return;
        }

        SetAmbience(defaultAmbience, fadeTime);
    }

    private bool TryResolve(string key, out SfxLibrary.Entry entry, out AudioClip clip)
    {
        entry = null;
        clip = null;

        if (string.IsNullOrEmpty(key)) return false;

        if (library == null)
        {
            WarnOnce("no_library", $"[{nameof(AudioManager)}] Chưa gán SfxLibrary — mọi key sẽ bị bỏ qua.", this);
            return false;
        }

        if (!library.TryGet(key, out entry))
        {
            WarnOnce($"missing:{key}", $"[{nameof(AudioManager)}] SfxLibrary không có key '{key}'.", this);
            return false;
        }

        if (!entry.HasClip)
        {
            WarnOnce($"empty:{key}", $"[{nameof(AudioManager)}] Key '{key}' chưa gán clip nào — bỏ qua.", this);
            return false;
        }

        clip = entry.PickClip();

        if (clip == null)
        {
            WarnOnce($"null_slot:{key}", $"[{nameof(AudioManager)}] Key '{key}' chỉ có slot null — bỏ qua.", this);
            return false;
        }

        return true;
    }

    private bool PassesCooldown(string key, float cooldown)
    {
        if (cooldown <= 0f) return true;

        if (lastPlayedTime.TryGetValue(key, out float last) && Time.unscaledTime - last < cooldown)
            return false;

        return true;
    }

    private float FinalVolume(SfxLibrary.Entry entry, float volumeScale)
    {
        float volume = Mathf.Clamp01(entry.volume) * Mathf.Max(0f, volumeScale);
        volume *= GetBusVolume(entry.group);

        if (entry.group != SfxLibrary.GroupMaster)
            volume *= GetBusVolume(SfxLibrary.GroupMaster);

        return Mathf.Clamp01(volume);
    }

    private static float RandomPitch(SfxLibrary.Entry entry)
    {
        float min = entry.pitchRange.x;
        float max = entry.pitchRange.y;

        if (max < min)
        {
            float swap = min;
            min = max;
            max = swap;
        }

        return Mathf.Approximately(min, max) ? min : UnityEngine.Random.Range(min, max);
    }

    private void ShowCaption(SfxLibrary.Entry entry, string captionOverride)
    {
        if (captionUI == null) return;

        string text = captionOverride ?? entry.caption;
        if (string.IsNullOrEmpty(text)) return;

        captionUI.Show(text);
    }

    private void WarnOnce(string warnKey, string message, UnityEngine.Object context)
    {
        if (!warned.Add(warnKey)) return;
        Debug.LogWarning(message, context);
    }
}