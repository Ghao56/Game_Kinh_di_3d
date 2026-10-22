using System;
using System.Collections.Generic;
using UnityEngine;
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

                EnemyAI3D enemyAI = t.instance.GetComponent<EnemyAI3D>();
                if (enemyAI != null)
                    enemyAI.player = player;

                EnemyHearing enemyHearing = t.instance.GetComponent<EnemyHearing>();
                if (enemyHearing != null)
                    enemyHearing.player = player;

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

        // Chọn góc ngẫu nhiên, luôn cách player đúng spawnDistance (không bao giờ trùng vị trí player)
        float a = UnityEngine.Random.value * Mathf.PI * 2f;
        Vector3 offset = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * t.spawnDistance;
        return player.position + offset;
    }

    [ContextMenu("Test: +30")]
    void TestAdd() => Add(30f);

    [ContextMenu("Test: về 0")]
    void TestZero() => Add(-maxAnxiety);
}