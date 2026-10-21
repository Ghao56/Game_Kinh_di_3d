using UnityEngine;

public class EnemyJumpscareTrigger : MonoBehaviour
{
    private bool hasTriggered = false;

    // 1. Dùng cho Trigger Collider (nếu Collider trên Enemy có tích Is Trigger)
    private void OnTriggerEnter(Collider other)
    {
        CheckAndTrigger(other.gameObject);
    }

    // 2. Dùng cho Physical Collider
    private void OnCollisionEnter(Collision collision)
    {
        CheckAndTrigger(collision.gameObject);
    }

    // 3. Đặc trị riêng cho Player dùng CharacterController
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        CheckAndTrigger(hit.gameObject);
    }

    private void CheckAndTrigger(GameObject target)
    {
        if (hasTriggered) return;

        // Kiểm tra xem đối tượng va chạm có Tag "Player" hoặc tên chứa "Player" không
        if (target.CompareTag("Player") || target.name.Contains("Player"))
        {
            hasTriggered = true;
            Debug.Log("ENEMEY ĐÃ BẮT ĐƯỢC PLAYER -> KÍCH HOẠT JUMPSCARE!");

            if (JumpscareManager.Instance != null)
            {
                JumpscareManager.Instance.TriggerJumpscare();
            }
            else
            {
                // Tự động tìm nếu Instance bị null
                JumpscareManager manager = FindObjectOfType<JumpscareManager>();
                if (manager != null)
                {
                    manager.TriggerJumpscare();
                }
                else
                {
                    Debug.LogError("KHÔNG TÌM THẤY JUMPSCARE MANAGER TRONG SCENE!");
                }
            }
        }
    }
}