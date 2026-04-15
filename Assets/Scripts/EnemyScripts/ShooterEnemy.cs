using System.Collections;
using UnityEngine;

/// <summary>
/// 원거리 사격 적.
/// 
/// 상태:
///   Approach  → 너무 멀면 플레이어에게 접근
///   Retreat   → 너무 가까우면 도망
///   Combat    → 적정 거리에서 사격
/// </summary>
public class ShooterEnemy : EnemyBase
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Distance Control")]
    [SerializeField] private float _preferredDistance = 15f;  // 유지하려는 거리
    [SerializeField] private float _tooCloseDistance  = 8f;   // 이 거리보다 가까우면 도망
    [SerializeField] private float _tooFarDistance    = 22f;  // 이 거리보다 멀면 접근
    [SerializeField] private float _retreatSpeed      = 6f;   // 도망 속도

    [Header("Combat")]
    [SerializeField] private float      _fireRate         = 1.5f;   // 발사 간격 (초)
    [SerializeField] private float      _bulletSpeed      = 20f;    // 총알 속도
    [SerializeField] private float      _bulletDamage     = 15f;    // 총알 데미지
    [SerializeField] private GameObject _bulletPrefab;               // 총알 프리팹
    [SerializeField] private Transform  _muzzlePoint;                // 총구 위치

    [Header("Line of Sight")]
    [SerializeField] private LayerMask  _obstacleMask;               // 시야 차단 레이어

    // ─── Runtime State ────────────────────────────────────────────────────────
    private enum State { Approach, Retreat, Combat }
    private State _state = State.Approach;
    private float _nextFireTime = 0f;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        _moveSpeed = 4f;
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        if (_isDead || _playerTransform == null) return;

        UpdateState();
        ExecuteState();
    }

    // ─── State Machine ────────────────────────────────────────────────────────
    private void UpdateState()
    {
        float dist = DistanceToPlayer();

        if (dist < _tooCloseDistance)
            _state = State.Retreat;
        else if (dist > _tooFarDistance)
            _state = State.Approach;
        else
            _state = State.Combat;
    }

    private void ExecuteState()
    {
        switch (_state)
        {
            case State.Approach:
                MoveToward(_playerTransform.position, _moveSpeed);
                break;

            case State.Retreat:
                Retreat();
                break;

            case State.Combat:
                StopHorizontalMovement();
                FacePlayer();
                TryFire();
                break;
        }
    }

    // ─── Retreat ──────────────────────────────────────────────────────────────
    private void Retreat()
    {
        Vector3 gravDir  = GravitySystem.Instance != null
                           ? GravitySystem.Instance.GravityDirection
                           : Vector3.down;
        Vector3 playerUp = -gravDir;

        // 플레이어 반대 방향으로 이동
        Vector3 awayDir  = transform.position - _playerTransform.position;
        awayDir          = Vector3.ProjectOnPlane(awayDir, playerUp).normalized;

        if (awayDir.sqrMagnitude < 0.001f) return;

        Vector3 vertVel    = Vector3.Project(_rb.linearVelocity, playerUp);
        _rb.linearVelocity = awayDir * _retreatSpeed + vertVel;

        transform.rotation = Quaternion.LookRotation(awayDir, playerUp);
    }

    // ─── Combat ───────────────────────────────────────────────────────────────
    private void FacePlayer()
    {
        Vector3 gravDir  = GravitySystem.Instance != null
            ? GravitySystem.Instance.GravityDirection
            : Vector3.down;
        Vector3 playerUp = -gravDir;

        // 수평 + 수직 모두 포함한 3D 방향으로 조준
        Vector3 dir = _playerTransform.position - transform.position;

        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized, playerUp);
    }

    private void TryFire()
    {
        if (Time.time < _nextFireTime) return;
        if (!HasLineOfSight()) return;

        Fire();
        _nextFireTime = Time.time + _fireRate;
    }

    private void Fire()
    {
        if (_bulletPrefab == null) return;

        Vector3 origin    = _muzzlePoint != null ? _muzzlePoint.position : transform.position;
        Vector3 direction = (_playerTransform.position - origin).normalized;

        GameObject bullet = Instantiate(_bulletPrefab, origin,
            Quaternion.LookRotation(direction));

        if (bullet.TryGetComponent<EnemyBullet>(out var bulletComp))
        {
            // 자기 자신의 콜라이더와 충돌 무시
            Collider shooterCol = GetComponent<Collider>();
            bulletComp.Initialize(direction, _bulletSpeed, _bulletDamage, shooterCol);
        }
    }

    // ─── Line of Sight ────────────────────────────────────────────────────────
    private bool HasLineOfSight()
    {
        Vector3 origin = _muzzlePoint != null ? _muzzlePoint.position : transform.position;
        Vector3 dir    = _playerTransform.position - origin;
        float   dist   = dir.magnitude;

        // _obstacleMask가 0(Nothing)이면 항상 시야 확보로 간주
        if (_obstacleMask.value == 0) return true;

        // 장애물이 있으면 발사 안 함
        return !Physics.Raycast(origin, dir.normalized, dist, _obstacleMask);
    }

    // ─── Gizmo ────────────────────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _tooCloseDistance);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _preferredDistance);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _tooFarDistance);
    }
}