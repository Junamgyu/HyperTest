using UnityEngine;

/// <summary>
/// 적이 발사하는 총알.
/// 플레이어 태그를 가진 오브젝트에 닿으면 데미지를 줍니다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class EnemyBullet : MonoBehaviour
{
    [SerializeField] private float _lifetime = 5f;

    private Rigidbody _rb;
    private Collider  _col;
    private float     _damage;

    private void Awake()
    {
        _rb            = GetComponent<Rigidbody>();
        _col           = GetComponent<Collider>();
        _rb.isKinematic = false;   // 프리팹 설정과 무관하게 항상 물리 이동 가능하게
        _rb.useGravity  = false;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    public void Initialize(Vector3 direction, float speed, float damage, Collider ignoreCollider = null)
    {
        _damage            = damage;
        _rb.linearVelocity = direction.normalized * speed;
        transform.rotation = Quaternion.LookRotation(direction);
        Destroy(gameObject, _lifetime);

        // 발사한 적의 콜라이더와 충돌 무시 (자기충돌 방지)
        if (ignoreCollider != null)
        {
            Collider myCollider = GetComponent<Collider>();
            if (myCollider != null)
                Physics.IgnoreCollision(myCollider, ignoreCollider);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            // TODO: 플레이어 데미지 처리
            // collision.gameObject.GetComponent<PlayerHealth>()?.TakeDamage(_damage);
            Debug.Log($"[EnemyBullet] 플레이어에게 {_damage} 데미지!");
        }

        Destroy(gameObject);
    }
}