using System;
using System.Collections.Generic;
using UnityEngine;

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

    // ── 状態 ────────────────────────────────────────────────
    private float _currentStamina;
    private bool  _isExhausted;
    private float _exhaustRecoverThreshold = 25f;   // この量まで回復したら疲労解除
    private Rigidbody _cachedRigidbody;

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
        _currentStamina = _maxStamina;
        _cachedRigidbody = GetComponent<Rigidbody>();
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

        bool isMoving = GetIsMoving();
        float rate    = isMoving ? _regenRateMoving : _regenRateBase;
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
