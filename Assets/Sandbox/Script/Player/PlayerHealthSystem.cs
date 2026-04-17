using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GDD §4.1 — プレイヤーHP・死亡システム。
/// 落下ダメージ、落石、凍死 etc でHP減少。
/// HP=0 または画面外落下で死亡 → 偵察幽霊に遷移。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerHealthSystem : MonoBehaviour
{
    private static readonly List<PlayerHealthSystem> s_registeredPlayers = new();

    [Header("HP")]
    [SerializeField] private float _maxHp             = 100f;
    [SerializeField] private float _fallDamageMinSpeed = 8f;    // これ以上の着地速度でダメージ
    [SerializeField] private float _fallDamageScale    = 3f;    // ダメージ係数
    [SerializeField] private float _deathY             = -30f;  // この高度で即死

    private float     _currentHp;
    private bool      _isDead;
    private Rigidbody _rb;
    private float     _prevVelocityY;

    public float HpPercent => _maxHp > 0f ? _currentHp / _maxHp : 0f;
    public bool  IsDead    => _isDead;
    public float CurrentHp => _currentHp;
    public float MaxHp     => _maxHp;

    public event Action<float>              OnDamaged;   // (amount)
    public event Action<PlayerHealthSystem> OnDied;      // (self)

    public static IReadOnlyList<PlayerHealthSystem> RegisteredPlayers => s_registeredPlayers;

    private void Awake()
    {
        // 不変条件: MaxHp は正の値でなければならない
        Debug.Assert(_maxHp > 0f,
            $"[Contract] PlayerHealthSystem.Awake: _maxHp は 0 より大きくなければなりません (value={_maxHp})");

        _rb        = GetComponent<Rigidbody>();
        _currentHp = _maxHp;
    }

    private void OnEnable()
    {
        if (!s_registeredPlayers.Contains(this))
            s_registeredPlayers.Add(this);
    }

    private void OnDisable()
    {
        s_registeredPlayers.Remove(this);
    }

    private void Update()
    {
        if (_isDead) return;

        // 画面外落下：即死
        if (transform.position.y < _deathY)
            Die();
    }

    private void LateUpdate()
    {
        _prevVelocityY = _rb.linearVelocity.y;
    }

    private void OnCollisionEnter(Collision col)
    {
        if (_isDead) return;

        float impactSpeedY = Mathf.Abs(_prevVelocityY);
        if (impactSpeedY < _fallDamageMinSpeed) return;

        float damage = (impactSpeedY - _fallDamageMinSpeed) * _fallDamageScale;
        TakeDamage(damage);
    }

    // ── ダメージ ─────────────────────────────────────────────
    /// <summary>
    /// ダメージを与える。
    /// 前提条件: amount は正の値。死亡中は無視される。
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (amount < 0f)
        {
            Debug.LogError(
                $"[Contract] PlayerHealthSystem.TakeDamage: amount は 0 以上でなければなりません " +
                $"(value={amount}, player={gameObject.name})");
            return;
        }

        if (_isDead || amount <= 0f) return;

        _currentHp = Mathf.Max(0f, _currentHp - amount);
        OnDamaged?.Invoke(amount);

        // 事後条件: HP は 0 以上 MaxHp 以下
        Debug.Assert(_currentHp >= 0f && _currentHp <= _maxHp,
            $"[Contract] TakeDamage 後の HP が不正: {_currentHp}");

        if (_currentHp <= 0f)
            Die();
    }

    /// <summary>
    /// HP を回復する。
    /// 前提条件: amount は正の値。死亡中は無視される。
    /// </summary>
    public void Heal(float amount)
    {
        if (amount < 0f)
        {
            Debug.LogError(
                $"[Contract] PlayerHealthSystem.Heal: amount は 0 以上でなければなりません " +
                $"(value={amount}, player={gameObject.name})");
            return;
        }

        if (_isDead) return;

        _currentHp = Mathf.Min(_maxHp, _currentHp + amount);

        // 事後条件: HP は MaxHp を超えてはならない
        Debug.Assert(_currentHp <= _maxHp,
            $"[Contract] Heal 後の HP が MaxHp を超えました: {_currentHp} > {_maxHp}");
    }

    // ── 死亡 ─────────────────────────────────────────────────
    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        Debug.Log($"[Health] {gameObject.name} が死亡しました");
        OnDied?.Invoke(this);

        // GhostSystem に遷移を委譲
        GetComponent<GhostSystem>()?.EnterGhostMode();
    }
}
