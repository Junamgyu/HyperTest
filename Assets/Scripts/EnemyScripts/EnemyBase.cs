using System.Collections;
using UnityEngine;

/// <summary>
/// 모든 적의 공통 기반 클래스.
/// 체력, 피격 처리, 플레이어 참조, 커스텀 중력, 중력 재정렬을 담당합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public abstract class EnemyBase : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Stats")]
    [SerializeField] protected float _maxHealth = 100f;
    [SerializeField] protected float _moveSpeed = 5f;

    [Header("Gravity")]
    [SerializeField] private float _gravityMultiplier = 2.5f;  // 기본 중력 배율
    [SerializeField] private float _fallMultiplier    = 4.0f;  // 낙하 중 추가 배율

    [Header("Reorientation")]
    [SerializeField] private float    _reorientDuration  = 0.3f;
    [SerializeField] private float    _groundCheckRadius = 0.5f;
    [SerializeField] private float    _groundCheckOffset = 1.0f;
    [SerializeField] private LayerMask _groundMask;

    [Header("References")]
    [SerializeField] protected Transform _playerTransform;

    // ─── Runtime State ────────────────────────────────────────────────────────
    protected Rigidbody _rb;
    protected float     _currentHealth;
    protected bool      _isDead     = false;

    protected Vector3   _gravityDir = Vector3.down;
    private bool        _wasGrounded;
    private bool        _isReorienting;

    // ─── Properties ───────────────────────────────────────────────────────────
    public bool IsDead => _isDead;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    protected virtual void Awake()
    {
        _rb                = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;
        _rb.useGravity     = false;   // 커스텀 중력 직접 적용 (Unity 기본 중력 비활성화)
        _currentHealth     = _maxHealth;
    }

    protected virtual void Start()
    {
        // 플레이어 자동 탐색
        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                _playerTransform = player.transform;
            }
            else
            {
                // "Player" 태그가 없는 경우 PlayerMovement 컴포넌트로 폴백
                PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
                if (pm != null)
                    _playerTransform = pm.transform;
                else
                    Debug.LogWarning($"[{gameObject.name}] 플레이어를 찾지 못했습니다. Player 태그 또는 PlayerMovement 컴포넌트를 확인하세요.");
            }
        }

        // 중력 시스템 구독
        if (GravitySystem.Instance != null)
        {
            _gravityDir = GravitySystem.Instance.GravityDirection;
            GravitySystem.Instance.OnGravityDirectionChanged += HandleGravityChanged;
        }
    }

    protected virtual void OnDisable()
    {
        if (GravitySystem.Instance != null)
            GravitySystem.Instance.OnGravityDirectionChanged -= HandleGravityChanged;
    }

    /// <summary>
    /// 기반 FixedUpdate: 커스텀 중력 적용 + 중력 재정렬.
    /// 파생 클래스에서 반드시 base.FixedUpdate()를 먼저 호출해야 합니다.
    /// </summary>
    protected virtual void FixedUpdate()
    {
        ApplyGravity();
        CheckReorientation();
    }

    // ─── Gravity ──────────────────────────────────────────────────────────────
    /// <summary>
    /// GravitySystem 방향으로 중력 가속을 Rigidbody에 직접 가합니다.
    /// PlayerMovement / CustomGravityBody 와 동일한 방식.
    /// </summary>
    private void ApplyGravity()
    {
        float gravMag  = GravitySystem.Instance != null
                         ? GravitySystem.Instance.GravityMagnitude
                         : 9.81f;

        // 중력 방향으로 이미 떨어지는 중이면 fallMultiplier 적용
        bool  isFalling  = Vector3.Dot(_rb.linearVelocity, _gravityDir) > 0f;
        float multiplier = isFalling ? _fallMultiplier : _gravityMultiplier;

        _rb.AddForce(_gravityDir * (gravMag * multiplier), ForceMode.Acceleration);
    }

    private void HandleGravityChanged(Vector3 newDirection)
    {
        _gravityDir        = newDirection;
        _rb.linearVelocity = Vector3.zero;
        _wasGrounded       = false;
    }

    // ─── Reorientation ────────────────────────────────────────────────────────
    /// <summary>
    /// 착지 시 중력 방향에 맞게 몸을 부드럽게 회전시킵니다.
    /// </summary>
    protected void CheckReorientation()
    {
        if (_isReorienting) return;

        Vector3 sphereCenter = transform.position + (_gravityDir * _groundCheckOffset);
        bool    isGrounded   = Physics.CheckSphere(sphereCenter, _groundCheckRadius, _groundMask);

        bool justLanded = isGrounded && !_wasGrounded;
        if (justLanded)
        {
            float angleDiff = Vector3.Angle(transform.up, -_gravityDir);
            if (angleDiff > 1f)
                StartCoroutine(ReorientRoutine());
        }

        _wasGrounded = isGrounded;
    }

    private IEnumerator ReorientRoutine()
    {
        _isReorienting = true;

        Quaternion fromRot = transform.rotation;
        Quaternion toRot   = Quaternion.FromToRotation(transform.up, -_gravityDir)
                             * transform.rotation;

        float elapsed = 0f;
        while (elapsed < _reorientDuration)
        {
            elapsed           += Time.deltaTime;
            float t            = Mathf.SmoothStep(0f, 1f,
                                     Mathf.Clamp01(elapsed / _reorientDuration));
            transform.rotation = Quaternion.Slerp(fromRot, toRot, t);
            yield return null;
        }

        transform.rotation = toRot;
        _isReorienting     = false;
    }

    // ─── Damage / Death ───────────────────────────────────────────────────────
    public virtual void TakeDamage(float damage)
    {
        if (_isDead) return;

        _currentHealth -= damage;

        if (_currentHealth <= 0f)
            Die();
    }

    protected virtual void Die()
    {
        _isDead = true;
        OnDeath();
        Destroy(gameObject, 0.1f);
    }

    protected virtual void OnDeath() { }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    protected float DistanceToPlayer()
    {
        if (_playerTransform == null) return float.MaxValue;
        return Vector3.Distance(transform.position, _playerTransform.position);
    }

    /// <summary>
    /// 중력 수평 평면 기준으로 목표 위치를 향해 이동합니다.
    /// 수직(중력 방향) 속도는 유지해 중력이 정상 작동합니다.
    /// </summary>
    protected void MoveToward(Vector3 targetPosition, float speed)
    {
        Vector3 up  = -_gravityDir;
        Vector3 dir = Vector3.ProjectOnPlane(targetPosition - transform.position, up);

        if (dir.sqrMagnitude < 0.001f) return;

        // 수직 속도(중력 축) 보존, 수평만 교체
        Vector3 vertVel    = Vector3.Project(_rb.linearVelocity, up);
        _rb.linearVelocity = dir.normalized * speed + vertVel;

        transform.rotation = Quaternion.LookRotation(dir.normalized, up);
    }

    protected void StopHorizontalMovement()
    {
        Vector3 up      = -_gravityDir;
        Vector3 vertVel = Vector3.Project(_rb.linearVelocity, up);
        _rb.linearVelocity = vertVel;
    }

    // ─── Gizmo ────────────────────────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        Vector3 gravDir      = Application.isPlaying ? _gravityDir : Vector3.down;
        Vector3 sphereCenter = transform.position + (gravDir * _groundCheckOffset);

        Gizmos.color = _wasGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(sphereCenter, _groundCheckRadius);
    }
}
