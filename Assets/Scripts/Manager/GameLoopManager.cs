using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 전체 루프를 담당합니다.
/// 
/// 흐름:
///   씬 시작 → Stage 1 UI 표시 → 중력 설정 → 적 스폰
///   → 모든 적 처치 → Stage Clear → 다음 스테이지
///   → Stage 6 클리어 → Game Clear
/// </summary>
public class GameLoopManager : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Stage Data (1~6 순서대로 연결)")]
    [SerializeField] private StageData[] _stages;

    [Header("References")]
    [SerializeField] private EnemySpawner _spawner;
    [SerializeField] private GameLoopUI   _ui;

    [Header("Timing")]
    [SerializeField] private float _stageAnnounceDuration = 2.5f; // Stage N 표시 시간
    [SerializeField] private float _clearAnnounceDuration = 2.0f; // Clear 표시 시간
    [SerializeField] private float _nextStageDelay        = 1.0f; // 다음 스테이지 전 딜레이
    [SerializeField] private float _clearCheckInterval    = 0.5f; // 클리어 체크 주기

    // ─── Runtime State ────────────────────────────────────────────────────────
    private int  _currentStageIndex = 0;
    private bool _isRunning         = false;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    private void Start()
    {
        StartCoroutine(RunGameLoop());
    }

    // ─── Game Loop ────────────────────────────────────────────────────────────
    private IEnumerator RunGameLoop()
    {
        _isRunning = true;

        for (_currentStageIndex = 0; _currentStageIndex < _stages.Length; _currentStageIndex++)
        {
            yield return StartCoroutine(RunStage(_stages[_currentStageIndex]));
        }

        yield return StartCoroutine(GameClear());
    }

    private IEnumerator RunStage(StageData data)
    {
        // 1. Stage N 표시
        _ui.ShowStageAnnounce(data.stageNumber);
        yield return new WaitForSeconds(_stageAnnounceDuration);
        _ui.HideAnnounce();

        // 2. 중력 방향 설정
        if (GravitySystem.Instance != null)
            GravitySystem.Instance.SetGravityDirection(data.gravityDirection);

        Debug.Log($"[GameLoop] Stage {data.stageNumber} 시작 — 중력: {data.gravityDirection}");

        // 3. 적 스폰
        yield return StartCoroutine(_spawner.SpawnStage(data));

        // 4. 모든 적 처치 대기
        yield return StartCoroutine(WaitForStageClear());

        // 5. Stage Clear 표시
        _ui.ShowClearAnnounce(data.stageNumber);
        yield return new WaitForSeconds(_clearAnnounceDuration);
        _ui.HideAnnounce();

        yield return new WaitForSeconds(_nextStageDelay);
    }

    private IEnumerator WaitForStageClear()
    {
        // 모든 적이 사라질 때까지 주기적으로 확인
        while (!_spawner.IsStageCleared())
            yield return new WaitForSeconds(_clearCheckInterval);
    }

    private IEnumerator GameClear()
    {
        _ui.ShowGameClear();
        Debug.Log("[GameLoop] 게임 클리어!");

        yield return new WaitForSeconds(3f);
        // 추후 게임 클리어 씬으로 전환 가능
        // SceneManager.LoadScene("ClearScene");
    }
}