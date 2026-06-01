using UnityEngine;
using PeakPlunder.Audio;

/// <summary>
/// ダウン（瀕死）→蘇生システム（PEAK/R.E.P.O. の協力プレイ中核）。
/// HP が尽きると即死せず一定時間ダウンし、その間に生存中の味方が近くに留まると蘇生される。
/// 出血タイマーが切れると力尽きて幽霊化する。ダウン中は操作不能。
/// <see cref="PlayerHealthSystem"/> から自動付与される。
/// </summary>
[RequireComponent(typeof(PlayerHealthSystem))]
public class DownedSystem : MonoBehaviour
{
    [Header("出血タイマー")]
    [Tooltip("ダウンから力尽きるまでの時間 (s)")]
    [SerializeField] private float _bleedOutSeconds = 30f;

    [Header("蘇生")]
    [Tooltip("生存中の味方がこの距離内に留まると蘇生が進行する (m)")]
    [SerializeField] private float _reviveRange = 2.5f;
    [Tooltip("蘇生に必要な滞在時間 (s)")]
    [SerializeField] private float _reviveHoldSeconds = 4f;
    [Tooltip("蘇生後に回復する HP")]
    [SerializeField] private float _reviveHpRestore = 50f;

    private PlayerHealthSystem _health;
    private PlayerStateMachine _stateMachine;

    private bool  _isDowned;
    private float _bleedTimer;
    private float _reviveProgress;

    public bool  IsDowned          => _isDowned;
    public float BleedOutRemaining => _bleedTimer;
    public float ReviveProgress01  => _reviveHoldSeconds > 0f ? Mathf.Clamp01(_reviveProgress / _reviveHoldSeconds) : 0f;

    private void Awake()
    {
        _health       = GetComponent<PlayerHealthSystem>();
        _stateMachine = GetComponent<PlayerStateMachine>();
    }

    /// <summary>PlayerHealthSystem.Die() から呼ばれ、ダウン状態へ入る。</summary>
    public void EnterDowned()
    {
        if (_isDowned) return;
        _isDowned       = true;
        _bleedTimer     = _bleedOutSeconds;
        _reviveProgress = 0f;

        SetControllable(false);
        SafeTransition(PlayerState.Downed);
        GameServices.Audio?.PlaySE(SoundId.StaminaEmpty, transform.position);
        Debug.Log($"[Downed] {gameObject.name} ダウン。蘇生猶予 {_bleedOutSeconds:F0}s");
    }

    private void Update()
    {
        if (!_isDowned) return;

        _bleedTimer -= Time.deltaTime;
        if (_bleedTimer <= 0f)
        {
            BleedOut();
            return;
        }

        var reviver = FindNearbyReviver();
        if (reviver != null)
        {
            // 蘇生者が Medic なら蘇生速度が上がる（役割の差別化）
            float mult = 1f;
            var role = reviver.GetComponent<PlayerRoleSystem>();
            if (role != null) mult = role.ReviveSpeedMultiplier;

            _reviveProgress += Time.deltaTime * mult;
            if (_reviveProgress >= _reviveHoldSeconds)
                Revive();
        }
        else
        {
            _reviveProgress = 0f;
        }
    }

    private PlayerHealthSystem FindNearbyReviver()
    {
        var players = PlayerHealthSystem.RegisteredPlayers;
        for (int i = 0; i < players.Count; i++)
        {
            PlayerHealthSystem p = players[i];
            if (p == null || p == _health) continue;
            if (p.IsDead || p.IsDowned) continue; // 蘇生できるのは生存中の味方のみ
            if (Vector3.Distance(p.transform.position, transform.position) <= _reviveRange)
                return p;
        }
        return null;
    }

    private void Revive()
    {
        _isDowned = false;
        SetControllable(true);
        SafeTransition(PlayerState.Alive);
        _health.ReviveFromDowned(_reviveHpRestore);
        GameServices.Audio?.PlaySE(SoundId.ShrineRevive, transform.position);
        Debug.Log($"[Downed] {gameObject.name} が味方に蘇生された！");
    }

    private void BleedOut()
    {
        _isDowned = false;
        Debug.Log($"[Downed] {gameObject.name} 力尽きた…幽霊化");
        _health.FinalizeDeath(); // Downed→Ghost
    }

    private void SetControllable(bool enabled)
    {
        var controller = GetComponent<ExplorerController>();
        if (controller != null) controller.enabled = enabled;
        var scramble = GetComponent<ScrambleClimbController>();
        if (scramble != null) scramble.enabled = enabled;
    }

    private void SafeTransition(PlayerState next)
    {
        if (_stateMachine == null) return;
        if (!PlayerStateMachine.IsValidTransition(_stateMachine.Current, next)) return;
        _stateMachine.Transition(next);
    }
}
