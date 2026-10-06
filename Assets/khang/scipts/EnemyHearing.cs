using UnityEngine;

// Script riêng phụ trách "nghe tiếng bước chân" của player.
// Gắn chung với EnemyAI3D trên cùng 1 object. EnemyAI3D chỉ cần đọc
// thuộc tính HeardPlayer, không cần biết cách tính bên trong.
//
// Cơ chế: phát hiện NGAY LẬP TỨC khi player đang di chuyển và lọt vào
// bán kính hearingRadius - không cần tích luỹ dần như bản trước.
public class EnemyHearing : MonoBehaviour
{
    public Transform player;

    // Bán kính có thể nghe được tiếng bước chân của player
    public float hearingRadius = 8f;

    // Tốc độ di chuyển tối thiểu của player để được tính là đang gây tiếng động
    public float minPlayerSpeedToMakeNoise = 0.2f;

    private Vector3 lastPlayerPosition;

    // EnemyAI3D đọc giá trị này mỗi frame để biết có nên chuyển sang Chase không
    public bool HeardPlayer { get; private set; }

    // Vị trí cuối cùng NGHE thấy player (chỉ cập nhật khi HeardPlayer == true)
    public Vector3 LastHeardPosition { get; private set; }

    void Start()
    {
        if (player != null)
            lastPlayerPosition = player.position;
    }

    void Update()
    {
        if (player == null)
        {
            HeardPlayer = false;
            return;
        }

        float playerSpeed =
            (player.position - lastPlayerPosition).magnitude /
            Mathf.Max(Time.deltaTime, 0.0001f);

        lastPlayerPosition = player.position;

        float distance =
            Vector3.Distance(transform.position, player.position);

        // Phát hiện ngay lập tức: trong bán kính + đang di chuyển đủ nhanh
        HeardPlayer =
            distance <= hearingRadius &&
            playerSpeed >= minPlayerSpeedToMakeNoise;

        if (HeardPlayer)
            LastHeardPosition = player.position;
    }

    // Giữ lại để tương thích với EnemyAI3D (không còn ý nghĩa tích luỹ nữa,
    // nhưng gọi vẫn an toàn, không gây lỗi).
    public void ResetMeter()
    {
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.5f);

        int segments = 36;

        Vector3 previousPoint =
            transform.position +
            new Vector3(hearingRadius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = (360f / segments) * i;

            Vector3 point =
                transform.position +
                Quaternion.Euler(0f, angle, 0f) *
                new Vector3(hearingRadius, 0f, 0f);

            Gizmos.DrawLine(previousPoint, point);

            previousPoint = point;
        }
    }
}