using System;
using System.Collections.Generic;
using UnityEngine;
using PeakPlunder.Audio;

/// <summary>
/// GDD §3.1 — スタミナ管理システム。
/// 登攀・スプリント時に消費し、静止・回復アイテムで回復する。
/// 高山帯（2000m以上）では回復速度が低下する（酸素タンク必須）。
/// </summary>
public class StaminaSystem : MonoBehaviour
{
    private static readonly List<StaminaSystem> s_registeredPlayers = new();

    // ── Inspector ───────────────────────────────────────────
    [Header("設定 (ScriptableObject — 未設定時は Inspector デフォルト)")]
    [SerializeField] private StaminaConfigSO _config;

    [Header("スタミナ設定 (Config 未設定時のフォールバック)")]
    [SerializeField] private float _maxStamina          = 100f;
    [SerializeField] private float _regenRateBase        = 12f;
    [SerializeField] private float _regenRateMoving      = 5f;
    [SerializeField] private float _sprintDrain          = 15f;
    [SerializeField] private float _climbDrain           = 10f;

    [Header("高山病")]
    [SerializeField] private float _highAltitude         = 2000f;
    [SerializeField] private float _altitudeDrainBonus   = 5f;
    [SerializeField] private bool  _hasOxygenTank        = false;

    [Header("セーフゾーン (GDD §10.5)")]
    [SerializeField] private float _shelterRegenMultiplier = 2f;

    private float _bonusMaxStamina; // 恒久アップグレードによる加算

    private float MaxStaminaValue            => (_config != null ? _config.MaxStamina : _maxStamina) + _bonusMaxStamina;
    private float RegenRateBaseValue       => _config != null ? _config.RegenRateBase : _regenRateBase;
    private float RegenRateMovingValue     => _config != null ? _config.RegenRateMoving : _regenRateMoving;
    private float SprintDrainValue         => _config != null ? _config.SprintDrain : _sprintDrain;
    private float ClimbDrainValue          => _config != null ? _config.ClimbDrain : _climbDrain;
    private float HighAltitudeValue        => _config != null ? _config.HighAltitude : _highAltitude;
    private float AltitudeDrainBonusValue  => _config != null ? _config.AltitudeDrainBonus : _altitudeDrainBonus;
    private float ShelterRegenMultiplierValue => _config != null ? _config.ShelterRegenMultiplier : _shelterRegenMultiplier;
    private float ExhaustRecoverThresholdValue => _config != null ? _config.ExhaustRecoverThreshold : 25f;

    private float _currentStamina;
    private bool  _isExhausted;
    private Rigidbody       _cachedRigidbody;
    private ShelterOccupant _shelter;

    // GDD §15.2 — stamina_warning SE の閾値（20 未満で心拍音）
    private const float WARNING_THRESHOLD = 20f;
    private bool _wasBelowWarningThreshold;

    public float MaxStamina        => MaxStaminaValue;
    public float CurrentStamina    => _currentStamina;
    public float StaminaPercent    => MaxStaminaValue > 0f ? _currentStamina / MaxStaminaValue : 0f;
    public bool  IsEmpty           => _currentStamina <= 0f;
    public bool  IsExhausted       => _isExhausted;
    public bool  HasOxygenTank     { get => _hasOxygenTank; set => _hasOxygenTank = value; }

    public event Action OnExhausted;
    public event Action OnRecovered;
    public static IReadOnlyList<StaminaSystem> RegisteredPlayers => s_registeredPlayers;

    private void Awake()
    {
        Contract.Invariant(MaxStaminaValue > 0f, "StaminaSystem: maxStamina は正の値でなければならない");
        _currentStamina  = MaxStaminaValue;
        _cachedRigidbody = GetComponent<Rigidbody>();
        _shelter         = GetComponent<ShelterOccupant>();
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
        RegenerateStamina();
        CheckAltitudeDrain();
        UpdateExhaustionState();
        UpdateWarningSound();
    }

    // ── 消費 ─────────────────────────────────────────────────
    public void Consume(float amount)
    {
        if (_currentStamina <= 0f) return;
        _currentStamina = Mathf.Max(0f, _currentStamina - amount);
    }

    public void ConsumeSprint()   => Consume(SprintDrainValue   * Time.deltaTime);
    public void ConsumeClimbing() => Consume(ClimbDrainValue    * Time.deltaTime);

    // ── 回復 ─────────────────────────────────────────────────
    private void RegenerateStamina()
    {
        if (_isExhausted) return;

        bool  isMoving = GetIsMoving();
        float rate     = isMoving ? RegenRateMovingValue : RegenRateBaseValue;

        if (_shelter != null && _shelter.IsSheltered)
            rate *= ShelterRegenMultiplierValue;

        _currentStamina = Mathf.Min(MaxStaminaValue, _currentStamina + rate * Time.deltaTime);
    }

    /// <summary>食料アイテム使用時などに外部から回復させる。</summary>
    public void Recover(float amount)
    {
        _currentStamina = Mathf.Min(MaxStaminaValue, _currentStamina + amount);
    }

    /// <summary>恒久アップグレードによる最大スタミナ加算を適用する（べき等）。</summary>
    public void ApplyMaxStaminaBonus(float bonus)
    {
        if (bonus < 0f) bonus = 0f;
        float delta = bonus - _bonusMaxStamina;
        _bonusMaxStamina = bonus;
        if (delta > 0f) _currentStamina += delta;
        _currentStamina = Mathf.Clamp(_currentStamina, 0f, MaxStaminaValue);
    }

    // ── 高山病 ────────────────────────────────────────────────
    private void CheckAltitudeDrain()
    {
        if (_hasOxygenTank) return;

        float altitude = transform.position.y;
        if (altitude < HighAltitudeValue) return;

        Consume(AltitudeDrainBonusValue * Time.deltaTime);
    }

    // ── 疲労状態管理 ─────────────────────────────────────────
    private void UpdateExhaustionState()
    {
        bool wasExhausted = _isExhausted;

        if (!_isExhausted && _currentStamina <= 0f)
        {
            _isExhausted = true;
            OnExhausted?.Invoke();
            // GDD §15.2 — stamina_empty SE（「ハァ…」息切れ音）
            GameServices.Audio?.PlaySE2D(SoundId.StaminaEmpty);
            Debug.Log("[Stamina] スタミナ切れ！");
        }

        if (_isExhausted && _currentStamina >= ExhaustRecoverThresholdValue)
        {
            _isExhausted = false;
            OnRecovered?.Invoke();
        }
    }

    private bool GetIsMoving()
    {
        return _cachedRigidbody != null && _cachedRigidbody.linearVelocity.sqrMagnitude > 0.5f;
    }

    /// <summary>
    /// GDD §15.2 — スタミナが 20 を下回った瞬間に stamina_warning を一度だけ発火する。
    /// 連続再生ではなく閾値の立ち下がりエッジで鳴らす。
    /// </summary>
    private void UpdateWarningSound()
    {
        bool nowBelow = _currentStamina < WARNING_THRESHOLD && _currentStamina > 0f;
        if (nowBelow && !_wasBelowWarningThreshold)
            GameServices.Audio?.PlaySE2D(SoundId.StaminaWarning);
        _wasBelowWarningThreshold = nowBelow;
    }

    // ── デバッグ用 ────────────────────────────────────────────
    /// <summary>
    /// オフラインテスト用: スタミナを即座にゼロにする。
    /// OfflineTestBootstrapper の F9 キーから呼ばれる。
    /// </summary>
    public void ConsumeAll()
    {
        Consume(_currentStamina);
    }
}
