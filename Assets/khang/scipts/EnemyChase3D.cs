using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI3D : MonoBehaviour, IStunnable
{
    public Transform player;

    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;

    // Bán kính chọn điểm tuần tra ngẫu nhiên quanh vị trí hiện tại
    public float patrolRadius = 10f;

    // Vùng quét bằng mắt - giữ nguyên logic
    // scanAngle = 360 nghĩa là quét toàn bộ quanh enemy (không cần quay mặt về hướng player).
    public float scanDistance = 15f;
    public float scanAngle = 360f;

    [Header("Tìm kiếm khi mất dấu bằng mắt")]
    // Thời gian (giây) enemy đứng lại tìm kiếm tại vị trí cuối cùng thấy player
    // trước khi bỏ cuộc và quay về tuần tra.
    public float searchDuration = 4f;

    private enum State
    {
        Patrol,
        Chase,
        Search,
        Stunned
    }

    private State currentState = State.Patrol;

    private NavMeshAgent agent;

    // Script riêng phụ trách nghe tiếng bước chân - có thể không gắn (để null) nếu chưa cần tính năng này
    private EnemyHearing hearing;

    private float patrolTimer;
    private const float patrolInterval = 3f;

    // Vị trí cuối cùng phát hiện player (bằng mắt hoặc bằng tai), dùng khi cần đi tìm
    private Vector3 lastKnownPosition;

    // true nếu lần phát hiện gần nhất vẫn còn hiệu lực (thấy hoặc nghe), dùng để quyết định có Search hay không
    private bool hadValidDetection = false;

    private float searchTimer;

    // Thời gian choáng còn lại (giây), chỉ có ý nghĩa khi currentState == Stunned
    private float stunTimer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = patrolSpeed;

        hearing = GetComponent<EnemyHearing>();

        SetNewPatrolDestination();
        patrolTimer = patrolInterval;
    }

    // Gọi từ bên ngoài (ví dụ SaltBagProjectile) để làm enemy choáng trong duration giây.
    // Trong lúc choáng enemy đứng yên, không nhìn, không nghe, không đuổi.
    public void Stun(float duration)
    {
        Debug.Log($"ENEMY BỊ CHOÁNG {duration}s!");

        currentState = State.Stunned;
        stunTimer = duration;

        agent.isStopped = true;
        agent.velocity = Vector3.zero;
    }

    void Update()
    {
        // Xử lý choáng trước tiên, không phụ thuộc vào player
        if (currentState == State.Stunned)
        {
            stunTimer -= Time.deltaTime;

            if (stunTimer <= 0f)
            {
                Debug.Log("ENEMY HẾT CHOÁNG!");

                agent.isStopped = false;
                agent.speed = patrolSpeed;

                // Quay về Patrol, nếu player vẫn trong tầm thấy/nghe thì các frame sau
                // sẽ tự chuyển lại sang Chase
                currentState = State.Patrol;
                hadValidDetection = false;

                SetNewPatrolDestination();
                patrolTimer = patrolInterval;
            }

            return;
        }

        if (player == null)
            return;

        bool seenPlayer = CheckScan();
        bool heardPlayer = hearing != null && hearing.HeardPlayer;

        bool playerDetected = seenPlayer || heardPlayer;

        // Ghi nhận vị trí cuối cùng phát hiện được (ưu tiên bằng mắt vì chính xác hơn,
        // nếu chỉ nghe thấy thì dùng vị trí nghe được)
        if (seenPlayer)
        {
            lastKnownPosition = player.position;
            hadValidDetection = true;
        }
        else if (heardPlayer && hearing != null)
        {
            lastKnownPosition = hearing.LastHeardPosition;
            hadValidDetection = true;
        }

        if (currentState == State.Patrol)
        {
            if (playerDetected)
            {
                Debug.Log(seenPlayer
                    ? "ĐÃ QUÉT ĐƯỢC PLAYER!"
                    : "NGHE THẤY TIẾNG BƯỚC CHÂN!");

                currentState = State.Chase;
                agent.speed = chaseSpeed;
            }
            else
            {
                Patrol();
            }
        }
        else if (currentState == State.Chase)
        {
            if (playerDetected)
            {
                // NavMeshAgent tự tính đường ngắn nhất tới player mỗi frame
                ChasePlayer();
            }
            else if (hadValidDetection)
            {
                // Vừa mất dấu (do nhìn hoặc do nghe) -> tính đường ngắn nhất
                // tới vị trí cuối cùng phát hiện được để tìm kiếm
                Debug.Log("MẤT DẤU - ĐI TỚI VỊ TRÍ CUỐI CÙNG PHÁT HIỆN ĐỂ TÌM");

                currentState = State.Search;
                agent.SetDestination(lastKnownPosition);
                searchTimer = searchDuration;

                hadValidDetection = false;
            }
            else
            {
                // Trường hợp hiếm: chưa từng có phát hiện hợp lệ nào -> về thẳng Patrol
                Debug.Log("MẤT DẤU PLAYER - QUAY VỀ TUẦN TRA");

                currentState = State.Patrol;
                agent.speed = patrolSpeed;

                SetNewPatrolDestination();
                patrolTimer = patrolInterval;
            }
        }
        else if (currentState == State.Search)
        {
            if (playerDetected)
            {
                // Tìm lại được player trong lúc đang tìm kiếm -> đuổi tiếp ngay
                Debug.Log("TÌM LẠI ĐƯỢC PLAYER!");

                currentState = State.Chase;
                agent.speed = chaseSpeed;
            }
            else
            {
                searchTimer -= Time.deltaTime;

                if (searchTimer <= 0f)
                {
                    // Tìm không thấy sau searchDuration giây -> bỏ cuộc, về tuần tra
                    Debug.Log("KHÔNG TÌM THẤY PLAYER - QUAY VỀ TUẦN TRA");

                    currentState = State.Patrol;
                    agent.speed = patrolSpeed;

                    SetNewPatrolDestination();
                    patrolTimer = patrolInterval;
                }
            }
        }
    }

    void Patrol()
    {
        patrolTimer -= Time.deltaTime;

        bool reachedDestination =
            !agent.pathPending &&
            agent.remainingDistance <= agent.stoppingDistance;

        if (patrolTimer <= 0f || reachedDestination)
        {
            SetNewPatrolDestination();
            patrolTimer = patrolInterval;
        }
    }

    void SetNewPatrolDestination()
    {
        Vector3 randomOffset =
            UnityEngine.Random.insideUnitSphere * patrolRadius;

        randomOffset.y = 0f;

        Vector3 candidatePosition =
            transform.position + randomOffset;

        if (NavMesh.SamplePosition(
                candidatePosition,
                out NavMeshHit hit,
                patrolRadius,
                NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    void ChasePlayer()
    {
        // agent.SetDestination dùng thuật toán A* trên NavMesh
        // để tự tính quãng đường ngắn nhất tới player, tránh vật cản
        agent.SetDestination(player.position);
    }

    // Giữ nguyên logic quét bằng mắt (khoảng cách + góc + linecast) như bản gốc,
    // chỉ đổi từ việc tự set state sang trả về bool.
    bool CheckScan()
    {
        Vector3 direction =
            player.position - transform.position;

        direction.y = 0f;

        float distance =
            direction.magnitude;

        if (distance > scanDistance)
            return false;

        float angle =
            Vector3.Angle(
                transform.forward,
                direction.normalized
            );

        if (angle > scanAngle / 2f)
            return false;

        Vector3 origin =
            transform.position + Vector3.up;

        Vector3 target =
            player.position + Vector3.up;

        if (Physics.Linecast(
            origin,
            target,
            out RaycastHit hit
        ))
        {
            if (hit.transform == player ||
                hit.transform.root == player.root)
            {
                return true;
            }

            return false;
        }
        else
        {
            return true;
        }
    }

    void OnDrawGizmos()
    {
        Vector3 origin =
            transform.position + Vector3.up;

        Gizmos.color = Color.yellow;

        int segments = 36;

        // Giới hạn 360 để tránh vẽ chồng lặp khi scanAngle > 360
        float clampedAngle = Mathf.Min(scanAngle, 360f);

        float halfAngle = clampedAngle / 2f;

        Vector3 previousPoint =
            origin +
            Quaternion.Euler(
                0f,
                -halfAngle,
                0f
            ) *
            transform.forward *
            scanDistance;

        for (int i = 1; i <= segments; i++)
        {
            float angle =
                -halfAngle +
                (clampedAngle / segments) * i;

            Vector3 direction =
                Quaternion.Euler(
                    0f,
                    angle,
                    0f
                ) *
                transform.forward;

            Vector3 point =
                origin +
                direction *
                scanDistance;

            Gizmos.DrawLine(
                previousPoint,
                point
            );

            previousPoint = point;
        }

        // Nếu là 360° (vòng tròn kín) thì không cần vẽ 2 đường viền trái/phải
        if (clampedAngle < 360f)
        {
            Vector3 left =
                Quaternion.Euler(
                    0f,
                    -halfAngle,
                    0f
                ) *
                transform.forward;

            Vector3 right =
                Quaternion.Euler(
                    0f,
                    halfAngle,
                    0f
                ) *
                transform.forward;

            Gizmos.DrawLine(
                origin,
                origin + left * scanDistance
            );

            Gizmos.DrawLine(
                origin,
                origin + right * scanDistance
            );
        }

        if (player != null)
        {
            Gizmos.color = Color.green;

            Gizmos.DrawLine(
                origin,
                player.position
            );
        }

        // Vị trí cuối cùng thấy player (khi đang ở state Search)
        if (currentState == State.Search)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(lastKnownPosition, 0.5f);
        }
    }
}