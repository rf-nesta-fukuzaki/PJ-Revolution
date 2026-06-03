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
    [Tooltip("自然回復速度 (/s)。走り・登攀をしていない間、一定レートで最大値まで回復する。")]
    [SerializeField] private float _regenRateBase        = 12f;
    [SerializeField] private float _sprintDrain          = 15f;
    [Tooltip("スクランブル登攀の消費 (/s)。登攀中は自然回復しないため、この値がそのままネット消費になる。")]
    [SerializeField] private float _climbDrain           = 5f;

    [Header("高山病")]
    [SerializeField] private float _highAltitude         = 2000f;
    [SerializeField] private float _altitudeDrainBonus   = 5f;
    [SerializeField] private bool  _hasOxygenTank        = false;

    [Header("セーフゾーン (GDD §10.5)")]
    [SerializeField] private float _shelterRegenMultiplier = 2f;

    private float _bonusMaxStamina; // 恒久アップグレードによる加算

    private float MaxStaminaValue            => (_config != null ? _config.MaxStamina : _maxStamina) + _bonusMaxStamina;
    private float RegenRateBaseValue       => _config != null ? _config.RegenRateBase : _regenRateBase;
    private float SprintDrainValue         => _config != null ? _config.SprintDrain : _sprintDrain;
    private float ClimbDrainValue          => _config != null ? _config.ClimbDrain : _climbDrain;
    private float HighAltitudeValue        => _config != null ? _config.HighAltitude : _highAltitude;
    private float AltitudeDrainBonusValue  => _config != null ? _config.AltitudeDrainBonus : _altitudeDrainBonus;
    private float ShelterRegenMultiplierValue => _config != null ? _config.ShelterRegenMultiplier : _shelterRegenMultiplier;
    private float ExhaustRecoverThresholdValue => _config != null ? _config.ExhaustRecoverThreshold : 25f;

    private float _currentStamina;
    private bool  _isExhausted;
    private ShelterOccupant _shelter;
    // 走り・登攀でスタミナを消費したフレーム番号。このフレームは自然回復しない（走っている間は回復しない）。
    private int   _exertionFrame = -1;

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
        _currentStamina = MaxStaminaValue;
        _shelter        = GetComponent<ShelterOccupant>();
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
    /// <param name="isExertion">
    /// 走り・登攀など能動的に踏ん張っている消費か。true のフレームはこの直後の自然回復を止める。
    /// 高山病ドレインなどの受動的消費は false（回復速度の低下として扱う）。
    /// </param>
    public void Consume(float amount, bool isExertion = false)
    {
        if (_currentStamina <= 0f) return;
        _currentStamina = Mathf.Max(0f, _currentStamina - amount);
        if (isExertion) _exertionFrame = Time.frameCount;
    }

    public void ConsumeSprint()   => Consume(SprintDrainValue * Time.deltaTime, isExertion: true);
    public void ConsumeClimbing() => Consume(ClimbDrainValue  * Time.deltaTime, isExertion: true);

    // ── 回復 ─────────────────────────────────────────────────
    private void RegenerateStamina()
    {
        // 走り・登攀で踏ん張っているフレームは自然回復しない（「走っていない時」だけ回復する）。
        // ConsumeSprint/ConsumeClimbing と本メソッドの Update 実行順に依存しないよう直近 1
        // フレームまで猶予を見る。踏ん張りをやめれば一定レートで最大値まで回復する。
        // ※スプリント可否のゲートは ExplorerController 側の IsEmpty 判定が担うため、ここで
        //   回復を止めても永久疲労デッドロックにはならない（走りを止めれば必ず回復するため）。
        if (Time.frameCount - _exertionFrame <= 1) return;

        float rate = RegenRateBaseValue;
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

        // 高山病による消費は「山高に対する割合」で判定する（実山高 ~460m に対し絶対 2000m 固定では
        // 永遠に発火しなかったため）。MountainProfile 未準備時は従来の絶対標高フォールバック。
        float threshold = MountainProfile.IsReady
            ? MountainProfile.WorldYAtFraction(0.75f)
            : HighAltitudeValue;
        if (transform.position.y < threshold) return;

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
