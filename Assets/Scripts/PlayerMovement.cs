using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 커스텀 중력 방향 기반 1인칭 이동.
/// 
/// 흐름:
///   1. GravitySystem에서 중력 방향 변경 이벤트를 받는다
///   2. useGravity = false 상태에서 새 방향으로 직접 힘을 가한다
///   3. 새 벽에 착지하면 ReorientRoutine이 플레이어 body를 부드럽게 회전시킨다
///   4. 재정렬 완료 후 WASD·마우스 조작이 새 방향 기준으로 정상 작동한다
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour
{
    // ─── Constants ────────────────────────────────────────────────────────────
    private const float COYOTE_TIME = 0.12f;
    private const float JUMP_BUFFER_TIME = 0.12f;
    private const float REORIENT_ANGLE_THRESH = 1f;     // 이미 정렬된 것으로 볼 최소 각도차

    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 8f;
    [SerializeField] private float _acceleration = 80f;
    [SerializeField] private float _deceleration = 50f;
    [SerializeField] private float _airControlFactor = 0.35f;

    [Header("Jump")]
    [SerializeField] private float _jumpHeight = 2.5f;
    [SerializeField] private float _gravityMultiplier = 2.5f;  // 전체 중력 배율
    [SerializeField] private float _fallMultiplier = 4.0f;  // 낙하 중 추가 배율

    [Header("Ground Check")]
    [SerializeField] private LayerMask _groundMask;
    [SerializeField] private float _groundCheckRadius = 0.5f;
    [SerializeField] private float _groundCheckOffset = 1.0f; // 발바닥에서 아래 방향 오프셋

    [Header("Reorientation")]
    [SerializeField] private float _reorientDuration = 0.35f;

    [Header("References")]
    [SerializeField] private MouseLook _mouseLook;

    // ─── Runtime State ────────────────────────────────────────────────────────
    private Rigidbody _rb;
    private Vector3 _gravityDir = Vector3.down;

    private bool _isGrounded;
    private bool _wasGrounded;        // 이전 프레임 접지 여부 (착지 감지용)
    private bool _isReorienting;

    private float _coyoteTimer;
    private float _jumpBufferTimer;

    private Coroutine _reorientCoroutine;

    // ─── Properties ───────────────────────────────────────────────────────────
    public bool IsGrounded => _isGrounded;
    public bool IsReorienting => _isReorienting;
    public Vector3 GravityDir => _gravityDir;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;        // 중력을 직접 관리
        _rb.freezeRotation = true;     // 물리엔진이 회전 제어 못 하도록

        // GravitySystem에서 초기 방향 가져오기
        if (GravitySystem.Instance != null)
            _gravityDir = GravitySystem.Instance.GravityDirection;

        // 씬 시작 시 중력에 맞게 즉시 정렬
        AlignToGravityImmediate();
    }

    private void Start()
    {
        if (GravitySystem.Instance != null)
            GravitySystem.Instance.OnGravityDirectionChanged += OnGravityChanged;   
    }

    private void OnDisable()
    {
        if (GravitySystem.Instance != null)
            GravitySystem.Instance.OnGravityDirectionChanged -= OnGravityChanged;
    }

    private void Update()
    {
        HandleJumpInput();
    }

    private void FixedUpdate()
    {
        CheckGround();
        DetectLanding();

        if (!_isReorienting)
        {
            ApplyMovement();
            TryJump();
        }

        ApplyGravity();

        _wasGrounded = _isGrounded;
    }

    // ─── Ground Detection ─────────────────────────────────────────────────────
    /// <summary>현재 중력 방향 기준으로 발밑에 바닥이 있는지 검사합니다.</summary>
    private void CheckGround()
    {
        Vector3 sphereCenter = transform.position + (_gravityDir * _groundCheckOffset);
        _isGrounded = Physics.CheckSphere(sphereCenter, _groundCheckRadius, _groundMask);

        if (_isGrounded)
            _coyoteTimer = COYOTE_TIME;
        else
            _coyoteTimer -= Time.fixedDeltaTime;
    }

    // ─── Landing Detection ────────────────────────────────────────────────────
    /// <summary>
    /// 공중 → 착지 전환을 감지해서 플레이어 재정렬을 시작합니다.
    /// 이미 올바른 방향으로 정렬되어 있으면 재정렬을 건너뜁니다.
    /// </summary>
    private void DetectLanding()
    {
        bool justLanded = _isGrounded && !_wasGrounded;
        if (!justLanded) return;

        float angleDiff = Vector3.Angle(transform.up, -_gravityDir);
        if (angleDiff > REORIENT_ANGLE_THRESH)
            StartReorientation();
    }

    // ─── Movement ─────────────────────────────────────────────────────────────
    private void ApplyMovement()
    {
        Vector2 moveInput = new Vector2(
            Keyboard.current.dKey.isPressed ? 1f : Keyboard.current.aKey.isPressed ? -1f : 0f,
            Keyboard.current.wKey.isPressed ? 1f : Keyboard.current.sKey.isPressed ? -1f : 0f
        );

        float h = moveInput.x;
        float v = moveInput.y;

        // transform.right / transform.forward는 현재 플레이어 방향 기준
        // → transform.up(= -gravityDir)에 수직인 평면으로 투영하면 항상 수평 이동만 남음
        Vector3 inputDir = transform.right * h + transform.forward * v;
        inputDir = Vector3.ProjectOnPlane(inputDir, transform.up);
        if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();

        Vector3 targetVel = inputDir * _moveSpeed;
        Vector3 currentHorizVel = Vector3.ProjectOnPlane(_rb.linearVelocity, transform.up);

        float controlFactor = _isGrounded ? 1f : _airControlFactor;
        float accel = inputDir.sqrMagnitude > 0.01f ? _acceleration : _deceleration;

        Vector3 newHorizVel = Vector3.MoveTowards(
            currentHorizVel,
            targetVel,
            accel * controlFactor * Time.fixedDeltaTime
        );

        // 수직 속도(중력 방향)는 ApplyGravity가 담당 → 수평만 교체
        Vector3 vertVel  = Vector3.Project(_rb.linearVelocity, transform.up);
        _rb.linearVelocity = newHorizVel + vertVel;
    }

    // ─── Gravity ──────────────────────────────────────────────────────────────
    private void ApplyGravity()
    {
        float gravMag = GravitySystem.Instance != null
            ? GravitySystem.Instance.GravityMagnitude
            : 9.81f;

        // 중력 방향으로 속도가 향할 때 = 낙하 중 → fallMultiplier 적용
        bool  isFalling = Vector3.Dot(_rb.linearVelocity, _gravityDir) > 0f;
        float multiplier = isFalling ? _fallMultiplier : _gravityMultiplier;

        _rb.AddForce(_gravityDir * (gravMag * multiplier), ForceMode.Acceleration);
    }

    // ─── Jump ─────────────────────────────────────────────────────────────────
    private void HandleJumpInput()
    {
        if(Keyboard.current.spaceKey.wasPressedThisFrame)
            _jumpBufferTimer = JUMP_BUFFER_TIME;
        else
            _jumpBufferTimer -= Time.deltaTime;
    }

    private void TryJump()
    {
        if (_jumpBufferTimer <= 0f || _coyoteTimer <= 0f) return;

        float gravMag   = GravitySystem.Instance != null
                          ? GravitySystem.Instance.GravityMagnitude
                          : 9.81f;

        // v = sqrt(2 * g_eff * h),  g_eff = gravMag * _gravityMultiplier
        float jumpSpeed = Mathf.Sqrt(2f * gravMag * _gravityMultiplier * _jumpHeight);

        // 수직 속도만 교체 (수평 속도 유지)
        Vector3 horizVel = Vector3.ProjectOnPlane(_rb.linearVelocity, transform.up);
        _rb.linearVelocity     = horizVel + transform.up * jumpSpeed;

        _jumpBufferTimer = 0f;
        _coyoteTimer     = 0f;
    }

    // ─── Gravity Change Handler ────────────────────────────────────────────────
    private void OnGravityChanged(Vector3 newGravityDir)
    {
        _gravityDir = newGravityDir;

        // 재정렬 중이었다면 취소 (새 중력 방향으로 다시 처리)
        if (_isReorienting)
        {
            StopReorientation();
        }

        // 새 중력 방향으로의 속도 성분 제거
        // (기존 낙하 관성 대신 새 방향으로 깨끗하게 전환)
        Vector3 horizVel = Vector3.ProjectOnPlane(_rb.linearVelocity, _gravityDir);
        _rb.linearVelocity     = horizVel;

        // 착지 상태라면 즉시 공중으로 전환 (새 중력이 당기도록)
        // → CheckGround가 다음 FixedUpdate에서 false를 반환하면 자연스럽게 Airborne 상태가 됨
    }

    // ─── Reorientation ────────────────────────────────────────────────────────
    private void StartReorientation()
    {
        if (_reorientCoroutine != null)
            StopCoroutine(_reorientCoroutine);

        _reorientCoroutine = StartCoroutine(ReorientRoutine());
    }

    private void StopReorientation()
    {
        if (_reorientCoroutine != null)
        {
            StopCoroutine(_reorientCoroutine);
            _reorientCoroutine = null;
        }

        _isReorienting = false;
        _mouseLook?.SetLookEnabled(true);
    }

    /// <summary>
    /// 착지 후 플레이어 body를 새 중력 기준으로 부드럽게 회전시킵니다.
    /// transform.up → -gravityDir 이 되도록 Slerp.
    /// 회전 중 MouseLook은 비활성화, 완료 후 재활성화.
    /// </summary>
    private IEnumerator ReorientRoutine()
    {
        _isReorienting = true;
        _mouseLook?.SetLookEnabled(false);

        Quaternion fromRot  = transform.rotation;
        Quaternion toRot    = CalculateTargetRotation();

        float elapsed = 0f;
        while (elapsed < _reorientDuration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _reorientDuration));
            transform.rotation = Quaternion.Slerp(fromRot, toRot, t);
            yield return null;
        }

        transform.rotation = toRot;
        _reorientCoroutine = null;
        _isReorienting     = false;
        _mouseLook?.SetLookEnabled(true);
    }

    /// <summary>
    /// 현재 forward 방향을 최대한 유지하면서 transform.up = -gravityDir 인 회전을 계산합니다.
    /// </summary>
    private Quaternion CalculateTargetRotation()
    {
        Vector3 targetUp = -_gravityDir;
        Vector3 currentForward = transform.forward;

        // forward를 새 up 기준 평면에 투영
        Vector3 newForward = Vector3.ProjectOnPlane(currentForward, targetUp);

        // 정면이 up/down 방향일 때 fallback (예: 천장을 바라보다 중력 역전)
        if (newForward.sqrMagnitude < 0.001f)
            newForward = Vector3.ProjectOnPlane(transform.right, targetUp);
        if (newForward.sqrMagnitude < 0.001f)
            newForward = Vector3.ProjectOnPlane(Vector3.forward, targetUp);

        return Quaternion.LookRotation(newForward.normalized, targetUp);
    }

    /// <summary>초기화 또는 씬 시작 시 중력에 맞게 즉시 정렬합니다.</summary>
    private void AlignToGravityImmediate()
    {
        transform.rotation = CalculateTargetRotation();
    }

    private void OnDrawGizmos()
    {
        Vector3 gravDir = Application.isPlaying? _gravityDir : Vector3.down;
        Vector3 sphereCenter = transform.position + (gravDir * _groundCheckOffset);

        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(sphereCenter, _groundCheckRadius);

        // 중력 방향 화살표 (노란색)
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, _gravityDir * 1.5f);
    }
}