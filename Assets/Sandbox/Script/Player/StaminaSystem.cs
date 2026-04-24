using System;
using System.Collections.Generic;
using UnityEngine;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

/// <summary>
/// GDD §3.1 — スタミナ管理システム。
/// 登攀・スプリント時に消費し、静止・回復アイテムで回復する。
/// 高山帯（2000m以上）では回復速度が低下する（酸素タンク必須）。
/// </summary>
public class StaminaSystem : MonoBehaviour
{
    private static readonly List<StaminaSystem> s_registeredPlayers = new();

    // ── Inspector ───────────────────────────────────────────
    [Header("スタミナ設定")]
    [SerializeField] private float _maxStamina          = 100f;
    [SerializeField] private float _regenRateBase        = 12f;   // /秒（静止時）
    [SerializeField] private float _regenRateMoving      = 5f;    // /秒（移動時）
    [SerializeField] private float _sprintDrain          = 15f;   // /秒
    [SerializeField] private float _climbDrain           = 10f;   // /秒（GrabPoint に設定された値を使う）

    [Header("高山病")]
    [SerializeField] private float _highAltitude         = 2000f; // m 以上で高山病
    [SerializeField] private float _altitudeDrainBonus   = 5f;    // /秒 追加消費
    [SerializeField] private bool  _hasOxygenTank        = false; // 酸素タンクの有無

    [Header("セーフゾーン (GDD §10.5)")]
    [Tooltip("ShelterZone 内でのスタミナ回復速度倍率（GDD §10.5: 2倍）")]
    [SerializeField] private float _shelterRegenMultiplier = 2f;

    // ── 状態 ────────────────────────────────────────────────
    private float _currentStamina;
    private bool  _isExhausted;
    private float _exhaustRecoverThreshold = 25f;   // この量まで回復したら疲労解除
    private Rigidbody       _cachedRigidbody;
    private ShelterOccupant _shelter;

    // GDD §15.2 — stamina_warning SE の閾値（20 未満で心拍音）
    private const float WARNING_THRESHOLD = 20f;
    private bool _wasBelowWarningThreshold;

    public float MaxStamina        => _maxStamina;
    public float CurrentStamina    => _currentStamina;
    public float StaminaPercent    => _currentStamina / _maxStamina;
    public bool  IsEmpty           => _currentStamina <= 0f;
    public bool  IsExhausted       => _isExhausted;
    public bool  HasOxygenTank     { get => _hasOxygenTank; set => _hasOxygenTank = value; }

    public event Action OnExhausted;
    public event Action OnRecovered;
    public static IReadOnlyList<StaminaSystem> RegisteredPlayers => s_registeredPlayers;

    private void Awake()
    {
        _currentStamina  = _maxStamina;
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

    public void ConsumeSprint()   => Consume(_sprintDrain   * Time.deltaTime);
    public void ConsumeClimbing() => Consume(_climbDrain    * Time.deltaTime);

    // ── 回復 ─────────────────────────────────────────────────
    private void RegenerateStamina()
    {
        if (_isExhausted) return;

        bool  isMoving = GetIsMoving();
        float rate     = isMoving ? _regenRateMoving : _regenRateBase;

        // GDD §10.5: セーフゾーン内では回復速度 2 倍
        if (_shelter != null && _shelter.IsSheltered)
            rate *= _shelterRegenMultiplier;

        _currentStamina = Mathf.Min(_maxStamina, _currentStamina + rate * Time.deltaTime);
    }

    /// <summary>食料アイテム使用時などに外部から回復させる。</summary>
    public void Recover(float amount)
    {
        _currentStamina = Mathf.Min(_maxStamina, _currentStamina + amount);
    }

    // ── 高山病 ────────────────────────────────────────────────
    private void CheckAltitudeDrain()
    {
        if (_hasOxygenTank) return;

        float altitude = transform.position.y;
        if (altitude < _highAltitude) return;

        Consume(_altitudeDrainBonus * Time.deltaTime);
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
            PPAudioManager.Instance?.PlaySE2D(SoundId.StaminaEmpty);
            Debug.Log("[Stamina] スタミナ切れ！");
        }

        if (_isExhausted && _currentStamina >= _exhaustRecoverThreshold)
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
            PPAudioManager.Instance?.PlaySE2D(SoundId.StaminaWarning);
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
