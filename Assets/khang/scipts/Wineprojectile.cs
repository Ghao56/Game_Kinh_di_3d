using UnityEngine;

// Script RIÊNG cho giọt/chai rượu khi bay ra - tách biệt với SaltBagProjectile
// để sau này thêm hiệu ứng riêng cho rượu (khác với hiệu ứng của muối) mà không
// phải đụng vào hay ảnh hưởng tới logic của muối.
public class WineProjectile : MonoBehaviour
{
    public float lifeTime = 5f;

    // Thời gian hiệu ứng (choáng) khi trúng quái - có thể chỉnh khác với muối
    public float effectDuration = 3f;

    public string enemyTag = "Enemy";

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    void OnCollisionEnter(Collision collision)
    {
        bool hitEnemy =
            collision.transform.CompareTag(enemyTag) ||
            collision.transform.root.CompareTag(enemyTag);

        if (hitEnemy)
        {
            Debug.Log("RƯỢU TRÚNG QUÁI!");

            IStunnable stunnable = collision.transform.GetComponentInParent<IStunnable>();

            if (stunnable != null)
                stunnable.Stun(effectDuration);

            // TODO: chỗ này sau này thêm hiệu ứng RIÊNG của rượu
            // (ví dụ hiệu ứng khác choáng thường, để lại vũng rượu, bắt lửa nếu gần nguồn lửa...)

            Destroy(gameObject);
        }

        // Va chạm vật khác (bàn, sàn, tường...) thì để rơi/nằm lại tự nhiên,
        // chỉ huỷ khi hết lifeTime.
    }
}