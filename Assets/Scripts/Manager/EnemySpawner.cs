using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 데이터에 따라 적을 스폰합니다.
/// GameLoopManager에서 호출합니다.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Prefabs")]
    [SerializeField] private GameObject _shooterEnemyPrefab;
    //[SerializeField] private GameObject _suicideEnemyPrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] _spawnPoints; // 스폰 위치들

    // ─── Runtime State ────────────────────────────────────────────────────────
    private readonly List<GameObject> _aliveEnemies = new List<GameObject>();

    public int AliveCount => _aliveEnemies.Count;
    public bool AllDead   => _aliveEnemies.TrueForAll(e => e == null);

    // ─── API ──────────────────────────────────────────────────────────────────
    public IEnumerator SpawnStage(StageData data)
    {
        _aliveEnemies.Clear();

        // ShooterEnemy 스폰
        for (int i = 0; i < data.shooterEnemyCount; i++)
        {
            SpawnEnemy(_shooterEnemyPrefab);
            yield return new WaitForSeconds(data.spawnInterval);
        }

        // SuicideEnemy 스폰
        for (int i = 0; i < data.suicideEnemyCount; i++)
        {
            //SpawnEnemy(_suicideEnemyPrefab);
            yield return new WaitForSeconds(data.spawnInterval);
        }
    }

    private void SpawnEnemy(GameObject prefab)
    {
        if (prefab == null) return;
        if (_spawnPoints == null || _spawnPoints.Length == 0)
        {
            Debug.LogWarning("[EnemySpawner] 스폰 포인트가 없습니다.");
            return;
        }

        // 랜덤 스폰 포인트 선택
        Transform point = _spawnPoints[Random.Range(0, _spawnPoints.Length)];
        GameObject enemy = Instantiate(prefab, point.position, point.rotation);
        _aliveEnemies.Add(enemy);
    }

    /// <summary>살아있는 적이 모두 처치됐는지 반환합니다.</summary>
    public bool IsStageCleared()
    {
        _aliveEnemies.RemoveAll(e => e == null);
        return _aliveEnemies.Count == 0;
    }

    public void ClearAll()
    {
        foreach (var e in _aliveEnemies)
            if (e != null) Destroy(e);
        _aliveEnemies.Clear();
    }
}