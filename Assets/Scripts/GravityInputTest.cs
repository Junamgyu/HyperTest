using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 개발/테스트용 중력 방향 전환 입력 (New Input System).
/// 빌드 전 제거하거나 실제 트리거 로직으로 교체하세요.
///
/// 조작:
///   1 → 아래 (기본)
///   2 → 위 (천장)
///   3 → 왼쪽 벽
///   4 → 오른쪽 벽
///   5 → 앞 벽
///   6 → 뒤 벽
///   F → 현재 방향 반전
/// </summary>
public class GravityInputTest : MonoBehaviour
{
    private void Update()
    {
        if (GravitySystem.Instance == null) return;

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.digit1Key.wasPressedThisFrame) 
        {
            Debug.Log("파랑 중력 -> 아래");
            GravitySystem.Instance.SetGravityDown();
        }
        if (kb.digit2Key.wasPressedThisFrame)
        {
            Debug.Log("하양 중력 -> 위");
            GravitySystem.Instance.SetGravityUp();    
        }
        
        if (kb.digit3Key.wasPressedThisFrame) 
        {
            Debug.Log("빨강 중력 -> 왼쪽 벽");
            GravitySystem.Instance.SetGravityLeft();
        }
        
        if (kb.digit4Key.wasPressedThisFrame) 
        {
            Debug.Log("초록 중력 -> 오른쪽 벽");
            GravitySystem.Instance.SetGravityRight();
        }
        if (kb.digit5Key.wasPressedThisFrame) 
        {
            Debug.Log("보라 중력 -> 앞쪽 벽");
            GravitySystem.Instance.SetGravityForward();
        }

        if (kb.digit6Key.wasPressedThisFrame) 
        {
            Debug.Log("검정 중력 -> 뒷쪽 벽");
            GravitySystem.Instance.SetGravityBack();
        }

        if (kb.fKey.wasPressedThisFrame)     
        {
            Debug.Log("F키 현재 방향의 반대 중력 적용");
            GravitySystem.Instance.FlipGravity();
        }
    }
}