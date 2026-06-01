using UnityEngine;
using Sandbox.World.Integration;

namespace Sandbox.World.Environment
{
    /// <summary>
    /// 山頂到達で一度だけ多色紙吹雪を噴く祝祭コンポーネント（PEAK 風のドタバタお祝い）。
    /// 2系統のトリガに対応し、どちらのシーン構成でも確実に発火する:
    ///   ① <see cref="ExpeditionEvents.OnSummitReached"/> … フルゲームの SummitGoal トリガ経路。
    ///   ② プレイヤー(ExplorerController)が手続き山頂(ColliderBaker.GlobalMaxPos)へ近接 … combined シーンには
    ///      SummitGoal トリガが無い(autoAttachSpawners=0 でイベントが発火しない)ため、近接検出で補う。
    /// いずれも一度きり(<see cref="_fired"/> ガード)。SandboxBootstrap と同 GameObject へ自動付与される想定。
    /// 常時アンビエントの SummitVisualEffects とは別物のワンショットバースト。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SummitConfettiCelebration : MonoBehaviour
    {
        [Tooltip("山頂と判定するプレイヤーまでの距離[m]。")]
        [SerializeField] private float reachRadius = 10f;
        [Tooltip("近接チェック間隔[s]（毎フレームは不要）。")]
        [SerializeField] private float checkInterval = 0.5f;
        [Tooltip("紙吹雪のスケール。")]
        [SerializeField] private float confettiScale = 1.3f;
        [Tooltip("紙吹雪のバースト数。")]
        [SerializeField] private int confettiBurst = 90;

        private SandboxBootstrap _bootstrap;
        private float _timer;
        private bool _fired;
        private bool _pending; // イベントは来たが山頂未確定 → 確定次第 Update で発火

        private void Awake()
        {
            _bootstrap = GetComponent<SandboxBootstrap>();
            // 境界値のサニタイズ（checkInterval=0 だと毎フレーム走査になる等）。
            reachRadius   = Mathf.Max(0f, reachRadius);
            checkInterval = Mathf.Max(0.1f, checkInterval);
            confettiScale = Mathf.Max(0.1f, confettiScale);
        }

        private void OnEnable()  => ExpeditionEvents.OnSummitReached += HandleSummitEvent;
        private void OnDisable() => ExpeditionEvents.OnSummitReached -= HandleSummitEvent;

        // ① フルゲーム経路：イベント発火で即お祝い。山頂未確定なら保留し Update が確定後に発火（取りこぼし防止）。
        private void HandleSummitEvent(float clearTimeSeconds)
        {
            if (_fired) return;
            Vector3 summit = ResolveSummitPosition();
            if (summit != Vector3.zero) Fire(summit);
            else _pending = true;
        }

        // ② combined シーン経路：プレイヤーが手続き山頂へ到達したかを間引きチェック（＋保留イベントの遅延発火）。
        private void Update()
        {
            if (_fired) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = checkInterval;

            Vector3 summit = ResolveSummitPosition();
            if (summit == Vector3.zero) return; // まだ山頂が確定していない

            if (_pending) { Fire(summit); return; } // イベント受信済み・山頂確定 → 近接不問で発火

            var players = FindObjectsByType<ExplorerController>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p == null) continue;
                if (Vector3.Distance(p.transform.position, summit) <= reachRadius)
                {
                    Fire(summit);
                    return;
                }
            }
        }

        /// <summary>山頂のワールド座標を発火時に解決する（followHighestPeak で動き得るのでキャッシュしない）。</summary>
        private Vector3 ResolveSummitPosition()
        {
            // combined シーンの山頂の真実は ColliderBaker.GlobalMaxPos。
            if (_bootstrap != null && _bootstrap.ColliderBaker != null)
            {
                var p = _bootstrap.ColliderBaker.GlobalMaxPos;
                if (p != Vector3.zero) return p;
            }
            // フルゲームに SandboxSummitGoal があればそれを優先。
            var goal = FindFirstObjectByType<SandboxSummitGoal>();
            if (goal != null && goal.HasSummit) return goal.SummitPosition;
            return Vector3.zero;
        }

        private void Fire(Vector3 summit)
        {
            if (_fired || summit == Vector3.zero) return;
            _fired = true;
            StylizedImpactFx.Confetti(summit + Vector3.up * 2f, confettiScale, confettiBurst);
            Debug.Log($"[SummitConfettiCelebration] 山頂紙吹雪を発火 @ {summit}");
            enabled = false; // 一度きり。発火後は Update の近接走査を停止（OnDisable でイベントも解除）。
        }
    }
}
