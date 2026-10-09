using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class LightingManager : MonoBehaviour
{
    [Header("Tham chiếu")]
    [Tooltip("Đèn hướng chính. Dùng lại làm cả trăng ban đêm nên không tốn thêm light slot.")]
    [SerializeField] private Light sunLight;

    [Tooltip("Global Volume chứa Color Adjustments. Bỏ trống nếu không dùng post-processing.")]
    [SerializeField] private Volume globalVolume;

    [Header("Preset")]
    [SerializeField] private LightingPreset day = new LightingPreset();
    [SerializeField] private LightingPreset dusk = new LightingPreset();
    [SerializeField] private LightingPreset night = new LightingPreset();

    [Header("Transition")]
    [Min(0.1f)]
    [SerializeField] private float duskDuration = 12f;
    [Min(0.1f)]
    [SerializeField] private float nightDuration = 18f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Sự kiện")]
    [Tooltip("Bắn khi trời đã tối hẳn. Dùng để spawn quái, đổi nhạc, đổi ambience.")]
    [SerializeField] private UnityEvent onNightFallen;

    public LightingState Target { get; private set; }
    public bool IsTransitioning { get; private set; }
    public float TransitionProgress { get; private set; }

    private static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");
    private static readonly int GroundColorId = Shader.PropertyToID("_GroundColor");
    private static readonly int AtmosphereThicknessId = Shader.PropertyToID("_AtmosphereThickness");
    private static readonly int ExposureId = Shader.PropertyToID("_Exposure");

    private readonly LightingPreset current = new LightingPreset();
    private readonly LightingPreset from = new LightingPreset();
    private readonly LightingPreset to = new LightingPreset();

    private Material skyboxInstance;
    private ColorAdjustments colorAdjustments;
    private bool skyboxDrivable;
    private float elapsed;
    private float duration;

    private void Awake()
    {
        if (sunLight == null)
        {
            Debug.LogError($"[{nameof(LightingManager)}] Chưa gán 'sunLight' trong Inspector.", this);
            enabled = false;
            return;
        }

        EnsurePresets();

        sunLight.useColorTemperature = false;
        RenderSettings.sun = sunLight;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;

        SetupSkybox();
        SetupVolume();

        current.CopyFrom(day);
        Target = LightingState.Day;
        IsTransitioning = false;
        TransitionProgress = 1f;
        Apply(current);
    }

    public void SetDay()
    {
        ApplyImmediate(LightingState.Day, day);
    }

    public void SetDusk()
    {
        ApplyImmediate(LightingState.Dusk, dusk);
    }

    public void SetNight()
    {
        ApplyImmediate(LightingState.Night, night);
    }

    public void StartTransitionToDusk()
    {
        BeginTransition(LightingState.Dusk, dusk, duskDuration);
    }

    public void StartTransitionToNight()
    {
        BeginTransition(LightingState.Night, night, nightDuration);
    }

    public void ResetPresetsToRecommended()
    {
        day = LightingPreset.CreateDay();
        dusk = LightingPreset.CreateDusk();
        night = LightingPreset.CreateNight();
    }

    public LightingPreset GetPreset(LightingState state)
    {
        switch (state)
        {
            case LightingState.Dusk:
                return dusk;
            case LightingState.Night:
                return night;
            default:
                return day;
        }
    }

    private void Update()
    {
        if (!IsTransitioning) return;

        elapsed += Time.deltaTime;
        TransitionProgress = Mathf.Clamp01(elapsed / duration);

        current.Lerp(from, to, transitionCurve.Evaluate(TransitionProgress));
        Apply(current);

        if (TransitionProgress < 1f) return;

        IsTransitioning = false;
        if (Target == LightingState.Night) { onNightFallen?.Invoke(); FlowBus.Raise("night_fallen"); }
    }

    private void OnDestroy()
    {
        if (skyboxInstance == null) return;

        if (RenderSettings.skybox == skyboxInstance)
        {
            RenderSettings.skybox = null;
        }

        Destroy(skyboxInstance);
        skyboxInstance = null;
    }

    private void EnsurePresets()
    {
        if (day == null) day = LightingPreset.CreateDay();
        if (dusk == null) dusk = LightingPreset.CreateDusk();
        if (night == null) night = LightingPreset.CreateNight();
    }

    private void BeginTransition(LightingState target, LightingPreset preset, float seconds)
    {
        from.CopyFrom(current);
        to.CopyFrom(preset);

        duration = Mathf.Max(0.01f, seconds);
        elapsed = 0f;
        Target = target;
        IsTransitioning = true;
        TransitionProgress = 0f;
    }

    private void ApplyImmediate(LightingState state, LightingPreset preset)
    {
        current.CopyFrom(preset);
        Target = state;
        IsTransitioning = false;
        TransitionProgress = 1f;
        Apply(current);
    }

    private void SetupSkybox()
    {
        if (RenderSettings.skybox == null)
        {
            Debug.LogWarning($"[{nameof(LightingManager)}] RenderSettings.skybox đang null, bỏ qua điều khiển trời.", this);
            return;
        }

        skyboxInstance = new Material(RenderSettings.skybox);
        RenderSettings.skybox = skyboxInstance;

        skyboxDrivable = skyboxInstance.HasProperty(SkyTintId);
        if (!skyboxDrivable)
        {
            Debug.LogWarning($"[{nameof(LightingManager)}] Skybox không dùng shader 'Skybox/Procedural', bỏ qua điều khiển trời.", this);
        }
    }

    private void SetupVolume()
    {
        if (globalVolume == null)
        {
            Debug.LogWarning($"[{nameof(LightingManager)}] Chưa gán 'globalVolume', bỏ qua Color Adjustments.", this);
            return;
        }

        VolumeProfile profile = globalVolume.profile;
        if (profile == null)
        {
            Debug.LogWarning($"[{nameof(LightingManager)}] Volume '{globalVolume.name}' chưa có Profile.", this);
            return;
        }

        if (!profile.TryGet(out ColorAdjustments existing))
        {
            colorAdjustments = profile.Add<ColorAdjustments>(true);
        }
        else
        {
            colorAdjustments = existing;
        }

        colorAdjustments.active = true;
    }

    private void Apply(LightingPreset p)
    {
        RenderSettings.ambientSkyColor = p.ambientSky;
        RenderSettings.ambientEquatorColor = p.ambientEquator;
        RenderSettings.ambientGroundColor = p.ambientGround;
        RenderSettings.ambientIntensity = p.ambientIntensity;

        RenderSettings.fogColor = p.fogColor;
        RenderSettings.fogDensity = p.fogDensity;

        RenderSettings.reflectionIntensity = p.reflectionIntensity;

        sunLight.transform.rotation = Quaternion.Euler(p.sunEuler);
        sunLight.color = p.sunColor;
        sunLight.intensity = p.sunIntensity;
        sunLight.enabled = p.sunIntensity > 0.001f;

        if (skyboxDrivable)
        {
            skyboxInstance.SetColor(SkyTintId, p.skyTint);
            skyboxInstance.SetColor(GroundColorId, p.skyGround);
            skyboxInstance.SetFloat(AtmosphereThicknessId, p.atmosphereThickness);
            skyboxInstance.SetFloat(ExposureId, p.skyExposure);
        }

        if (colorAdjustments == null) return;

        colorAdjustments.postExposure.value = p.postExposure;
        colorAdjustments.contrast.value = p.postContrast;
        colorAdjustments.saturation.value = p.postSaturation;
        colorAdjustments.hueShift.value = p.postHueShift;
        colorAdjustments.colorFilter.value = Color.Lerp(Color.white, p.postFilter, p.postFilterAmount);
    }

    private void OnDrawGizmosSelected()
    {
        if (sunLight == null) return;

        Gizmos.color = sunLight.color;
        Gizmos.DrawWireSphere(sunLight.transform.position, 0.5f);
    }
}
