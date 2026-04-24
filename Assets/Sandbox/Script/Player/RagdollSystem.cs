using System.Collections;
using UnityEngine;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

/// <summary>
/// GDD §5.5 — Ragdoll システム。
///
/// 落下中に壁面/地面に衝突速度 VelocityThreshold m/s 以上で接触した場合、
/// Duration 秒間 Ragdoll 状態に入り、制御を無効化する。
/// 復帰後に落下ダメージ適用。
///
/// 設定値は RagdollConfigSO に外部化されている。
/// ステート管理は PlayerStateMachine に委譲。
///
/// セットアップ：
///   1. このコンポーネントを PlayerPrefab のルートに付ける
///   2. Ragdoll ボーンは Layer 15 (RagdollBone) に設定
///   3. _ragdollBodies に全ての子 Rigidbody を設定（Hips から末端まで）
///   4. _mainRigidbody はプレイヤーのルート Rigidbody
///   5. _mainCollider はプレイヤーの CapsuleCollider
/// </summary>
[RequireComponent(typeof(PlayerStateMachine))]
public class RagdollSystem : MonoBehaviour
{
    // ── データ駆動設定 ────────────────────────────────────────
    [Header("Ragdoll 設定 (ScriptableObject)")]
    [SerializeField] private RagdollConfigSO _config;

    [Header("Ragdoll コンポーネント")]
    [SerializeField] private Rigidbody[] _ragdollBodies;
    [SerializeField] private Collider[]  _ragdollColliders;

    [Header("プレイヤー本体")]
    [SerializeField] private Rigidbody   _mainRigidbody;
    [SerializeField] private Collider    _mainCollider;
    [SerializeField] private Animator    _animator;

    // ── コンポーネント ─────────────────────────────────────────
    private PlayerHealthSystem  _health;
    private ExplorerController  _controller;
    private PlayerStateMachine  _stateMachine;

    // ── プロパティ ───────────────────────────────────────────
    public bool IsRagdoll => _stateMachine != null && _stateMachine.IsRagdoll;

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        _health       = GetComponent<PlayerHealthSystem>();
        _controller   = GetComponent<ExplorerController>();
        _stateMachine = GetComponent<PlayerStateMachine>();

        Debug.Assert(_stateMachine != null,
            "[RagdollSystem] PlayerStateMachine が同一 GameObject に見つかりません");

        // Inspector で未設定の場合、子から自動取得
        if ((_ragdollBodies == null || _ragdollBodies.Length == 0) && transform.childCount > 0)
        {
            var mainRb = _mainRigidbody != null ? _mainRigidbody : GetComponent<Rigidbody>();
            var list   = new System.Collections.Generic.List<Rigidbody>();
            foreach (var rb in GetComponentsInChildren<Rigidbody>())
                if (rb != mainRb) list.Add(rb);
            _ragdollBodies = list.ToArray();
        }

        if (_mainRigidbody == null) _mainRigidbody = GetComponent<Rigidbody>();
        if (_mainCollider  == null) _mainCollider  = GetComponent<Collider>();
        if (_animator      == null) _animator      = GetComponentInChildren<Animator>();

        SetRagdollActive(false);
    }

    // ── 衝突判定 ─────────────────────────────────────────────
    private void OnCollisionEnter(Collision col)
    {
        if (IsRagdoll) return;
        if (_health != null && _health.IsDead) return;
        if (_stateMachine != null && !_stateMachine.IsAlive) return;

        float threshold      = _config != null ? _config.VelocityThreshold : 15f;
        float impactVelocity = col.relativeVelocity.magnitude;
        if (impactVelocity >= threshold)
        {
            Debug.Log($"[Ragdoll] 衝突速度 {impactVelocity:F1}m/s ≥ {threshold}。Ragdoll 発動！");
            StartCoroutine(RagdollCoroutine(impactVelocity));
        }
    }

    // ── Ragdoll コルーチン ────────────────────────────────────
    private IEnumerator RagdollCoroutine(float impactVelocity)
    {
        float duration = _config != null ? _config.Duration : 3f;

        _stateMachine.Transition(PlayerState.Ragdoll);
        SetRagdollActive(true);
        if (_controller != null) _controller.enabled = false;

        // GDD §15.2 — ragdoll_impact（高速衝突で Ragdoll 発動の瞬間に鳴らす）
        PPAudioManager.Instance?.PlaySE(SoundId.RagdollImpact, transform.position);

        yield return new WaitForSeconds(duration);

        SetRagdollActive(false);
        RecoverFromRagdoll();

        float fallDamage = CalculateFallDamage(impactVelocity);
        if (fallDamage > 0f && _health != null)
        {
            _health.TakeDamage(fallDamage);
            Debug.Log($"[Ragdoll] 落下ダメージ: {fallDamage:F0}");
        }

        _stateMachine.Transition(PlayerState.Alive);
        Debug.Log("[Ragdoll] 制御復帰");
    }

    // ── Ragdoll 有効/無効 ─────────────────────────────────────
    private void SetRagdollActive(bool active)
    {
        if (_mainRigidbody != null) _mainRigidbody.isKinematic = active;
        if (_mainCollider  != null) _mainCollider.enabled       = !active;
        if (_animator      != null) _animator.enabled           = !active;

        if (_ragdollBodies != null)
            foreach (var rb in _ragdollBodies)
                if (rb != null) rb.isKinematic = !active;

        if (_ragdollColliders != null)
            foreach (var col in _ragdollColliders)
                if (col != null) col.enabled = active;
    }

    private void RecoverFromRagdoll()
    {
        // ルート位置をヒップボーンに合わせる（床にめり込み防止）
        if (_ragdollBodies != null && _ragdollBodies.Length > 0 && _ragdollBodies[0] != null)
            transform.position = _ragdollBodies[0].position;

        if (_controller != null) _controller.enabled = true;
    }

    // ── 落下ダメージ計算（GDD §3.4 テーブルに基づく簡易版）──
    /// <summary>
    /// 衝突速度から落下ダメージを計算する。
    /// v² = 2gh → h = v² / (2 × 9.81) で落下高さに変換。
    /// 設定値は RagdollConfigSO から取得する。
    /// </summary>
    private float CalculateFallDamage(float velocity)
    {
        // 前提条件: velocity は非負でなければならない
        Debug.Assert(velocity >= 0f, $"[Contract] RagdollSystem.CalculateFallDamage: velocity が負の値 ({velocity})");

        float safeFall    = _config != null ? _config.SafeFallHeight    : 3f;
        float instantKill = _config != null ? _config.InstantKillHeight : 15f;
        float damageRate  = _config != null ? _config.DamagePerMeter    : 8f;

        float fallHeight = velocity * velocity / (2f * 9.81f);
        if (fallHeight < safeFall)    return 0f;
        if (fallHeight >= instantKill) return 100f;
        return (fallHeight - safeFall) * damageRate;
    }
}
