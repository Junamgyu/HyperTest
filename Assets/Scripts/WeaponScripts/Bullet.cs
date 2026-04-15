using UnityEngine;

/// <summary>
/// 총알 동작 스크립트.
/// GunController에서 Instantiate 후 Initialize()를 호출해 초기화합니다.
/// 
/// 나중에 적 데미지 처리는 OnTriggerEnter/OnCollisionEnter에서 구현 예정.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Bullet : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Bullet Settings")]
    [SerializeField] private float _lifetime        = 3f;    // 자동 소멸 시간
    [SerializeField] private float _damage          = 10f;   // 데미지 (추후 사용)
    [SerializeField] private LayerMask _hitMask;             // 맞출 레이어

    // ─── Runtime State ────────────────────────────────────────────────────────
    private Rigidbody _rb;
    private bool      _isInitialized = false;

    public float Damage => _damage;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    private void Awake()
    {
        _rb            = GetComponent<Rigidbody>();
        _rb.useGravity = false;         // 총알은 중력 무시 (직선 비행)
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // 관통 방지
    }

    private void Start()
    {
        // Initialize가 호출 안 됐을 경우 안전하게 소멸
        if (!_isInitialized)
            Destroy(gameObject, _lifetime);
    }

    // ─── API ──────────────────────────────────────────────────────────────────
    /// <summary>GunController에서 Instantiate 직후 호출합니다.</summary>
    public void Initialize(Vector3 direction, float speed)
    {
        _isInitialized    = true;
        _rb.linearVelocity = direction.normalized * speed;

        // 총알 방향으로 회전
        transform.rotation = Quaternion.LookRotation(direction);

        Destroy(gameObject, _lifetime);
    }

    // ─── Collision ────────────────────────────────────────────────────────────
    private void OnCollisionEnter(Collision collision)
    {
        // _hitMask에 포함된 레이어만 처리
        if ((_hitMask.value & (1 << collision.gameObject.layer)) == 0) return;

        if (collision.gameObject.TryGetComponent<EnemyBase>(out var enemy))
        enemy.TakeDamage(_damage);

        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if ((_hitMask.value & (1 << other.gameObject.layer)) == 0) return;

        if (other.TryGetComponent<EnemyBase>(out var enemy))
        enemy.TakeDamage(_damage);
        
        Destroy(gameObject);
    }
}