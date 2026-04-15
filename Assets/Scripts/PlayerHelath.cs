using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 체력, 피격, 사망을 담당합니다.
/// EnemyBullet이 충돌 시 TakeDamage()를 호출합니다.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Health")]
    [SerializeField] private float _maxHealth       = 100f;
    [SerializeField] private float _invincibleTime  = 0.3f;  // 피격 후 무적 시간 (연속 피격 방지)

    [Header("Death")]
    [SerializeField] private float _deathDelay      = 1.5f;  // 사망 후 씬 리로드까지 딜레이

    // ─── Runtime State ────────────────────────────────────────────────────────
    private float _currentHealth;
    private bool  _isDead        = false;
    private bool  _isInvincible  = false;

    // ─── Properties ───────────────────────────────────────────────────────────
    public float CurrentHealth => _currentHealth;
    public float MaxHealth     => _maxHealth;
    public float HealthRatio   => _currentHealth / _maxHealth;
    public bool  IsDead        => _isDead;

    // ─── Events ───────────────────────────────────────────────────────────────
    public event System.Action<float, float> OnHealthChanged; // (current, max)
    public event System.Action               OnDeath;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    private void Awake()
    {
        _currentHealth = _maxHealth;
    }

    // ─── API ──────────────────────────────────────────────────────────────────
    public void TakeDamage(float damage)
    {
        if (_isDead || _isInvincible) return;

        _currentHealth = Mathf.Max(0f, _currentHealth - damage);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

        Debug.Log($"[PlayerHealth] 피격 {damage} / 남은 체력: {_currentHealth}");

        if (_currentHealth <= 0f)
            Die();
        else
            StartCoroutine(InvincibleRoutine());
    }

    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        Debug.Log("[PlayerHealth] 플레이어 사망");
        OnDeath?.Invoke();

        // 이동 잠금
        PlayerMovement pm = GetComponentInParent<PlayerMovement>();
        if (pm != null) pm.enabled = false;

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(_deathDelay);
        // 현재 씬 재시작 (추후 게임오버 씬으로 교체 가능)
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private IEnumerator InvincibleRoutine()
    {
        _isInvincible = true;
        yield return new WaitForSeconds(_invincibleTime);
        _isInvincible = false;
    }

    public void Heal(float amount)
    {
        if (_isDead) return;
        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }
}