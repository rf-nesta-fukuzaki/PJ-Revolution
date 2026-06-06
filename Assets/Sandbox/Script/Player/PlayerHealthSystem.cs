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

    [Header("設定 (ScriptableObject — 未設定時は Inspector デフォルト)")]
    [SerializeField] private PlayerHealthConfigSO _config;

    [Header("HP (Config 未設定時のフォールバック)")]
    [SerializeField] private float _maxHp             = 100f;
    [SerializeField] private float _fallDamageMinSpeed = 8f;
    [SerializeField] private float _fallDamageScale    = 3f;
    [SerializeField] private float _safeFallHeight     = 3f;
    [SerializeField] private float _instantKillHeight  = 15f;
    [SerializeField] private float _fallDamagePerMeter = 8f;
    [SerializeField] private float _deathY             = -30f;

    private float        _currentHp;
    private bool         _isDead;
    private bool         _isDowned;
    private Rigidbody    _rb;
    private float        _prevVelocityY;
    private RagdollSystem _ragdoll;

    private float _bonusMaxHp;        // 恒久アップグレードによる加算
    private float _damageResistance;  // 役割(Vanguard)による被ダメージ軽減 (0-1)

    private float MaxHpValue             => (_config != null ? _config.MaxHp : _maxHp) + _bonusMaxHp;
    private float FallDamageMinSpeedValue => _config != null ? _config.FallDamageMinSpeed : _fallDamageMinSpeed;
    private float FallDamageScaleValue    => _config != null ? _config.FallDamageScale : _fallDamageScale;
    private float SafeFallHeightValue     => _config != null ? _config.SafeFallHeight : _safeFallHeight;
    private float InstantKillHeightValue  => _config != null ? _config.InstantKillHeight : _instantKillHeight;
    private float FallDamagePerMeterValue => _config != null ? _config.FallDamagePerMeter : _fallDamagePerMeter;
    private float DeathYValue             => _config != null ? _config.DeathY : _deathY;

    public float HpPercent => MaxHpValue > 0f ? _currentHp / MaxHpValue : 0f;
    public bool  IsDead    => _isDead;
    public bool  IsDowned  => _isDowned;
    public float CurrentHp => _currentHp;
    public float MaxHp     => MaxHpValue;

    public event Action<float>              OnDamaged;
    public event Action<PlayerHealthSystem> OnDied;
    public event Action<PlayerHealthSystem> OnDowned;

    public static IReadOnlyList<PlayerHealthSystem> RegisteredPlayers => s_registeredPlayers;

    private void Awake()
    {
        if (_config != null && !_config.TryValidate(out string reason))
            Debug.LogError($"[Contract] PlayerHealthConfigSO が不正: {reason}");

        Contract.Invariant(MaxHpValue > 0f,
            $"PlayerHealthSystem.Awake: maxHp は 0 より大きくなければなりません (value={MaxHpValue})");

        _rb        = GetComponent<Rigidbody>();
        _ragdoll   = GetComponent<RagdollSystem>();
        _currentHp = MaxHpValue;

        // ダウン→蘇生システムを全プレイヤーに付与（非破壊・無ければ生成）
        if (GetComponent<DownedSystem>() == null)
            gameObject.AddComponent<DownedSystem>();

        // 恒久アップグレード適用コンポーネントを付与（無ければ生成）
        if (GetComponent<PlayerUpgradeApplier>() == null)
            gameObject.AddComponent<PlayerUpgradeApplier>();

        // 疲労失神システムを付与（StaminaSystem がある場合のみ・無ければ生成）
        if (GetComponent<StaminaSystem>() != null && GetComponent<ExhaustionPassoutSystem>() == null)
            gameObject.AddComponent<ExhaustionPassoutSystem>();

        // 役割システムを付与（無ければ生成）
        if (GetComponent<PlayerRoleSystem>() == null)
            gameObject.AddComponent<PlayerRoleSystem>();
    }

    /// <summary>役割(Vanguard)による被ダメージ軽減率を設定する (0=なし, 0.4=40%軽減)。</summary>
    public void SetDamageResistance(float resistance) => _damageResistance = Mathf.Clamp01(resistance);

    /// <summary>恒久アップグレードによる最大HP加算を適用する（べき等）。</summary>
    public void ApplyMaxHpBonus(float bonus)
    {
        if (bonus < 0f) bonus = 0f;
        float delta = bonus - _bonusMaxHp;
        _bonusMaxHp = bonus;
        if (delta > 0f && !_isDead) _currentHp += delta;
        _currentHp = Mathf.Clamp(_currentHp, 0f, MaxHpValue);
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

        if (transform.position.y < DeathYValue)
            Die();
    }

    private void LateUpdate()
    {
        _prevVelocityY = _rb.linearVelocity.y;
    }

    private void OnCollisionEnter(Collision col)
    {
        if (_isDead || _isDowned) return;

        // 高速衝突（Ragdoll 閾値以上）は RagdollSystem が一手に処理し、復帰時に落下ダメージを
        // 適用する。ここで二重に適用しないようスキップする（旧実装は両方が別式で適用するバグ）。
        if (_ragdoll != null && col.relativeVelocity.magnitude >= _ragdoll.VelocityThreshold)
            return;

        // GDD §3.4 — 落下ダメージは「落下高さ」基準。衝突直前の Y 速度から h = v²/2g で換算。
        float impactSpeedY = Mathf.Abs(_prevVelocityY);
        float fallHeight   = impactSpeedY * impactSpeedY / (2f * 9.81f);
        float damage       = ComputeFallDamage(fallHeight);
        if (damage > 0f)
            TakeDamage(damage);
    }

    /// <summary>GDD §3.4 — 落下高さからダメージを求める。&lt;安全高さ=0 / ≥即死高さ=即死 / 間=(h-安全)×係数。</summary>
    private float ComputeFallDamage(float fallHeight)
    {
        float safe = SafeFallHeightValue;
        float kill = InstantKillHeightValue;
        if (fallHeight < safe)  return 0f;
        if (fallHeight >= kill) return MaxHpValue;   // 即死
        return (fallHeight - safe) * FallDamagePerMeterValue;
    }

    /// <summary>ダメージを与える。前提条件: amount は 0 以上。</summary>
    public void TakeDamage(float amount)
    {
        if (!Contract.TryRequires(amount >= 0f,
            $"PlayerHealthSystem.TakeDamage: amount は 0 以上でなければなりません (value={amount}, player={gameObject.name})"))
            return;

        // ダウン中はこれ以上ダメージを受けない（出血タイマーのみが死を決める）
        if (_isDead || _isDowned || amount <= 0f) return;

        // 役割(Vanguard)による被ダメージ軽減
        amount *= (1f - _damageResistance);
        if (amount <= 0f) return;

        _currentHp = Mathf.Max(0f, _currentHp - amount);
        OnDamaged?.Invoke(amount);

        Contract.Ensures(_currentHp >= 0f && _currentHp <= MaxHpValue,
            $"TakeDamage 後の HP が不正: {_currentHp}");

        if (_currentHp <= 0f)
            Die();
    }

    /// <summary>HP を回復する。前提条件: amount は 0 以上。</summary>
    public void Heal(float amount)
    {
        if (!Contract.TryRequires(amount >= 0f,
            $"PlayerHealthSystem.Heal: amount は 0 以上でなければなりません (value={amount}, player={gameObject.name})"))
            return;

        if (_isDead) return;

        _currentHp = Mathf.Min(MaxHpValue, _currentHp + amount);

        Contract.Ensures(_currentHp <= MaxHpValue,
            $"Heal 後の HP が MaxHp を超えました: {_currentHp} > {MaxHpValue}");
    }

    private void Die()
    {
        if (_isDead || _isDowned) return;

        // ダウンシステムがあれば、即死せずまず瀕死（ダウン）状態へ。味方が蘇生できる。
        var downed = GetComponent<DownedSystem>();
        if (downed != null)
        {
            _isDowned = true;
            Debug.Log($"[Health] {gameObject.name} がダウンしました（蘇生可能）");
            OnDowned?.Invoke(this);
            downed.EnterDowned();
            return;
        }

        FinalizeDeath();
    }

    /// <summary>ダウンの出血タイマー切れ等で完全に死亡し、幽霊へ移行する。</summary>
    public void FinalizeDeath()
    {
        if (_isDead) return;
        _isDowned = false;
        _isDead   = true;

        Debug.Log($"[Health] {gameObject.name} が死亡しました");
        OnDied?.Invoke(this);

        GetComponent<GhostSystem>()?.EnterGhostMode();
    }

    /// <summary>味方によるダウンからの蘇生。HP を部分回復して行動可能に戻す。</summary>
    public void ReviveFromDowned(float hpRestored)
    {
        if (!_isDowned) return;
        _isDowned  = false;
        _currentHp = Mathf.Clamp(hpRestored, 0.01f, MaxHpValue);
        Debug.Log($"[Health] {gameObject.name} がダウンから蘇生 (HP={_currentHp:F0})");
    }

    /// <summary>死亡状態から復活して HP を回復する。</summary>
    public void Revive(float hpRestored)
    {
        if (!Contract.TryRequires(hpRestored >= 0f,
            $"PlayerHealthSystem.Revive: hpRestored は 0 以上でなければなりません (value={hpRestored}, player={gameObject.name})"))
            return;

        _isDead    = false;
        _currentHp = Mathf.Clamp(hpRestored, 0.01f, MaxHpValue);

        Debug.Log($"[Health] {gameObject.name} が復活 (HP={_currentHp:F0}/{MaxHpValue:F0})");
    }
}
