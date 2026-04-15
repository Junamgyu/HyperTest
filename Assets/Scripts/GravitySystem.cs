using UnityEngine;

/// <summary>
/// 씬 전체의 중력 방향을 관리하는 싱글톤.
/// 중력이 바뀌면 OnGravityDirectionChanged 이벤트를 발행
/// 모든 중력의 영향을 받는 오브젝트(플레이어, 적 등)는 이 이벤트를 구독
/// </summary>
public class GravitySystem : MonoBehaviour
{
    public static GravitySystem Instance { get; private set; }

    [SerializeField] private Vector3 _initialGravityDirection = Vector3.down;

    private Vector3 _gravityDirection;

    public Vector3 GravityDirection => _gravityDirection;
    public float GravityMagnitude  => Physics.gravity.magnitude; // 크기는 Unity 세팅 그대로 사용

    public event System.Action<Vector3> OnGravityDirectionChanged;

    //! ─── Lifecycle ────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _gravityDirection = _initialGravityDirection.normalized;
    }

    //! ─── API ──────────────────────────────────────────────────────────────────
    public void SetGravityDirection(Vector3 newDirection)
    {
        if (newDirection.sqrMagnitude < 0.001f) return;

        _gravityDirection = newDirection.normalized;
        
        OnGravityDirectionChanged?.Invoke(_gravityDirection);
    }

    // 방향 단축키 (기획/디버그용)
    public void FlipGravity() => SetGravityDirection(-_gravityDirection);
    public void SetGravityDown() => SetGravityDirection(Vector3.down);
    public void SetGravityUp() => SetGravityDirection(Vector3.up);
    public void SetGravityLeft() => SetGravityDirection(Vector3.left);
    public void SetGravityRight() => SetGravityDirection(Vector3.right);
    public void SetGravityForward() => SetGravityDirection(Vector3.forward);
    public void SetGravityBack() => SetGravityDirection(Vector3.back);
}