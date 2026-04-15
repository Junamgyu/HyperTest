using UnityEngine;

/// <summary>
/// 돌진 자폭 적.
/// 플레이어를 빠르게 추적하다가 폭발 범위 내 들어오면 자폭합니다.
/// </summary>
public class SuicideEnemy : EnemyBase
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Suicide Settings")]
    [SerializeField] private float _explosionRadius  = 3f;    // 자폭 범위
    [SerializeField] private float _explosionDamage  = 80f;   // 자폭 데미지
    [SerializeField] private float _explodeDistance  = 1.5f;  // 자폭 트리거 거리
    [SerializeField] private float _rushSpeedBoost   = 1.5f;  // 가까워질수록 속도 증가 배율

    [Header("VFX")]
    [SerializeField] private GameObject _explosionEffectPrefab; // 폭발 이펙트

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        _moveSpeed = 7f; // 기본보다 빠르게
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();   // 커스텀 중력 + 재정렬
        if (_isDead || _playerTransform == null) return;

        float dist = DistanceToPlayer();

        if (dist <= _explodeDistance)
            Explode();
        else
            Rush(dist);
    }

    // ─── Rush ─────────────────────────────────────────────────────────────────
    private void Rush(float dist)
    {
        // 가까울수록 더 빠르게 (최대 _rushSpeedBoost배)
        float speedMultiplier = Mathf.Lerp(_rushSpeedBoost, 1f,
                                    Mathf.Clamp01(dist / 10f));
        MoveToward(_playerTransform.position, _moveSpeed * speedMultiplier);
    }

    // ─── Explode ──────────────────────────────────────────────────────────────
    private static readonly Collider[] _explosionBuffer = new Collider[16];

    private void Explode()
    {
        // 폭발 범위 내 플레이어에게 데미지 (NonAlloc으로 GC 부담 없음)
        int count = Physics.OverlapSphereNonAlloc(transform.position, _explosionRadius, _explosionBuffer);
        for (int i = 0; i < count; i++)
        {
            if (_explosionBuffer[i].CompareTag("Player"))
            {
                // TODO: 플레이어 데미지 처리
                // _explosionBuffer[i].GetComponent<PlayerHealth>()?.TakeDamage(_explosionDamage);
                Debug.Log($"[SuicideEnemy] 플레이어에게 {_explosionDamage} 데미지!");
            }
        }

        // 폭발 이펙트 생성
        if (_explosionEffectPrefab != null)
            Instantiate(_explosionEffectPrefab, transform.position, Quaternion.identity);

        Die();
    }

    protected override void OnDeath()
    {
        // 자폭이 아닌 총에 맞아 죽을 때도 작은 폭발
        if (_explosionEffectPrefab != null)
            Instantiate(_explosionEffectPrefab, transform.position, Quaternion.identity);
    }

    // ─── Gizmo ────────────────────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _explodeDistance);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, _explosionRadius);
    }
}