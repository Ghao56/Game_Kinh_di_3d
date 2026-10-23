using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Chặn quái đi xuyên tường. Được AnxietyManager tự gắn vào quái khi spawn.
/// Mỗi frame kiểm tra đoạn đường quái vừa đi có cắt qua collider đặc không;
/// nếu có thì đưa quái về điểm an toàn trước đó.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyWallGuard : MonoBehaviour
{
    [Tooltip("Độ cao (so với chân) của tia kiểm tra, nên bằng khoảng ngang eo quái")]
    [SerializeField] float checkHeight = 0.8f;
    [SerializeField] LayerMask blockMask = ~0;
    [SerializeField] bool debugLog = false;

    NavMeshAgent agent;
    Vector3 lastSafe;
    Transform playerRoot;
    bool warned;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        lastSafe = transform.position;

        if (AnxietyManager.Instance != null && AnxietyManager.Instance.Player != null)
            playerRoot = AnxietyManager.Instance.Player.root;
    }

    void LateUpdate()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        Vector3 now = transform.position;
        if ((now - lastSafe).sqrMagnitude < 0.0001f) return;

        Vector3 from = lastSafe + Vector3.up * checkHeight;
        Vector3 to = now + Vector3.up * checkHeight;

        if (Physics.Linecast(from, to, out RaycastHit hit, blockMask, QueryTriggerInteraction.Ignore)
            && !IsIgnored(hit.collider))
        {
            // Bước đi vừa rồi cắt qua tường: trả quái về điểm an toàn
            agent.Warp(lastSafe);
            agent.ResetPath();

            if (debugLog || !warned)
            {
                warned = true;
                Debug.LogWarning($"EnemyWallGuard: đã chặn quái '{name}' xuyên qua '{hit.collider.name}'. " +
                                 "NavMesh có thể đang phủ qua vật này, hãy bake lại hoặc dùng NavMeshModifier/Obstacle.", this);
            }
            return;
        }

        lastSafe = now;
    }

    bool IsIgnored(Collider c)
    {
        Transform r = c.transform.root;
        if (r == transform.root) return true;                       // collider của chính quái
        if (playerRoot != null && r == playerRoot) return true;     // người chơi
        if (c.GetComponentInParent<EnemyAI3D>() != null) return true; // quái khác
        return false;
    }
}