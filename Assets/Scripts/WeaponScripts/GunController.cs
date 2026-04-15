using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 샷건 발사 시스템.
/// 
/// 구조:
///   CameraPivot
///   └── GunPivot          ← 이 컴포넌트 부착
///         └── ShotgunModel
///               └── MuzzlePoint  ← 총알 발사 위치
/// 
/// 총알 방향: CameraPivot(카메라)이 바라보는 방향 기준으로 발사
/// 총 위치:  CameraPivot 로컬 좌표계에서 화면 하단에 고정
/// </summary>
public class GunController : MonoBehaviour
{
    // ─── Constants ────────────────────────────────────────────────────────────
    private const float BOB_SPEED        = 8f;    // 총 흔들림 속도
    private const float BOB_AMOUNT       = 0.003f; // 총 흔들림 양

    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Shotgun Settings")]
    [SerializeField] private int   _pelletsPerShot  = 8;      // 한 발당 산탄 수
    [SerializeField] private float _spreadAngle     = 5f;     // 산탄 퍼짐 각도
    [SerializeField] private float _fireRate        = 1f;     // 초당 발사 횟수
    [SerializeField] private float _bulletSpeed     = 30f;    // 총알 속도

    [Header("Recoil")]
    [SerializeField] private float _recoilAmount    = 0.05f;  // 반동 뒤로 밀림 양
    [SerializeField] private float _recoilSpeed     = 10f;    // 반동 복구 속도

    [Header("Gun Position (로컬 좌표)")]
    [SerializeField] private Vector3 _gunRestPosition   = new Vector3(0.15f, -0.15f, 0.3f);  // 기본 위치
    [SerializeField] private Vector3 _gunRestRotation   = new Vector3(0f, 0f, 0f);           // 기본 회전

    [Header("References")]
    [SerializeField] private Transform  _cameraTransform;  // CameraPivot
    [SerializeField] private Transform  _muzzlePoint;      // 총구 위치
    [SerializeField] private GameObject _bulletPrefab;     // 총알 프리팹

    // ─── Runtime State ────────────────────────────────────────────────────────
    private float _nextFireTime  = 0f;
    private float _bobTimer      = 0f;

    private Vector3 _currentRecoilOffset = Vector3.zero;
    private Vector3 _targetRecoilOffset  = Vector3.zero;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    private void Awake()
    {
        ValidateReferences();
        // 총 초기 위치/회전 설정
        transform.localPosition = _gunRestPosition;
        transform.localEulerAngles = _gunRestRotation;
    }

    private void Update()
    {
        HandleFireInput();
    }

    private void LateUpdate()
    {
        UpdateRecoil();
        UpdateGunBob();
    }

    // ─── Fire ─────────────────────────────────────────────────────────────────
    private void HandleFireInput()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame && Time.time >= _nextFireTime)
            Fire();
    }

    private void Fire()
    {
        if (_bulletPrefab == null || _muzzlePoint == null) return;

        _nextFireTime = Time.time + (1f / _fireRate);

        // 산탄 발사: _pelletsPerShot 개수만큼 퍼뜨려서 발사
        for (int i = 0; i < _pelletsPerShot; i++)
        {
            Vector3 direction = CalculatePelletDirection();
            SpawnBullet(direction);
        }

        // 반동 적용
        ApplyRecoil();
    }

    /// <summary>
    /// 카메라 forward를 기준으로 _spreadAngle 범위 내 랜덤 방향을 반환합니다.
    /// </summary>
    private Vector3 CalculatePelletDirection()
    {
        // 카메라가 바라보는 방향 기준
        Vector3 baseDir = _cameraTransform.forward;

        // 원형 균등 분포로 퍼짐 (중앙 쏠림 없이 고르게)
        float angle  = Random.Range(0f, _spreadAngle);
        float rotate = Random.Range(0f, 360f);

        // baseDir 기준으로 angle만큼 꺾은 방향 계산
        Quaternion spreadRot = Quaternion.AngleAxis(rotate, baseDir)
                             * Quaternion.AngleAxis(angle, _cameraTransform.right);

        return spreadRot * baseDir;
    }

    private void SpawnBullet(Vector3 direction)
    {
        GameObject bullet = Instantiate(_bulletPrefab, _muzzlePoint.position, Quaternion.LookRotation(direction));

        if (bullet.TryGetComponent<Bullet>(out var bulletComp))
            bulletComp.Initialize(direction, _bulletSpeed);
    }

    // ─── Recoil ───────────────────────────────────────────────────────────────
    private void ApplyRecoil()
    {
        _targetRecoilOffset = new Vector3(0f, 0f, -_recoilAmount);
    }

    private void UpdateRecoil()
    {
        _targetRecoilOffset  = Vector3.Lerp(_targetRecoilOffset, Vector3.zero, _recoilSpeed * Time.deltaTime);
        _currentRecoilOffset = Vector3.Lerp(_currentRecoilOffset, _targetRecoilOffset, _recoilSpeed * Time.deltaTime);

    }

    // ─── Gun Bob ──────────────────────────────────────────────────────────────
    /// <summary>
    /// 이동 시 총이 살짝 흔들리는 효과.
    /// 키 입력이 있을 때만 bob 타이머를 진행합니다.
    /// </summary>
    private void UpdateGunBob()
    {
        bool isMoving = Keyboard.current.wKey.isPressed
                     || Keyboard.current.aKey.isPressed
                     || Keyboard.current.sKey.isPressed
                     || Keyboard.current.dKey.isPressed;

        if (isMoving)
            _bobTimer += Time.deltaTime * BOB_SPEED;

        float bobY = Mathf.Sin(_bobTimer) * BOB_AMOUNT;
        float bobX = Mathf.Sin(_bobTimer * 0.5f) * BOB_AMOUNT * 0.5f;

        Vector3 bobOffset = new Vector3(bobX, bobY, 0f);
        transform.localPosition = _gunRestPosition + _currentRecoilOffset + bobOffset;
    }

    // ─── Validation ───────────────────────────────────────────────────────────
    private void ValidateReferences()
    {
        if (_cameraTransform == null)
            Debug.LogError("[GunController] _cameraTransform이 비어있습니다. CameraPivot을 연결하세요.");
        if (_muzzlePoint == null)
            Debug.LogError("[GunController] _muzzlePoint가 비어있습니다. MuzzlePoint를 연결하세요.");
        if (_bulletPrefab == null)
            Debug.LogError("[GunController] _bulletPrefab이 비어있습니다. 총알 프리팹을 연결하세요.");
    }
}