using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Leo thang do kho cua ma theo concept muc 4 - 3 giai doan:
//   Stage 0 (dau luot): di cham, tuan tra, tao tieng dong thoi.
//   Stage 1 (giua luot): nhanh hon, nghe tieng dong, di kiem tra khu vuc.
//   Stage 2 (cuoi luot): chuyen dong truy duoi, phan ung nhanh, cat mat nguoi choi.
//
// Stage quyet dinh bang MAX cua hai nguon (concept muc 4: "theo thoi gian HOAC
// theo luong do da lay"):
//   Tien da chat len xe: >= 15M -> stage 1, >= 35M -> stage 2
//   Thoi gian da choi  : >= 8ph -> stage 1, >= 16ph -> stage 2
// Loot nhanh cung khong lam ma bien mat ky thuat nao - chi tang dan.
//
// Danh hieu tang stage NHUNG CO DAM BAO TRUOC (concept muc 4):
//   - Am thanh qua AudioManager key co san (thieu clip chi im lang, khong loi)
//   - Den point/spot gan player cham cham ~1.2s
//   - SMS "Chu no" khi vua qua moc 25M
//
// Object tu bootstrap, DontDestroyOnLoad, ap cho moi EnemyAI3D + EnemyHearing
// cua scene hien tai (refind moi scene load - khong cache object scene cu).
public sealed class GhostEscalation : MonoBehaviour
{
    public static GhostEscalation Instance { get; private set; }

    // Stage hien tai (0..2): EnemyHearing doc de chot co "nghe noise".
    public static int CurrentStage { get; private set; }

    private const int MaxStage = 2;
    private const float SetupPollInterval = 2f;

    [Header("Nguong leo thang")]
    [Tooltip("Tien da chat len xe (trieu) de qua stage 1 / 2.")]
    [SerializeField] private Vector2 paidStageThresholds = new Vector2(15f, 35f);

    [Tooltip("Phut choi de qua stage 1 / 2 (chan cho luot loot cham giam dan tuon thang).")]
    [SerializeField] private Vector2 timeStageMinutes = new Vector2(8f, 16f);

    [Header("Toc do - NHAN voi gia goc co trong scene, khong ghi de tuyet doi")]
    [Tooltip("He so patrolSpeed moi stage (stage 1, stage 2).")]
    [SerializeField] private Vector2 patrolSpeedMul = new Vector2(1.3f, 1.6f);

    [Tooltip("He so chaseSpeed moi stage, kep toi da de khong nhanh hon sprint player.")]
    [SerializeField] private Vector2 chaseSpeedMul = new Vector2(1.15f, 1.3f);

    [Tooltip("Toc doi chase toi da (m/s) - player di chuyen 5 m/s truoc multiplier.")]
    [SerializeField] private float maxChaseSpeed = 4.6f;

    [Tooltip("He so scanDistance moi stage.")]
    [SerializeField] private Vector2 scanDistanceMul = new Vector2(1.2f, 1.45f);

    [Header("Bao truoc khi len stage")]
    [Tooltip("Key am thanh AudioManager khi qua stage 1 / 2. De trong = bo qua.")]
    [SerializeField] private string stageSoundKey1 = "door_creak";
    [SerializeField] private string stageSoundKey2 = "memory_thud";

    [Tooltip("Ban kinh den cham cham quanh player khi len stage (m).")]
    [SerializeField] private float flickerRadius = 12f;

    [Tooltip("Tien (trieu) qua moc thi gui SMS chua no.")]
    [SerializeField] private float smsDebtReportThreshold = 25f;

    // Gia tri goc captured mot lan moi enemy; he so stage nhan tu DAY.
    // Chi TANG khong GIAM - hoi stage khong bao gio ha lui.
    private struct EnemyBase
    {
        public float PatrolSpeed;
        public float ChaseSpeed;
        public float ScanDistance;
        public float SearchDuration;
    }

    private readonly List<EnemyAI3D> enemies = new List<EnemyAI3D>();
    private readonly List<EnemyHearing> hearings = new List<EnemyHearing>();
    private readonly Dictionary<EnemyAI3D, EnemyBase> enemyBases = new Dictionary<EnemyAI3D, EnemyBase>();
    private readonly Dictionary<EnemyHearing, float> hearingBases = new Dictionary<EnemyHearing, float>();

    private float runElapsedSeconds;
    private float setupPollTimer;
    private bool smsQuarterSent;

    // ----------------------------------------------------------- lifecycle

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        CurrentStage = 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        Instance = new GameObject("GhostEscalation").AddComponent<GhostEscalation>();
        DontDestroyOnLoad(Instance.gameObject);
    }

    private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Scene additive không phải lượt mới: giữ stage/Timer đang chạy.
        if (mode == LoadSceneMode.Additive) return;

        // Moi scene load = luot choi moi (chet reload, menu vao lai):
        // stage + timer + cache enemy cu deu reset.
        CurrentStage = 0;
        runElapsedSeconds = 0f;
        smsQuarterSent = false;

        enemies.Clear();
        hearings.Clear();
        enemyBases.Clear();
        hearingBases.Clear(); // enemy cu da xoa theo scene, tham chieu thanh fake-null
    }

    // ----------------------------------------------------------- vong chinh

    private void Update()
    {
        if (QuestManager.Instance == null) return; // menu / scene khong co quest

        runElapsedSeconds += Time.deltaTime;

        int stage = ComputeStage();
        if (stage > CurrentStage)
        {
            CurrentStage = Mathf.Min(stage, MaxStage);
            AnnounceStage(CurrentStage);
        }

        ApplyStageToEnemies();
        PollWorld();
    }

    // Tim lai enemy moi + SMS moc tien. Enemy co the sinh them giua luot, khong
    // giu tham chieu object scene cu qua scene load. Tim moi 2s la du.
    private void PollWorld()
    {
        setupPollTimer += Time.unscaledDeltaTime;
        if (setupPollTimer < SetupPollInterval) return;
        setupPollTimer = 0f;

        RefindEnemies();
        ApplyStageToEnemies(); // enemy vua tim duoc: nap base + ap stage ngay
        TryQuarterSms();
    }

    private int ComputeStage()
    {
        float paid = DebtTracker.UncappedPaidMillions;
        int stage = 0;

        if (paid >= paidStageThresholds.x) stage = 1;
        if (paid >= paidStageThresholds.y) stage = 2;

        float minutes = runElapsedSeconds / 60f;
        if (minutes >= timeStageMinutes.x) stage = Mathf.Max(stage, 1);
        if (minutes >= timeStageMinutes.y) stage = Mathf.Max(stage, 2);

        return Mathf.Min(stage, MaxStage);
    }

    // --------------------------------------------------- ap stage len ma

    private void ApplyStageToEnemies()
    {

        float patrolMul = StageMul(patrolSpeedMul);
        float chaseMul = StageMul(chaseSpeedMul);
        float scanMul = StageMul(scanDistanceMul);
        float extraSearch = CurrentStage >= 2 ? 2f : CurrentStage >= 1 ? 1f : 0f;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyAI3D enemy = enemies[i];
            if (enemy == null) continue;

            if (!enemyBases.TryGetValue(enemy, out EnemyBase b))
            {
                b = new EnemyBase
                {
                    PatrolSpeed = enemy.patrolSpeed,
                    ChaseSpeed = enemy.chaseSpeed,
                    ScanDistance = enemy.scanDistance,
                    SearchDuration = enemy.searchDuration,
                };
                enemyBases[enemy] = b;
            }

            enemy.patrolSpeed = b.PatrolSpeed * patrolMul;
            enemy.chaseSpeed = Mathf.Min(b.ChaseSpeed * chaseMul, maxChaseSpeed);
            enemy.scanDistance = b.ScanDistance * scanMul;
            enemy.searchDuration = b.SearchDuration + extraSearch;

            // Chỉ sửa FIELD công khai của enemy (script của người khác, không đụng
            // code trong đó): EnemyAI3D gốc tự set agent.speed từ field mỗi lúc
            // đổi state (Patrol/Chase/Search), nên tốc độ mới áp đúng khi enemy
            // chuyển khuynh hướng — không cần gọi hàm đặc biệt nào.
        }

        float hearingExtra = CurrentStage * 2f;
        for (int i = 0; i < hearings.Count; i++)
        {
            EnemyHearing hearing = hearings[i];
            if (hearing == null) continue;

            if (!hearingBases.TryGetValue(hearing, out float baseRadius))
            {
                baseRadius = hearing.hearingRadius;
                hearingBases[hearing] = baseRadius;
            }

            hearing.hearingRadius = baseRadius + hearingExtra;
        }
    }

    private float StageMul(Vector2 muls)
    {
        return CurrentStage >= 2 ? muls.y : CurrentStage >= 1 ? muls.x : 1f;
    }

    private void RefindEnemies()
    {
        enemies.Clear();
        hearings.Clear();
        enemies.AddRange(FindObjectsByType<EnemyAI3D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        hearings.AddRange(FindObjectsByType<EnemyHearing>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
    }

    // --------------------------------------------------- bao truoc khi len stage

    private void AnnounceStage(int stage)
    {
        string key = stage >= 2 ? stageSoundKey2 : stageSoundKey1;
        if (!string.IsNullOrEmpty(key) && AudioManager.Instance != null)
        {
            Vector3 position = PlayerPosition();
            if (position != Vector3.zero) AudioManager.Instance.TryPlayAt(key, position);
            else AudioManager.Instance.TryPlay(key);
        }

        if (flickerRadius > 0f) StartCoroutine(FlickerRoutine());
    }

    private void TryQuarterSms()
    {
        if (smsQuarterSent) return;
        if (DebtTracker.UncappedPaidMillions < smsDebtReportThreshold) return;
        smsQuarterSent = true;

        PhoneUIManager phone = FindFirstObjectByType<PhoneUIManager>();
        if (phone == null) return;

        // SMS giữa lượt: chủ nợ vừa nhắc nợ vừa đe dọa — báo trước cho người
        // chơi rằng nhà đang trở nên nguy hiểm hơn.
        phone.PushSMS("Chủ nợ", "Nửa kìa. Còn cái nửa nữa, đừng nghĩ xong chậm mà tao tha. Trong nhà đang có động tĩnh gì đó tăng dần đấy.");
    }

    // --------------------------------------------------- den cham cham

    private IEnumerator FlickerRoutine()
    {
        Vector3 origin = PlayerPosition();
        if (origin == Vector3.zero) yield break; // chưa tìm được player, bỏ nhịp này
        Light[] all = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        List<Light> affected = new List<Light>();
        List<float> baseIntensities = new List<float>();

        for (int i = 0; i < all.Length; i++)
        {
            Light light = all[i];
            if (light == null || light.type == LightType.Directional) continue;
            if (Vector3.Distance(light.transform.position, origin) > flickerRadius) continue;

            affected.Add(light);
            baseIntensities.Add(light.intensity);
        }

        float elapsed = 0f;
        while (elapsed < 1.2f)
        {
            elapsed += Time.unscaledDeltaTime;
            for (int i = 0; i < affected.Count; i++)
            {
                Light light = affected[i];
                if (light == null) continue; // scene doi day => light chet, bo qua

                float phase = 0.65f + 0.35f * Mathf.Sin(elapsed * 17f + i * 2.7f);
                light.intensity = baseIntensities[i] * phase;
            }
            yield return null;
        }

        // Khoi phuc nguyen chinh xac (light chet giua chung thi bo qua an toan).
        for (int i = 0; i < affected.Count; i++)
        {
            if (affected[i] != null) affected[i].intensity = baseIntensities[i];
        }
    }

    private static Vector3 PlayerPosition()
    {
        PlayerGrab grab = FindFirstObjectByType<PlayerGrab>();
        return grab != null ? grab.transform.position : Vector3.zero;
    }
}
