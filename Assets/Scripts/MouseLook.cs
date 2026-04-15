using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 1인칭 마우스 시점 제어.
/// 
/// 좌우(Yaw) : 플레이어 body를 transform.up(= -gravityDir) 기준으로 회전.
///             중력 방향이 바뀌어도 Space.Self를 쓰면 항상 올바른 축으로 회전.
/// 상하(Pitch): CameraPivot의 로컬 X축 회전. Body 회전과 독립.
/// 
/// 재정렬 중(IsReorienting)에는 입력을 차단합니다.
/// Body가 Slerp로 회전하는 동안 카메라가 따라가므로
/// 피치 값(float)은 그대로 유지해도 시점이 자연스럽게 이어집니다.
/// </summary>
public class MouseLook : MonoBehaviour
{
    // ─── Constants ────────────────────────────────────────────────────────────
    private const float MIN_PITCH = -89f;
    private const float MAX_PITCH =  89f;

    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Sensitivity")]
    [SerializeField] private float _sensitivity = 2.5f;
    [SerializeField] private bool  _invertY     = false;

    [Header("References")]
    [SerializeField] private Transform _cameraPivot; // 카메라를 담고 있는 자식 오브젝트

    // ─── Runtime State ────────────────────────────────────────────────────────
    private float _pitch       = 0f;
    private bool  _lookEnabled = true;
    private Quaternion _pivotRotationBefore;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (_cameraPivot == null)
            Debug.LogError("[MouseLook] _cameraPivot이 비어있습니다. CameraPivot 오브젝트를 연결하세요.");
    }

    private void Start()
    {
        LockCursor(true);
    }

    private void Update()
    {
        if(Keyboard.current.escapeKey.wasPressedThisFrame)
            LockCursor(!IsCursorLocked());

        if (_lookEnabled && IsCursorLocked())
            HandleMouseLook();
    }

    // ─── Mouse Look ───────────────────────────────────────────────────────────
    private void HandleMouseLook()
    {
        Vector2 mouseDelata = Mouse.current.delta.ReadValue();
        float mouseX = mouseDelata.x * _sensitivity;
        float mouseY = mouseDelata.y * _sensitivity;

        if (_invertY) mouseY = -mouseY;

        // ── Yaw (좌우) ────────────────────────────────────────────────────────
        // Space.Self = 플레이어 자신의 up 축 기준 회전
        // transform.up은 ReorientRoutine이 -gravityDir로 맞춰두므로 항상 올바른 축
        transform.Rotate(Vector3.up, mouseX, Space.Self);

        // ── Pitch (상하) ──────────────────────────────────────────────────────
        // CameraPivot 로컬 X축 회전, Body와 독립
        // mouse up → 위를 봐야 하므로 pitch 감소
        _pitch = Mathf.Clamp(_pitch - mouseY, MIN_PITCH, MAX_PITCH);
        _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    // ─── Public API ───────────────────────────────────────────────────────────
    /// <summary>재정렬 코루틴이 입력 차단/재개할 때 호출합니다.</summary>
    public void SetLookEnabled(bool enabled)
    {
        _lookEnabled = enabled;

        if(!enabled)
        {
            _pivotRotationBefore = _cameraPivot.rotation;
        }
        else
        {
            _pitch = _cameraPivot.localEulerAngles.x > 180f 
                ? _cameraPivot.localEulerAngles.x - 360f 
                : _cameraPivot.localEulerAngles.x;
        }
    }
    public void LockCameraWorldRotation()
    {
        _cameraPivot.rotation = _pivotRotationBefore;
    }

    public void SyncAfterReorient(Transform playerBody, Vector3 newUp)
    {
        Vector3 cameraWorldForward = _pivotRotationBefore * Vector3.forward;

        Vector3 horizontalForward = Vector3.ProjectOnPlane(cameraWorldForward, newUp);

        if(horizontalForward.sqrMagnitude > 0.001f)
        {   //body yaw를 카메라가 보던 수평 방향으로 맞춤
            playerBody.rotation = Quaternion.LookRotation(horizontalForward.normalized);
        }
        
        float pitch = Vector3.SignedAngle(
            horizontalForward.normalized,
            cameraWorldForward,
            playerBody.right
        );

        _pitch = Mathf.Clamp(pitch, MIN_PITCH, MAX_PITCH);
        _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

    }

    /// <summary>카메라 피치를 수평으로 리셋합니다 (필요 시 외부에서 호출).</summary>
    public void ResetPitch()
    {
        _pitch = 0f;
        if (_cameraPivot != null)
            _cameraPivot.localRotation = Quaternion.identity;
    }

    // ─── Cursor ───────────────────────────────────────────────────────────────
    private static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible   = !locked;
    }

    private static bool IsCursorLocked()
        => Cursor.lockState == CursorLockMode.Locked;
}