using UnityEngine;

/// <summary>
/// 스테이지 하나의 설정 데이터.
/// ScriptableObject로 만들어서 각 스테이지마다 에셋으로 관리합니다.
/// </summary>
[CreateAssetMenu(fileName = "StageData", menuName = "Game/StageData")]
public class StageData : ScriptableObject
{
    [Header("Stage Info")]
    public int    stageNumber;
    public string stageName;

    [Header("Gravity")]
    public Vector3 gravityDirection = Vector3.down;

    [Header("Enemy Spawn")]
    public int   shooterEnemyCount  = 1;  // ShooterEnemy 수
    public int   suicideEnemyCount  = 0;  // SuicideEnemy 수
    public float spawnInterval      = 0.5f; // 적 사이 스폰 간격

    [Header("Clear Condition")]
    public int killsToClear = 0; // 0이면 모든 적 처치 시 클리어
}