using UnityEngine;

// Gắn script này vào prefab viên túi muối bay ra.
// Prefab cần có Rigidbody và Collider (không tick Is Trigger, để va chạm vật lý thật).
public class SaltBagProjectile : MonoBehaviour
{
   

    // Thời gian (giây) quái bị choáng khi bị trúng muối
    public float stunDuration = 3f;

    // Tag của các loại quái (gán tag này cho mọi loại quái)
    public string enemyTag = "Enemy";

    void Start()
    {
       
    }

    void OnCollisionEnter(Collision collision)
    {
        // Nhận diện quái bằng tag (kiểm tra cả object trúng lẫn object gốc,
        // phòng khi Collider nằm ở object con còn tag đặt ở object cha)
        bool hitEnemy =
            collision.transform.CompareTag(enemyTag) ||
            collision.transform.root.CompareTag(enemyTag);

        if (hitEnemy)
        {
            Debug.Log("NÉM TRÚNG QUÁI!");

            // Mỗi loại quái tự quyết định cách phản ứng qua interface IStunnable
            IStunnable stunnable = collision.transform.GetComponentInParent<IStunnable>();

            if (stunnable != null)
                stunnable.Stun(stunDuration);
            else
                Debug.LogWarning("Quái có tag Enemy nhưng chưa implement IStunnable nên không bị choáng!");

            Destroy(gameObject);
        }

        // Va chạm với vật khác (bàn, sàn, tường...) thì để nó rơi/nằm lại
        // bình thường theo vật lý, KHÔNG huỷ ngay - chỉ huỷ khi hết lifeTime.
    }
}