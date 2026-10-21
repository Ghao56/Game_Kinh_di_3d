using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class AnxietyThreshold
{
    public string name;
    [Range(0, 100)] public float triggerAt = 50f;   // mức lo âu để xuất hiện
    public GameObject prefab;                        // chỉ cần kéo prefab vào đây
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

    [Header("Thanh lo âu")]
    Transform player;
    [SerializeField] float maxAnxiety = 100f;
    [SerializeField, Range(0, 100)] float current;
    [SerializeField] float decayPerSecond = 2f;      // tự giảm khi không bị tác động
    [SerializeField] float decayDelay = 3f;          // sau bao lâu không tăng thì bắt đầu giảm

    [Header("Mốc xuất hiện vật thể")]
    public List<AnxietyThreshold> thresholds = new();

    [Header("Nguồn lo âu (âm thanh, vật phẩm...) - tự đăng ký")]
    [SerializeField] List<AnxietySource> sources = new();

    public UnityEvent<float> onChanged;              // truyền giá trị 0..1 (cho UI, hiệu ứng)

    float lastIncreaseTime;

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
        // Chưa kéo Player vào thì không chạy, tránh lỗi NullReference mỗi frame
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
    }

    void CheckThresholds()
    {
        foreach (var t in thresholds)
        {
            if (t.prefab == null) continue;

            bool above = current >= t.triggerAt;

            if (above && t.instance == null && !(t.onlyOnce && t.used))
            {
                t.instance = Instantiate(
                    t.prefab,
                    GetSpawnPos(t),
                    Quaternion.identity
                );

                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

                if (playerObject != null)
                {
                    Transform playerTransform = playerObject.transform;

                    EnemyAI3D enemyAI = t.instance.GetComponent<EnemyAI3D>();
                    if (enemyAI != null)
                        enemyAI.player = playerTransform;

                    EnemyHearing enemyHearing = t.instance.GetComponent<EnemyHearing>();
                    if (enemyHearing != null)
                        enemyHearing.player = playerTransform;
                }
                else
                {
                    Debug.LogError("Không tìm thấy Player có Tag Player!");
                }

                t.used = true;
            }
            else if (!above && t.despawnWhenBelow && t.instance != null)
            {
                Destroy(t.instance);
                t.instance = null;
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
}