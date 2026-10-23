using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

[Serializable]
public class AnxietyThreshold
{
    public string name;
    [Range(0, 100)] public float triggerAt = 50f;   // mức lo âu để xuất hiện (phải lớn hơn 0)
    [Tooltip("Kéo PREFAB từ cửa sổ Project, không kéo object trong Hierarchy")]
    public GameObject prefab;
    public Transform spawnPoint;                     // để trống = spawn ngẫu nhiên quanh player
    public float spawnDistance = 8f;                 // dùng khi không có spawnPoint
    public bool despawnWhenBelow = true;             // lo âu tụt xuống thì biến mất
    public bool onlyOnce = false;                    // chỉ xuất hiện 1 lần

    [NonSerialized] public GameObject instance;
    [NonSerialized] public bool used;
}

public class AnxietyManager : MonoBehaviour
{
    public static AnxietyManager Instance { get; private set; }

    Transform player;

    [Header("Thanh lo âu")]
    [SerializeField] float maxAnxiety = 100f;
    [SerializeField, Range(0, 100)] float current;
    [SerializeField] float decayPerSecond = 2f;      // tự giảm khi không bị tác động
    [SerializeField] float decayDelay = 3f;          // sau bao lâu không tăng thì bắt đầu giảm

    [Header("Mốc xuất hiện vật thể")]
    public List<AnxietyThreshold> thresholds = new();

    [Header("Nguồn lo âu (âm thanh, vật phẩm...) - tự đăng ký")]
    [SerializeField] List<AnxietySource> sources = new();

    [Header("Quái sau khi spawn")]
    [SerializeField] bool addColliderOnSpawn = true;    // tự thêm Collider nếu quái chưa có Collider thật
    [SerializeField] bool addWallGuardOnSpawn = true;   // gắn EnemyWallGuard để chặn quái đi xuyên tường

    [Header("Gỡ lỗi")]
    [SerializeField] bool debugLog = false;          // in trạng thái và log spawn/despawn ra Console

    public UnityEvent<float> onChanged;              // truyền giá trị 0..1 (cho UI, hiệu ứng)

    float lastIncreaseTime;
    float nextCheck, nextStateLog;

    public Transform Player => player;
    public float Current => current;
    public float Normalized => current / maxAnxiety;

    void Awake()
    {
        Instance = this;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
            player = playerObject.transform;
        else
            Debug.LogError("Không tìm thấy GameObject có Tag Player!");
    }

    void Start()
    {
        foreach (var t in thresholds)
        {
            if (t.prefab == null)
            {
                Debug.LogWarning($"AnxietyManager: mốc '{t.name}' chưa có Prefab.", this);
                continue;
            }
            // Object nằm trong scene (không phải prefab trong Project) => bản gốc sẽ đứng sẵn trong scene
            if (t.prefab.scene.IsValid())
                Debug.LogWarning($"AnxietyManager: mốc '{t.name}' đang dùng object trong Hierarchy. " +
                                 "Hãy kéo PREFAB từ cửa sổ Project vào ô Prefab.", this);
            if (t.triggerAt <= 0f)
                Debug.LogWarning($"AnxietyManager: mốc '{t.name}' có Trigger At = 0, hãy đặt lớn hơn 0.", this);
            if (!t.despawnWhenBelow)
                Debug.LogWarning($"AnxietyManager: mốc '{t.name}' đang TẮT Despawn When Below nên quái sẽ không biến mất.", this);
            if (t.spawnPoint == null && t.spawnDistance < 2f)
                Debug.LogWarning($"AnxietyManager: mốc '{t.name}' có Spawn Distance = {t.spawnDistance}, quái sẽ hiện sát người chơi. Nên đặt 8 trở lên.", this);
        }
    }

    public void Register(AnxietySource s) { if (!sources.Contains(s)) sources.Add(s); }
    public void Unregister(AnxietySource s) => sources.Remove(s);

    /// Cộng tức thời (jump scare, nhặt đồ...). Âm = giảm.
    public void Add(float amount)
    {
        if (amount > 0) lastIncreaseTime = Time.time;
        current = Mathf.Clamp(current + amount, 0, maxAnxiety);
        onChanged?.Invoke(Normalized);
        CheckThresholds();
    }

    void Update()
    {
        // Chưa tìm thấy Player thì không chạy, tránh lỗi NullReference mỗi frame
        if (player == null) return;

        // tổng từ các nguồn
        float gain = 0f;
        for (int i = sources.Count - 1; i >= 0; i--)
        {
            if (sources[i] == null) { sources.RemoveAt(i); continue; }
            gain += sources[i].Evaluate(player.position, Time.deltaTime);
        }
        if (gain != 0f) Add(gain);

        // tự giảm
        if (Time.time - lastIncreaseTime > decayDelay && current > 0f)
            Add(-decayPerSecond * Time.deltaTime);

        // Kiểm tra mốc định kỳ, phòng khi current đổi mà không qua Add()
        // (sửa tay trong Inspector, hoặc current đứng ở 0 nên không còn gọi Add)
        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + 0.2f;
            CheckThresholds();
        }

        if (debugLog && Time.time >= nextStateLog)
        {
            nextStateLog = Time.time + 1f;
            foreach (var t in thresholds)
                Debug.Log($"[Anxiety] current={current:F1} | '{t.name}': trigger={t.triggerAt}, " +
                          $"quái đang tồn tại={(t.instance != null)}, despawnWhenBelow={t.despawnWhenBelow}", this);
        }
    }

    void CheckThresholds()
    {
        foreach (var t in thresholds)
        {
            if (t.prefab == null) continue;

            // Xuất hiện: đạt mốc và lo âu phải lớn hơn 0
            bool shouldSpawn = current > 0f && current >= t.triggerAt;
            // Biến mất: về 0 hoặc tụt xuống dưới mốc
            bool shouldDespawn = current <= 0f || current < t.triggerAt;

            if (shouldSpawn && t.instance == null && !(t.onlyOnce && t.used))
            {
                t.instance = Instantiate(
                    t.prefab,
                    GetSpawnPos(t),
                    Quaternion.identity
                );

                PrepareEnemy(t.instance);

                EnemyAI3D enemyAI = t.instance.GetComponent<EnemyAI3D>();
                if (enemyAI != null)
                    enemyAI.player = player;

                EnemyHearing enemyHearing = t.instance.GetComponent<EnemyHearing>();
                if (enemyHearing != null)
                    enemyHearing.player = player;

                NavMeshAgent agent = t.instance.GetComponent<NavMeshAgent>();
                if (agent != null && !agent.isOnNavMesh)
                    Debug.LogWarning($"AnxietyManager: quái '{t.name}' có NavMeshAgent nhưng đang KHÔNG nằm trên NavMesh " +
                                     "(cần bake NavMesh hoặc đặt spawn point trên NavMesh).", this);

                t.used = true;
                if (debugLog) Debug.Log($"[Anxiety] SPAWN '{t.name}' (current={current:F1})", this);
            }
            else if (shouldDespawn && t.despawnWhenBelow && t.instance != null)
            {
                Destroy(t.instance);
                t.instance = null;
                if (debugLog) Debug.Log($"[Anxiety] DESPAWN '{t.name}' (current={current:F1})", this);
            }
        }
    }

    Vector3 GetSpawnPos(AnxietyThreshold t)
    {
        if (t.spawnPoint != null) return t.spawnPoint.position;

        const int attempts = 12;
        const float obstacleRadius = 0.4f;

        // Lượt 1: chỉ chọn điểm nằm trên NavMesh và không bị vật đặc (tường, đồ vật) chiếm chỗ
        for (int i = 0; i < attempts; i++)
        {
            Vector3 candidate = RandomPointAround(t.spawnDistance);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas) &&
                !Physics.CheckSphere(hit.position + Vector3.up, obstacleRadius, ~0, QueryTriggerInteraction.Ignore))
                return hit.position;
        }

        // Lượt 2 (scene chưa bake NavMesh): dò mặt đất bằng raycast, tránh điểm nằm trong vật đặc
        for (int i = 0; i < attempts; i++)
        {
            Vector3 candidate = RandomPointAround(t.spawnDistance);
            if (Physics.Raycast(candidate + Vector3.up * 5f, Vector3.down, out RaycastHit ground, 20f, ~0, QueryTriggerInteraction.Ignore) &&
                !Physics.CheckSphere(ground.point + Vector3.up, obstacleRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                Debug.LogWarning($"AnxietyManager: không tìm được điểm spawn trên NavMesh cho '{t.name}'. " +
                                 "Hãy bake NavMesh, nếu không quái có thể đi xuyên tường.", this);
                return ground.point;
            }
        }

        // Cuối cùng: dùng điểm ngẫu nhiên đầu tiên
        Debug.LogWarning($"AnxietyManager: không tìm được điểm spawn hợp lệ cho '{t.name}'.", this);
        return RandomPointAround(t.spawnDistance);
    }

    // Điểm ngẫu nhiên trên vòng tròn quanh người chơi, luôn cách đúng distance
    Vector3 RandomPointAround(float distance)
    {
        float a = UnityEngine.Random.value * Mathf.PI * 2f;
        Vector3 offset = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * distance;
        return player.position + offset;
    }

    // Chuẩn bị quái vừa clone: thêm Collider và EnemyWallGuard nếu cần
    void PrepareEnemy(GameObject enemy)
    {
        if (addColliderOnSpawn) EnsureCollider(enemy);

        if (addWallGuardOnSpawn &&
            enemy.GetComponent<NavMeshAgent>() != null &&
            enemy.GetComponent<EnemyWallGuard>() == null)
            enemy.AddComponent<EnemyWallGuard>();
    }

    // Nếu quái chưa có Collider thật (không phải Trigger) thì thêm CapsuleCollider vừa với model
    void EnsureCollider(GameObject enemy)
    {
        foreach (var c in enemy.GetComponentsInChildren<Collider>())
            if (!c.isTrigger) return;

        Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

        Vector3 scale = enemy.transform.lossyScale;
        float sx = Mathf.Max(Mathf.Abs(scale.x), 0.0001f);
        float sy = Mathf.Max(Mathf.Abs(scale.y), 0.0001f);

        var cap = enemy.AddComponent<CapsuleCollider>();
        cap.center = enemy.transform.InverseTransformPoint(b.center);
        cap.radius = Mathf.Min(b.extents.x, b.extents.z) / sx;
        cap.height = Mathf.Max(b.size.y / sy, cap.radius * 2f);

        if (debugLog) Debug.Log($"[Anxiety] Đã thêm CapsuleCollider cho '{enemy.name}'", this);
    }

    [ContextMenu("Test: +30")]
    void TestAdd() => Add(30f);

    [ContextMenu("Test: về 0")]
    void TestZero() => Add(-maxAnxiety);
}