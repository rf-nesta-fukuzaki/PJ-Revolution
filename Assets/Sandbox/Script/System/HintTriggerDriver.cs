using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GDD §21.2 — コンテキストヒントの発火ドライバー。
///
/// プレイヤーにアタッチし、ゲームプレイ状況を 1Hz でポーリングして
/// <see cref="HintManager"/> に HintId を通知する。
/// 各ヒントは SaveManager により初回のみ表示（seenHints で抑止）される。
///
/// 対応表（GDD §21.2）:
///   1 FirstClimbApproach  — GrabPoint に 3m 以内
///   2 DashIntroduction    — 初回歩行から 5 秒経過
///   3 StaminaDepleted     — StaminaSystem.OnExhausted
///   4 RelicApproach       — 未運搬の遺物に 5m 以内
///   5 RelicWithClimb      — 遺物運搬中に GrabPoint 近接
///   6 RopePlayerNearby    — 他プレイヤーに 4m 以内
///   7 Zone2Entry          — 標高 400m 到達
///   8 ReturnOrZone3       — 標高 800m 到達
/// </summary>
[DisallowMultipleComponent]
public class HintTriggerDriver : MonoBehaviour
{
    // ── GDD §21.2 定数（後で Inspector 調整が必要なら SerializeField 化する）──
    private const float POLL_INTERVAL          = 1f;    // ポーリング間隔（秒）
    private const float DASH_INTRO_DELAY       = 5f;    // ダッシュ紹介までの歩行継続秒数
    private const float CLIMB_PROXIMITY_RANGE  = 3f;    // GrabPoint ヒント発火距離
    private const float RELIC_PROXIMITY_RANGE  = 5f;    // 遺物発見ヒント発火距離
    private const float PLAYER_PROXIMITY_RANGE = 4f;    // ロープ接続ヒント発火距離
    private const float ZONE2_ALTITUDE         = 400f;  // ゾーン2（岩場帯）入口
    private const float ZONE3_ALTITUDE         = 800f;  // ゾーン3（急壁）入口
    private const int   PHYSICS_BUFFER_SIZE    = 16;

    // ── コンポーネント参照 ──────────────────────────────────
    [SerializeField] private StaminaSystem     _stamina;
    [SerializeField] private ClimbingController _climbing;
    [SerializeField] private RelicCarrier      _carrier;
    [SerializeField] private Transform         _playerTransform;

    // ── 状態 ────────────────────────────────────────────────
    private float _nextPollTime;
    private float _walkAccum;              // 累積歩行秒数（Hint 2 用）

    // Physics バッファ（GC フリー）
    private readonly Collider[] _overlapBuffer = new Collider[PHYSICS_BUFFER_SIZE];

    private void Awake()
    {
        if (_playerTransform == null) _playerTransform = transform;
        if (_stamina   == null) _stamina   = GetComponent<StaminaSystem>();
        if (_climbing  == null) _climbing  = GetComponent<ClimbingController>();
    }

    private void OnEnable()
    {
        if (_stamina != null)
            _stamina.OnExhausted += HandleStaminaExhausted;
    }

    private void OnDisable()
    {
        if (_stamina != null)
            _stamina.OnExhausted -= HandleStaminaExhausted;
    }

    // ── ポーリング ───────────────────────────────────────────
    private void Update()
    {
        AccumulateWalkTime();

        if (Time.unscaledTime < _nextPollTime) return;
        _nextPollTime = Time.unscaledTime + POLL_INTERVAL;

        var hintMgr = HintManager.Instance;
        if (hintMgr == null) return;

        // Hint 2: ダッシュ紹介（5 秒歩行後）
        if (_walkAccum >= DASH_INTRO_DELAY)
            hintMgr.TriggerHint(HintManager.HintId.DashIntroduction);

        // Zone-based（Y 座標参照）
        float altitude = _playerTransform.position.y;
        if (altitude >= ZONE2_ALTITUDE)
            hintMgr.TriggerHint(HintManager.HintId.Zone2Entry);
        if (altitude >= ZONE3_ALTITUDE)
            hintMgr.TriggerHint(HintManager.HintId.ReturnOrZone3);

        // 近接判定（GrabPoint・遺物・他プレイヤー）
        bool nearGrabPoint = IsAnythingNearby<GrabPoint>(CLIMB_PROXIMITY_RANGE);
        bool carryingRelic = _carrier != null && _carrier.IsBeingCarried;

        if (nearGrabPoint && !carryingRelic)
            hintMgr.TriggerHint(HintManager.HintId.FirstClimbApproach);

        if (nearGrabPoint && carryingRelic)
            hintMgr.TriggerHint(HintManager.HintId.RelicWithClimb);

        if (!carryingRelic && IsRelicNearby(RELIC_PROXIMITY_RANGE))
            hintMgr.TriggerHint(HintManager.HintId.RelicApproach);

        if (IsOtherPlayerNearby(PLAYER_PROXIMITY_RANGE))
            hintMgr.TriggerHint(HintManager.HintId.RopePlayerNearby);
    }

    // ── Hint 2 用歩行時間計測 ────────────────────────────────
    private void AccumulateWalkTime()
    {
        // MoveVector の大きさが 0.1 以上なら歩行中とみなす。
        Vector2 mv = InputStateReader.ReadMoveVectorRaw();
        if (mv.sqrMagnitude > 0.01f)
            _walkAccum += Time.unscaledDeltaTime;
    }

    // ── Hint 3 用 ─────────────────────────────────────────────
    private void HandleStaminaExhausted()
    {
        HintManager.Instance?.TriggerHint(HintManager.HintId.StaminaDepleted);
    }

    // ── 近接判定ヘルパー ─────────────────────────────────────
    private bool IsAnythingNearby<T>(float radius) where T : Component
    {
        int n = Physics.OverlapSphereNonAlloc(
            _playerTransform.position, radius, _overlapBuffer,
            ~0, QueryTriggerInteraction.Collide);

        for (int i = 0; i < n; i++)
        {
            if (_overlapBuffer[i] == null) continue;
            if (_overlapBuffer[i].GetComponentInParent<T>() != null)
                return true;
        }
        return false;
    }

    private bool IsRelicNearby(float radius)
    {
        int n = Physics.OverlapSphereNonAlloc(
            _playerTransform.position, radius, _overlapBuffer,
            ~0, QueryTriggerInteraction.Collide);

        for (int i = 0; i < n; i++)
        {
            if (_overlapBuffer[i] == null) continue;
            var rc = _overlapBuffer[i].GetComponentInParent<RelicCarrier>();
            if (rc != null && !rc.IsBeingCarried)
                return true;
        }
        return false;
    }

    private bool IsOtherPlayerNearby(float radius)
    {
        float r2 = radius * radius;
        IReadOnlyList<StaminaSystem> others = StaminaSystem.RegisteredPlayers;
        if (others == null) return false;

        for (int i = 0; i < others.Count; i++)
        {
            var s = others[i];
            if (s == null || s == _stamina) continue;
            if ((s.transform.position - _playerTransform.position).sqrMagnitude <= r2)
                return true;
        }
        return false;
    }
}
