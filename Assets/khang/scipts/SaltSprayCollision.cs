using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;

// Gắn script này vào object Particle System dùng làm "chùm hạt muối".
// Particle System đó CẦN:
//  - Module Collision: bật, Type = World, "Send Collision Messages" = true
//    (nếu không tick cái này, Unity sẽ KHÔNG gọi OnParticleCollision()).
// Script tự huỷ object sau "lifeTime" giây, và đảm bảo 1 quái chỉ bị Stun()
// MỘT LẦN duy nhất trong 1 lần phun dù có hàng chục hạt cùng trúng nó.
[RequireComponent(typeof(ParticleSystem))]
public class SaltSprayCollision : MonoBehaviour
{
    // Thời gian quái bị choáng khi trúng muối
    public float stunDuration = 3f;

    public string enemyTag = "Enemy";

    // Tự huỷ object này sau chừng này giây - nên đặt lớn hơn "Start Lifetime"
    // của Particle System một chút để hạt kịp bay/biến mất hết trước khi huỷ.
    public float lifeTime = 3f;

    // Tránh gọi Stun() nhiều lần lên cùng 1 quái trong cùng 1 lần phun
    private HashSet<GameObject> alreadyHit = new HashSet<GameObject>();

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    //void OnCollisionEnter(Collision collision)
    //{
    //    bool hitEnemy =
    //        collision.transform.CompareTag(enemyTag) ||
    //        collision.transform.root.CompareTag(enemyTag);

    //    if (!hitEnemy)
    //        return;

    //    Debug.Log("NÉM TRÚNG QUÁI!");

    //    IStunnable stunnable =
    //        collision.transform.GetComponentInParent<IStunnable>();

    //    if (stunnable != null)
    //        stunnable.Stun(stunDuration);

    //    ParticleSystem ps = GetComponent<ParticleSystem>();

    //    if (ps != null)
    //    {
    //        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    //    }

    //    Destroy(gameObject);
    //}
    private void OnTriggerEnter(Collider other)
    {
        bool hitEnemy =
            other.transform.CompareTag(enemyTag) ||
            other.transform.root.CompareTag(enemyTag);
        if (!hitEnemy)
            return;
        Debug.Log("NÉM TRÚNG QUÁI!");
        IStunnable stunnable =
            other.transform.GetComponentInParent<IStunnable>();
        if (stunnable != null)
            stunnable.Stun(stunDuration);
        ParticleSystem ps = GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        Destroy(gameObject);

    }
}