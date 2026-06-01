using UnityEngine;
using PeakPlunder.Audio;

/// <summary>
/// 疲労による失神（PEAK パリティ）。スタミナを切らしたまま無理を続けると一定時間で気絶し、
/// 短時間操作不能になったのち自力で回復する（スタミナ部分回復＝疲労状態の解除も兼ねる）。
/// ダウン中・死亡中は作動しない。<see cref="PlayerHealthSystem"/> から自動付与される。
/// </summary>
[RequireComponent(typeof(StaminaSystem))]
public class ExhaustionPassoutSystem : MonoBehaviour
{
    [Header("失神")]
    [Tooltip("疲労状態がこの秒数続くと失神する")]
    [SerializeField] private float _passoutThreshold = 5f;
    [Tooltip("失神してから自力回復するまでの時間 (s)")]
    [SerializeField] private float _passoutDuration = 4f;
    [Tooltip("回復時に戻すスタミナの割合 (0-1)")]
    [SerializeField, Range(0f, 1f)] private float _recoverStaminaFraction = 0.6f;

    private StaminaSystem      _stamina;
    private PlayerHealthSystem _health;
    private DownedSystem       _downed;

    private bool  _isPassedOut;
    private float _exhaustedTime;
    private float _passoutTimer;

    public bool  IsPassedOut   => _isPassedOut;
    public float ExhaustedTime => _exhaustedTime;

    private void Awake()
    {
        _stamina = GetComponent<StaminaSystem>();
        _health  = GetComponent<PlayerHealthSystem>();
        _downed  = GetComponent<DownedSystem>();
    }

    private void Update()
    {
        if (_stamina == null) return;

        // ダウン中・死亡中は失神判定しない
        if ((_health != null && _health.IsDead) || (_downed != null && _downed.IsDowned))
        {
            _exhaustedTime = 0f;
            return;
        }

        if (_isPassedOut)
        {
            _passoutTimer -= Time.deltaTime;
            if (_passoutTimer <= 0f) Recover();
            return;
        }

        // 過労判定: スタミナが尽きた状態（empty 付近）で無理に踏ん張る（ダッシュ/登攀）と蓄積。
        bool empty    = _stamina.IsExhausted || _stamina.StaminaPercent <= 0.05f;
        bool exerting = IsExerting();

        if (empty && exerting)
        {
            _exhaustedTime += Time.deltaTime;
            if (_exhaustedTime >= _passoutThreshold)
                PassOut();
        }
        else
        {
            _exhaustedTime = 0f;
        }
    }

    /// <summary>ダッシュ中または登攀中＝踏ん張っている（過労が蓄積する）か。</summary>
    private bool IsExerting()
    {
        var controller = GetComponent<ExplorerController>();
        if (controller != null && controller.enabled && controller.IsSprinting) return true;
        var scramble = GetComponent<ScrambleClimbController>();
        if (scramble != null && scramble.IsClimbing) return true;
        return false;
    }

    private void PassOut()
    {
        _isPassedOut  = true;
        _passoutTimer = _passoutDuration;
        SetControllable(false);
        GameServices.Audio?.PlaySE(SoundId.StaminaEmpty, transform.position);
        Debug.Log($"[Passout] {gameObject.name} が疲労で気絶！{_passoutDuration:F0}s 後に回復");
    }

    private void Recover()
    {
        _isPassedOut   = false;
        _exhaustedTime = 0f;
        _stamina.Recover(_stamina.MaxStamina * _recoverStaminaFraction);
        SetControllable(true);
        Debug.Log($"[Passout] {gameObject.name} 意識回復");
    }

    private void SetControllable(bool controllable)
    {
        var controller = GetComponent<ExplorerController>();
        if (controller != null) controller.enabled = controllable;
        var scramble = GetComponent<ScrambleClimbController>();
        if (scramble != null) scramble.enabled = controllable;
    }
}
