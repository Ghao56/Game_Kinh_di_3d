#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class LightingDiagnostics : MonoBehaviour
{
    private struct Frame
    {
        public string label;
        public float meanFull;
        public float meanCenter;
        public float meanTop;
        public float p95Full;
        public float maxFull;
        public float maxNormX;
        public float maxNormY;
        public Color32[] pixels;
    }

    private struct Pose
    {
        public string label;
        public Vector3 position;
        public Quaternion rotation;
    }

    [Header("Tham chiếu")]
    [Tooltip("LightingManager cần đo. Để trống sẽ tự tìm trong scene.")]
    [SerializeField] private LightingManager manager;
    [Tooltip("Đèn pinh trong tay nhân vật. Để trống sẽ tự tìm spot light.")]
    [SerializeField] private Light flashlight;
    [Tooltip("Đèn hướng chính. Để trống sẽ tự tìm directional light.")]
    [SerializeField] private Light sun;

    [Header("Thông số đo")]
    [Tooltip("Tự chạy ngay khi vào Play mode. Dùng cho batchmode, không cần bấm F9.")]
    [SerializeField] private bool autoStart;
    [Min(160)] [SerializeField] private int sampleWidth = 960;
    [Min(90)] [SerializeField] private int sampleHeight = 540;
    [Range(1, 10)] [SerializeField] private int settleFrames = 3;
    [Tooltip("Số khung hình chờ thêm sau khi gọi DynamicGI.UpdateEnvironment.")]
    [Range(1, 20)] [SerializeField] private int giSettleFrames = 4;

    [Header("ROI đèn pinh")]
    [Tooltip("Ngưỡng lấy vùng đèn pinh: pixel sáng hơn ngưỡng này khi bật so với khi tắt (đơn vị % sRGB).")]
    [Range(0.5f, 20f)] [SerializeField] private float roiThreshold = 2f;

    private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
    private static readonly float[] ReflectionSweep = { 0f, 0.05f, 0.1f, 0.15f, 0.2f, 0.3f, 0.5f, 1f };

    private readonly StringBuilder report = new StringBuilder();
    private readonly List<string> notes = new List<string>();

    private Camera cam;
    private Transform player;
    private Quaternion playerRotation;
    private Pose poseNear;
    private Pose poseFar;
    private Pose poseBeam;
    private RenderTexture target;
    private float reflectionBase;
    private bool running;

    private bool[] roiMask;
    private int roiCount;
    private float roiBaselineOn = -1f;
    private float roiBaselineOff = -1f;

    private void Awake()
    {
        reflectionBase = RenderSettings.reflectionIntensity;
    }

    private void Start()
    {
        if (autoStart) StartDiagnostics();
    }

    public void SetAutoStart(bool value)
    {
        autoStart = value;
    }

    private void Update()
    {
        if (running) return;
        if (Keyboard.current == null) return;
        if (Keyboard.current.f9Key.wasPressedThisFrame) StartDiagnostics();
    }

    [ContextMenu("Chay chan doan anh sang (F9)")]
    public void StartDiagnostics()
    {
        if (running) return;
        if (!Resolve()) return;

        running = true;
        StartCoroutine(RunAll());
    }

    private bool Resolve()
    {
        if (manager == null) manager = FindFirstObjectByType<LightingManager>();
        if (manager == null)
        {
            Debug.LogError($"[{nameof(LightingDiagnostics)}] Không tìm thấy LightingManager trong scene.", this);
            return false;
        }

        cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError($"[{nameof(LightingDiagnostics)}] Không tìm thấy Main Camera.", this);
            return false;
        }

        if (flashlight == null) flashlight = FindBrightest(LightType.Spot) ?? FindBrightest(LightType.Point);
        if (flashlight == null)
        {
            Debug.LogError($"[{nameof(LightingDiagnostics)}] Không tìm thấy đèn pinh.", this);
            return false;
        }

        if (sun == null) sun = FindBrightest(LightType.Directional);
        if (sun == null)
        {
            Debug.LogError($"[{nameof(LightingDiagnostics)}] Không tìm thấy directional light.", this);
            return false;
        }

        player = GameObject.Find("Player")?.transform;
        if (player == null)
        {
            Debug.LogError($"[{nameof(LightingDiagnostics)}] Không tìm thấy GameObject 'Player'.", this);
            return false;
        }

        return true;
    }

    private Light FindBrightest(LightType type)
    {
        Light best = null;
        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != type) continue;
            if (best == null || light.intensity > best.intensity) best = light;
        }
        return best;
    }

    private IEnumerator RunAll()
    {
        report.Clear();
        notes.Clear();

        float savedReflection = RenderSettings.reflectionIntensity;
        bool savedFog = RenderSettings.fog;
        bool savedFlashlight = flashlight.enabled;
        var disabled = new List<MonoBehaviour>();
        var hiddenCanvases = new List<Canvas>();

        FreezePlayerScripts(disabled);
        HideUi(hiddenCanvases);

        target = new RenderTexture(sampleWidth, sampleHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
        {
            name = "LightingDiagnosticsRT",
            antiAliasing = 1
        };
        target.Create();

        playerRotation = player.rotation;
        BuildPoses();
        CollectValidity();

        yield return MeasureRegressionBaselines();
        yield return MeasureBeamRoi();
        yield return MeasureReflectionSweep();
        yield return MeasureDynamicGi();
        yield return MeasureSunFlip();
        yield return MeasureShipped();

        target.Release();
        UnityEngine.Object.Destroy(target);
        target = null;

        RenderSettings.reflectionIntensity = savedReflection;
        RenderSettings.fog = savedFog;
        flashlight.enabled = savedFlashlight;
        manager.SetNight();
        UnfreezePlayerScripts(disabled);
        ShowUi(hiddenCanvases);

        running = false;
        Emit();
    }

    private void BuildPoses()
    {
        Vector3 anchor = player.position;
        Vector3 forward = playerRotation * Vector3.forward;
        Vector3 right = playerRotation * Vector3.right;

        poseNear = BuildPlayerRelativePose("Gan", anchor, forward, right, -2.4f, 1.6f, 1.1f, 0.4f);
        poseFar = BuildPlayerRelativePose("Xa", anchor, forward, right, -6.5f, 4.2f, 0.6f, 2.0f);

        Transform spot = flashlight.transform;
        Vector3 beam = spot.forward;
        Vector3 focus = spot.position + beam * 4.5f;
        Vector3 beamCam = spot.position - beam * 3.6f + Vector3.up * 1.1f;
        poseBeam = new Pose
        {
            label = "DenPin",
            position = beamCam,
            rotation = Quaternion.LookRotation(focus - beamCam, Vector3.up)
        };
    }

    private static Pose BuildPlayerRelativePose(string label, Vector3 anchor, Vector3 forward, Vector3 right, float z, float y, float lookAtHeight, float side)
    {
        Vector3 p = anchor + forward * z + right * side + Vector3.up * y;
        return new Pose
        {
            label = label,
            position = p,
            rotation = Quaternion.LookRotation(anchor + Vector3.up * lookAtHeight - p, Vector3.up)
        };
    }

    private void Apply(Pose pose)
    {
        cam.transform.position = pose.position;
        cam.transform.rotation = pose.rotation;
    }

    private void SetStateDay() => manager.SetDay();
    private void SetStateDusk() => manager.SetDusk();
    private void SetStateNight() => manager.SetNight();

    private IEnumerator MeasureShipped()
    {
        Line("A7. CAU HINH SE SHIP (Apply() tu gan reflectionIntensity tu preset)");
        Raw($"  Preset day/dusk/night reflectionIntensity = {manager.GetPreset(LightingState.Day).reflectionIntensity:0.###} / {manager.GetPreset(LightingState.Dusk).reflectionIntensity:0.###} / {manager.GetPreset(LightingState.Night).reflectionIntensity:0.###}");
        Head();

        SetStateNight();
        Raw($"  -- Night, RenderSettings.reflectionIntensity sau Apply = {RenderSettings.reflectionIntensity:0.###}");

        Apply(poseNear);
        yield return Settle();
        Flashlight(false);
        Row(Capture(poseNear, "SHIP Night/Gan/off"));

        Apply(poseFar);
        yield return Settle();
        Flashlight(false);
        Row(Capture(poseFar, "SHIP Night/Xa/off"));

        Apply(poseBeam);
        yield return Settle();
        Flashlight(false);
        Frame off = Capture(poseBeam, "SHIP Night/DenPin/off", true);

        Flashlight(true);
        yield return Settle();
        Frame on = Capture(poseBeam, "SHIP Night/DenPin/on", true);

        Row(off);
        Row(on);
        Raw($"  ROI (mask co dinh) khi TAT = {MeanOverRoi(off.pixels):0.00} %, khi BAT = {MeanOverRoi(on.pixels):0.00} %, so pixel = {roiCount}");

        SetStateDay();
        foreach (Pose p in new[] { poseNear, poseFar })
        {
            Apply(p);
            yield return Settle();
            Flashlight(false);
            Row(Capture(p, $"SHIP Day/{p.label}/off"));
        }

        SetStateDusk();
        foreach (Pose p in new[] { poseNear, poseFar })
        {
            Apply(p);
            yield return Settle();
            Flashlight(false);
            Row(Capture(p, $"SHIP Dusk/{p.label}/off"));
        }

        ResetNight();
        Blank();
    }

    private void ResetNight()
    {
        manager.SetNight();
        RenderSettings.reflectionIntensity = reflectionBase;
    }

    private IEnumerator Settle()
    {
        for (int f = 0; f < settleFrames; f++) yield return null;
    }

    private IEnumerator MeasureRegressionBaselines()
    {
        Line("A1. MOC HOI QUY - Day va Dusk, den pinh TAT");
        Head();

        SetStateDay();
        foreach (Pose p in new[] { poseNear, poseFar })
        {
            yield return Settle();
            Flashlight(false);
            Frame f = Capture(p, $"Day/{p.label}/off");
            Row(f);
        }

        SetStateDusk();
        foreach (Pose p in new[] { poseNear, poseFar })
        {
            yield return Settle();
            Flashlight(false);
            Frame f = Capture(p, $"Dusk/{p.label}/off");
            Row(f);
        }

        Blank();
    }

    private IEnumerator MeasureBeamRoi()
    {
        Line("A2 + A3. VUNG DEN PINH (Night) + nguon cua Max");
        Head();

        Apply(poseBeam);
        ResetNight();

        Flashlight(false);
        yield return Settle();
        Frame off = Capture(poseBeam, "Night/DenPin/ROI-don-tat", true);

        Flashlight(true);
        yield return Settle();
        Frame on = Capture(poseBeam, "Night/DenPin/ROI-don-bat", true);

        BuildRoiMask(off.pixels, on.pixels);
        Row(off);
        Row(on);

        Line("A2. ROI (nguong > " + roiThreshold.ToString("0.##", CultureInfo.InvariantCulture) + " pp)");
        Raw($"  So pixel ROI      = {roiCount} / {sampleWidth * sampleHeight} ({roiCount * 100f / (sampleWidth * sampleHeight):0.00}%)");
        Raw($"  Mean ROI khi TAT  = {roiBaselineOff:0.00} %");
        Raw($"  Mean ROI khi BAT  = {roiBaselineOn:0.00} %");
        Raw($"  Max  ROI khi BAT  = {MaxOverRoi(on.pixels):0.0} %");
        if (roiCount == 0) Raw("  !! ROI RONG - xem phan 'Dieu kien spot' o tren.");

        Line("A3. Max cua khung khi den TAT (de loai highlight phan chieu)");
        Raw($"  Max toan khung    = {off.maxFull:0.0} % tai (x={off.maxNormX:0.00}, y={off.maxNormY:0.00})");
        Raw($"  Mean 20% day tren = {off.meanTop:0.00} %");
        Blank();
    }

    private IEnumerator MeasureReflectionSweep()
    {
        Line("A4. SWEEP reflectionIntensity tren Night");
        Raw("  Den pinh: pose Gan/Xa = TAT, pose DenPin = BAT. Cot ROI lay theo mask co dinh tu A2.");
        Head();
        Raw($"  {"reflection",-10} | {"Gan full",-9} | {"Xa full",-8} | {"DenPin full",-11} | {"ROI bat",-8} | chenh lech ROI vs baseline");
        Raw("  " + new string('-', 86));

        foreach (float value in ReflectionSweep)
        {
            Apply(poseNear);
            ResetNight();
            RenderSettings.reflectionIntensity = value;
            yield return Settle();
            Flashlight(false);
            Frame near = Capture(poseNear, $"Night/Gan/r={value:0.##}");

            Apply(poseFar);
            ResetNight();
            RenderSettings.reflectionIntensity = value;
            yield return Settle();
            Flashlight(false);
            Frame far = Capture(poseFar, $"Night/Xa/r={value:0.##}");

            Apply(poseBeam);
            ResetNight();
            RenderSettings.reflectionIntensity = value;
            yield return Settle();
            Flashlight(true);
            Frame beam = Capture(poseBeam, $"Night/DenPin/r={value:0.##}", true);

            float roiMean = MeanOverRoi(beam.pixels);
            float delta = roiBaselineOn < 0f ? 0f : roiMean - roiBaselineOn;

            Raw($"  {value,-10:0.##} | {near.meanFull,-9:0.00} | {far.meanFull,-8:0.00} | {beam.meanFull,-11:0.00} | {roiMean,-8:0.00} | {delta:+0.00;-0.00;0.00} pp");
        }

        RenderSettings.reflectionIntensity = reflectionBase;
        Blank();
    }

    private IEnumerator MeasureDynamicGi()
    {
        Line("A5. THU PHUONG AN (b): DynamicGI.UpdateEnvironment(), reflection = 1");
        Head();

        Apply(poseNear);
        ResetNight();
        yield return Settle();
        Flashlight(false);
        Row(Capture(poseNear, "Night/Gan/truoc-GI"));

        DynamicGI.UpdateEnvironment();
        for (int f = 0; f < giSettleFrames; f++) yield return null;

        Row(Capture(poseNear, "Night/Gan/sau-GI"));

        Apply(poseFar);
        yield return Settle();
        Row(Capture(poseFar, "Night/Xa/sau-GI"));

        Raw($"  reflectionIntensity sau UpdateEnvironment = {RenderSettings.reflectionIntensity:0.###}");
        Blank();
    }

    private IEnumerator MeasureSunFlip()
    {
        Line("A6. THU SUN: euler.x = +30 tren Night (chi do, khong sua)");
        Head();

        Apply(poseNear);
        ResetNight();
        yield return Settle();
        Flashlight(false);
        Frame before = Capture(poseNear, "Night/Gan/sun-x=-12");

        sun.transform.rotation = Quaternion.Euler(30f, -30f, 0f);
        yield return Settle();
        Frame after = Capture(poseNear, "Night/Gan/sun-x=+30");

        Raw($"  Full khung   : {before.meanFull:0.00} -> {after.meanFull:0.00}  (len {after.meanFull - before.meanFull:+0.00;-0.00} pp)");
        Raw($"  20% day tren  : {before.meanTop:0.00} -> {after.meanTop:0.00}  (len {after.meanTop - before.meanTop:+0.00;-0.00} pp)");
        Raw($"  Center       : {before.meanCenter:0.00} -> {after.meanCenter:0.00}  (len {after.meanCenter - before.meanCenter:+0.00;-0.00} pp)");

        ResetNight();
        Blank();
    }

    private void BuildRoiMask(Color32[] offPixels, Color32[] onPixels)
    {
        roiMask = new bool[offPixels.Length];
        roiCount = 0;

        for (int i = 0; i < offPixels.Length; i++)
        {
            if (Luma(onPixels[i]) - Luma(offPixels[i]) > roiThreshold) roiMask[i] = true;
        }

        roiCount = 0;
        for (int i = 0; i < roiMask.Length; i++)
        {
            if (roiMask[i]) roiCount++;
        }

        roiBaselineOn = MeanOverRoi(onPixels);
        roiBaselineOff = MeanOverRoi(offPixels);
    }

    private float MeanOverRoi(Color32[] pixels)
    {
        if (roiMask == null || roiCount == 0 || pixels == null) return -1f;

        double sum = 0.0;
        for (int i = 0; i < roiMask.Length; i++)
        {
            if (roiMask[i]) sum += Luma(pixels[i]);
        }
        return (float)(sum / roiCount * 100.0 / 255.0);
    }

    private float MaxOverRoi(Color32[] pixels)
    {
        if (roiMask == null || roiCount == 0 || pixels == null) return -1f;

        float max = 0f;
        for (int i = 0; i < roiMask.Length; i++)
        {
            if (!roiMask[i]) continue;
            float l = Luma(pixels[i]);
            if (l > max) max = l;
        }
        return max * 100f / 255f;
    }

    private void Flashlight(bool on)
    {
        flashlight.enabled = on;
    }

    private Frame Capture(Pose pose, string label, bool keepPixels = false)
    {
        Apply(pose);

        var request = new UniversalRenderPipeline.SingleCameraRequest
        {
            destination = target,
            mipLevel = 0,
            face = CubemapFace.Unknown,
            slice = 0
        };

        RenderPipeline.SubmitRenderRequest<UniversalRenderPipeline.SingleCameraRequest>(cam, request);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;

        var tex = new Texture2D(sampleWidth, sampleHeight, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0f, 0f, sampleWidth, sampleHeight), 0, 0);
        tex.Apply(false);

        RenderTexture.active = previous;

        Color32[] pixels = tex.GetPixels32();
        Frame f = Analyze(pixels, sampleWidth, sampleHeight, label);
        if (keepPixels) f.pixels = pixels;

        UnityEngine.Object.Destroy(tex);
        return f;
    }

    private static float Luma(Color32 p)
    {
        return 0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b;
    }

    private static Frame Analyze(Color32[] pixels, int w, int h, string label)
    {
        var hist = new int[256];
        double sumFull = 0.0, sumCenter = 0.0, sumTop = 0.0;
        long countFull = 0, countCenter = 0, countTop = 0;
        int max = 0, maxX = 0, maxY = 0;

        int x0 = w / 4, x1 = w - x0;
        int y0 = h / 4, y1 = h - y0;
        int yTop = h / 5;

        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            bool centerRow = y >= y0 && y < y1;
            bool topRow = y >= h - yTop;

            for (int x = 0; x < w; x++)
            {
                float lum = Luma(pixels[row + x]);
                int bin = (int)Mathf.Clamp(Mathf.RoundToInt(lum), 0, 255);
                hist[bin]++;

                if (bin > max) { max = bin; maxX = x; maxY = y; }

                sumFull += lum;
                countFull++;

                if (centerRow && x >= x0 && x < x1) { sumCenter += lum; countCenter++; }
                if (topRow) { sumTop += lum; countTop++; }
            }
        }

        long target95 = (long)(countFull * 0.95);
        long acc = 0;
        int p95 = 0;
        for (int i = 0; i < hist.Length; i++)
        {
            acc += hist[i];
            if (acc >= target95) { p95 = i; break; }
        }

        return new Frame
        {
            label = label,
            meanFull = (float)(sumFull / countFull * 100.0 / 255.0),
            meanCenter = countCenter > 0 ? (float)(sumCenter / countCenter * 100.0 / 255.0) : 0f,
            meanTop = countTop > 0 ? (float)(sumTop / countTop * 100.0 / 255.0) : 0f,
            p95Full = p95 * 100f / 255f,
            maxFull = max * 100f / 255f,
            maxNormX = (float)maxX / w,
            maxNormY = (float)maxY / h,
            pixels = null
        };
    }

    private void CollectValidity()
    {
        Volume volume = FindFirstObjectByType<Volume>();
        Tonemapping tone = volume != null && volume.profile != null && volume.profile.TryGet(out Tonemapping t) ? t : null;
        int toneMode = tone == null ? -99 : (tone.mode.overrideState ? (int)tone.mode.value : -1);

        UniversalRenderPipelineAsset urp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        string urpInfo = urp == null
            ? "khong tim thay UniversalRenderPipelineAsset"
            : $"additionalLights = {urp.additionalLightsRenderingMode} (1 = PerPixel), maxAdditionalLightsCount = {urp.maxAdditionalLightsCount}, supportsHDR = {urp.supportsHDR}";

        Line("DIEU KIEN DO");
        Raw($"  ColorSpace            = {QualitySettings.activeColorSpace} (1 = Linear)");
        Raw($"  ambientMode           = {RenderSettings.ambientMode} (1 = Trilight)");
        Raw($"  reflectionIntensity   = {reflectionBase:0.###}  <- Apply() khong dung toi");
        Raw($"  fog                   = {RenderSettings.fog}, mode = {RenderSettings.fogMode} (2 = ExpSquared), density = {RenderSettings.fogDensity:0.####}");
        Raw($"  Tonemapping mode      = {toneMode} (1 = Neutral, 2 = ACES)");
        Raw($"  URP                   = {urpInfo}");
        Raw($"  Sun                   = {sun.name}, intensity = {sun.intensity:0.###}, euler = {sun.transform.eulerAngles}, cullingMask = {sun.cullingMask}");
        Raw($"  Flashlight            = {flashlight.name}, type = {flashlight.type}, intensity = {flashlight.intensity:0.#}, range = {flashlight.range:0.##}");
        Raw($"                         spotAngle = {flashlight.spotAngle:0.#} / inner = {flashlight.innerSpotAngle:0.#}, cullingMask = {flashlight.cullingMask}, enabled = {flashlight.enabled}");
        Raw($"  Flashlight world pos  = {flashlight.transform.position}, forward = {flashlight.transform.forward}");
        Raw($"  Player world pos      = {player.position}");
        Raw($"  Camera target         = {sampleWidth}x{sampleHeight}, settle = {settleFrames} frame");
        Blank();
    }

    private void FreezePlayerScripts(List<MonoBehaviour> disabled)
    {
        foreach (MonoBehaviour mb in player.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null || !mb.enabled) continue;
            if (mb is LightingDiagnostics) continue;

            string typeName = mb.GetType().Name;
            if (typeName != "ThirdPersonCamera" && typeName != "ThirdPersonController") continue;

            mb.enabled = false;
            disabled.Add(mb);
        }
    }

    private void UnfreezePlayerScripts(List<MonoBehaviour> disabled)
    {
        foreach (MonoBehaviour mb in disabled)
        {
            if (mb != null) mb.enabled = true;
        }
        disabled.Clear();
    }

    private void HideUi(List<Canvas> hidden)
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas == null || !canvas.gameObject.activeSelf) continue;
            canvas.gameObject.SetActive(false);
            hidden.Add(canvas);
        }
    }

    private void ShowUi(List<Canvas> hidden)
    {
        foreach (Canvas canvas in hidden)
        {
            if (canvas != null) canvas.gameObject.SetActive(true);
        }
        hidden.Clear();
    }

    private void Line(string s)
    {
        report.AppendLine();
        report.AppendLine("### " + s);
    }

    private void Blank()
    {
        report.AppendLine();
    }

    private void Raw(string s)
    {
        report.AppendLine(s);
    }

    private void Head()
    {
        Raw(string.Format(CultureInfo.InvariantCulture,
            "  {0,-28} | {1,7} | {2,7} | {3,7} | {4,5}", "Case", "Full%", "Center%", "p95%", "Max%"));
        Raw("  " + new string('-', 66));
    }

    private void Row(Frame f)
    {
        Raw(string.Format(CultureInfo.InvariantCulture,
            "  {0,-28} | {1,7:0.00} | {2,7:0.00} | {3,7:0.0} | {4,5:0.0}",
            f.label, f.meanFull, f.meanCenter, f.p95Full, f.maxFull));
    }

    private void Emit()
    {
        var sb = new StringBuilder();
        sb.AppendLine("================ LIGHTING DIAGNOSTICS v2 ================");
        foreach (string n in notes) sb.AppendLine(n);
        sb.Append(report.ToString());

        Debug.Log(sb.ToString());

        try
        {
            string dir = Path.Combine(Application.dataPath, "..", "Temp");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.GetFullPath(Path.Combine(dir, "lighting_diagnostics.txt")), sb.ToString());
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[{nameof(LightingDiagnostics)}] Không ghi được file kết quả: {e.Message}", this);
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(0);
#endif
    }
}
#endif
