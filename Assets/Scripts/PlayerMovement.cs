using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour
{
    // ─── Constants ────────────────────────────────────────────────────────────
    private const float COYOTE_TIME = 0.12f;
    private const float JUMP_BUFFER_TIME = 0.12f;
    private const float REORIENT_ANGLE_THRESH = 1f;     // 이미 정렬된 것으로 볼 최소 각도차
    private const float SLIDE_JUMP_WINDOW = 0.4f;       //슬라이딩 중 점프 인정 시간

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

    [Header("Slide")]   
    [SerializeField] private float _slideSpeed = 14f;           //슬라이드 초기 속도
    [SerializeField] private float _slideDuration = 0.6f;       //슬라이드 지속 시간
    [SerializeField] private float _slideFriction = 8f;         //슬라이드 감속력
    [SerializeField] private float _slideJumpMultiplier = 1.6f; //슬라이드 점프 전방 배율

    [Header("Dash")]
    [SerializeField] private float _dashSpeed = 20f;        //대쉬 순간 속도
    [SerializeField] private float _dashDuration = 0.15f;   //대쉬 지속 시간
    [SerializeField] private float _dashCooldown = 2f;      //대쉬 쿨타임

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

    private bool _isSliding;
    private float _slideTimer;
    private float _slideJumpTimer;  //슬라이드 중 점프 가능 시간 추적
    private Vector3 _slideDirection;
    private bool _isDashing;
    private float _dashTimer;
    private float _dashCooldownTimer;
    private Vector3 _dashDirection;
    
    // ─── Properties ───────────────────────────────────────────────────────────
    public bool IsGrounded => _isGrounded;
    public bool IsReorienting => _isReorienting;
    public Vector3 GravityDir => _gravityDir;
    public bool IsSliding => _isSliding;
    public bool IsDashing => _isDashing;
    public float DashCooldownRatio => Mathf.Clamp01(_dashCooldownTimer / _dashCooldown);

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
        HandleSlideInput();
        HandleDashInput();
    }

    private void FixedUpdate()
    {
        CheckGround();
        DetectLanding();

        if(_slideJumpTimer > 0f)
            _slideJumpTimer -= Time.fixedDeltaTime;

        if (!_isReorienting)
        {
            if(_isDashing)
                ApplyDash();
            else if(_isSliding)
                ApplySlide();
            else
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

        float gravMag = GravitySystem.Instance != null
            ? GravitySystem.Instance.GravityMagnitude
            : 9.81f;

        // v = sqrt(2 * g_eff * h),  g_eff = gravMag * _gravityMultiplier
        float jumpSpeed = Mathf.Sqrt(2f * gravMag * _gravityMultiplier * _jumpHeight);

        // 수직 속도만 교체 (수평 속도 유지)
        Vector3 horizVel = Vector3.ProjectOnPlane(_rb.linearVelocity, transform.up);

        if(_isSliding || _slideJumpTimer > 0f)
        {
            horizVel = _slideDirection * _slideSpeed * _slideJumpMultiplier;
            _isSliding = false;
            _slideTimer = 0f;
            _mouseLook?.SetSlideCameraOffset(false); // 추가
        }

        _rb.linearVelocity     = horizVel + transform.up * jumpSpeed;
        _jumpBufferTimer = 0f;
        _coyoteTimer     = 0f;
    }
    //슬라이딩
    private void HandleSlideInput()
    {
        if(Keyboard.current.leftCtrlKey.wasPressedThisFrame && _isGrounded && !_isSliding && !_isDashing)
        {
            _isSliding = true;
            _slideTimer = _slideDuration;
            _slideJumpTimer = _slideDuration + SLIDE_JUMP_WINDOW;

            //슬라이드 방향 = 현재 바라보는 수평 방향
            _slideDirection = Vector3.ProjectOnPlane(transform.forward, transform.up).normalized;

            //슬라이드 초기 속도 부여
            Vector3 vertVel = Vector3.Project(_rb.linearVelocity, transform.up);
            _rb.linearVelocity = _slideDirection * _slideSpeed + vertVel;

            _mouseLook?.SetSlideCameraOffset(true);
        }
    }

    private void ApplySlide()
    {
        _slideTimer -= Time.fixedDeltaTime;

        //슬라이드 방향 속도를 마찰로 감속
        Vector3 vertVel = Vector3.Project(_rb.linearVelocity, transform.up);
        Vector3 horizVel = Vector3.ProjectOnPlane(_rb.linearVelocity, transform.up);
        Vector3 newHorizVel = Vector3.MoveTowards(horizVel, Vector3.zero, _slideFriction * Time.deltaTime);
    
        _rb.linearVelocity = newHorizVel + vertVel;

        if(_slideTimer <= 0 || !_isGrounded)
        {
             _isSliding = false;
             _mouseLook?.SetSlideCameraOffset(false);
        }   
        
    }

    private void HandleDashInput()
    {
        if(Keyboard.current.leftShiftKey.wasPressedThisFrame && !_isDashing && _dashCooldownTimer <= 0f)
        {
            _isDashing = true;
            _dashTimer = _dashDuration;
            _dashCooldownTimer = _dashCooldown;

            // 대쉬 방향: 입력 방향 우선, 없으면 바라보는 방향
            float h = Keyboard.current.dKey.isPressed ? 1f : Keyboard.current.aKey.isPressed ? -1f : 0f;
            float v = Keyboard.current.wKey.isPressed ? 1f : Keyboard.current.sKey.isPressed ? -1f : 0f;

            Vector3 inputDir = transform.right * h + transform.forward * v;
            inputDir = Vector3.ProjectOnPlane(inputDir, transform.up);

            _dashDirection = inputDir.sqrMagnitude > 0.01f
                ? inputDir.normalized
                : transform.forward;

            // 대쉬 시작 시 기존 속도 제거 후 대쉬 속도 부여
            Vector3 vertVel    = Vector3.Project(_rb.linearVelocity, transform.up);
            _rb.linearVelocity = _dashDirection * _dashSpeed + vertVel;
        }

        if(_dashCooldownTimer > 0f)
            _dashCooldownTimer -= Time.deltaTime;
    }

    private void ApplyDash()
    {
        _dashTimer -= Time.fixedDeltaTime;

        //대쉬 중 속도 유지 (중력 방향 제외)
        Vector3 vertVel = Vector3.Project(_rb.linearVelocity, transform.up);
        _rb.linearVelocity = _dashDirection * _dashSpeed + vertVel;

        if(_dashTimer <= 0f)
            _isDashing = false;
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

        //질질 끌려가는 상황 때문에 즉시 공중 상태로 강제 전환
        _isGrounded = false;
        _wasGrounded = false;
        _coyoteTimer = 0f;
        
        _rb.linearVelocity = Vector3.zero;
        
        if(_isSliding)
        {
            _isSliding = false;
            _mouseLook?.SetSlideCameraOffset(false);
        _isDashing = false;}

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