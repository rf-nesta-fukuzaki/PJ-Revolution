using UnityEngine;
using PeakPlunder.Audio;

namespace Sandbox.World.Zipline
{
    /// <summary>
    /// プレイヤーをジップラインに搭乗させ、ケーブル沿いに走行させて反対側ステーションで降車させる。
    /// 拠点側で乗れば「登り」（動力付きトロリー）、チェックポイント側で乗れば「下り」（重力アシストで加速）。
    /// 搭乗中は通常移動系（<see cref="ExplorerController"/> など）を無効化し、Rigidbody を kinematic 化して
    /// ケーブル曲線へ追従させる。降車時に原状復帰して地表へ接地させる。
    ///
    /// <see cref="ExplorerController.Awake"/> から各プレイヤーへ自動付与される（非破壊・冪等）。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ZiplineRider : MonoBehaviour
    {
        [Header("搭乗・走行")]
        [Tooltip("ステーション足場へこの距離[m]以内で E を押すと搭乗できる。")]
        [SerializeField] private float _mountRange = 4.5f;
        [Tooltip("ケーブルからプレイヤーがぶら下がる距離[m]。")]
        [SerializeField] private float _hangOffset = 1.9f;
        [Tooltip("動力トロリーの基本速度[m/s]（登り・水平）。")]
        [SerializeField] private float _baseSpeed = 12f;
        [Tooltip("下り時の速度倍率（重力アシスト）。")]
        [SerializeField] private float _downhillMultiplier = 1.8f;

        private enum State { Free, Riding }

        private Rigidbody _rb;
        private PlayerHealthSystem _health;
        private int _inputSlot;

        private State _state = State.Free;
        private Zipline _line;
        private float _t;        // 現在の param 位置
        private float _targetT;  // 目的端（0 or 1）
        private float _paramSpeed;

        // 原状復帰用のキャッシュ。
        private bool _prevKinematic;
        private MonoBehaviour[] _suppressed;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _health = GetComponent<PlayerHealthSystem>();
        }

        private void OnDisable()
        {
            // 何かの拍子に無効化された場合も搭乗状態を残さない。
            if (_state == State.Riding) Dismount(transform.position, Vector3.zero);
        }

        private void Update()
        {
            _inputSlot = LocalCoopPartyMember.ResolveInputSlot(this);
            if (_inputSlot < 0) return;

            if (_health != null && _health.IsDead)
            {
                if (_state == State.Riding) AbortRide();
                return;
            }

            if (_state == State.Riding)
            {
                TickRide();
                return;
            }

            // 自由移動中：ステーション付近で E 押下 → 搭乗。
            if (InputStateReader.InteractPressedThisFrame(_inputSlot))
                TryMount();
        }

        // ── 搭乗 ─────────────────────────────────────────────────
        private void TryMount()
        {
            var net = ZiplineNetwork.Instance;
            if (net == null) return;

            Zipline best = null;
            bool bestEndB = false;
            float bestDist = _mountRange;

            foreach (var kv in net.Lines)
            {
                var zip = kv.Value;
                if (zip == null) continue;

                float dA = Vector3.Distance(transform.position, zip.StationA);
                if (dA < bestDist) { bestDist = dA; best = zip; bestEndB = false; }

                float dB = Vector3.Distance(transform.position, zip.StationB);
                if (dB < bestDist) { bestDist = dB; best = zip; bestEndB = true; }
            }

            if (best == null) return;
            Mount(best, bestEndB);
        }

        private void Mount(Zipline zip, bool startEndB)
        {
            _line = zip;
            _t = startEndB ? 1f : 0f;
            _targetT = startEndB ? 0f : 1f;

            // 全長から param 速度を算出。下りなら重力アシストで加速。
            float startY = zip.SampleCable(_t).y;
            float endY = zip.SampleCable(_targetT).y;
            bool downhill = endY < startY - 0.5f;
            float speed = _baseSpeed * (downhill ? _downhillMultiplier : 1f);
            _paramSpeed = speed / Mathf.Max(1f, zip.CableLength);

            SuppressLocomotion(true);
            _prevKinematic = _rb.isKinematic;
            _rb.isKinematic = true;

            _state = State.Riding;

            // 初期位置をケーブルへスナップ。
            ApplyRidePose();

            // 走行音：起動ワンショット → ループ走行音（WinchLoop）。
            GameServices.Audio?.PlaySE(SoundId.WinchStart, transform.position);
            StartRideLoop();
            Debug.Log($"[Zipline] CP{zip.CheckpointIndex + 1} ラインに搭乗（{(downhill ? "下り" : "登り")}）");
        }

        // ── 走行 ─────────────────────────────────────────────────
        private void TickRide()
        {
            if (_line == null)
            {
                AbortRide();
                return;
            }

            // 走行中に E / Space を押したら途中下車。
            if (InputStateReader.InteractPressedThisFrame(_inputSlot)
                || InputStateReader.JumpPressedThisFrame(_inputSlot))
            {
                DismountAtCurrent();
                return;
            }

            _t = Mathf.MoveTowards(_t, _targetT, _paramSpeed * Time.deltaTime);
            ApplyRidePose();

            if (Mathf.Approximately(_t, _targetT))
                DismountAtStation();
        }

        private void ApplyRidePose()
        {
            Vector3 cable = _line.SampleCable(_t);
            Vector3 pose = cable + Vector3.down * _hangOffset;
            _rb.position = pose;
            transform.position = pose;
            _line.SetTrolley(_t);
        }

        // ── 降車 ─────────────────────────────────────────────────
        private void DismountAtStation()
        {
            bool atEndB = _targetT >= 0.5f;
            Vector3 ground = _line.StationGround(atEndB);
            _line.ParkTrolley(_targetT);

            // ジップラインが設置されているステーション（足場）に降りる。前方へ跳ねさせない（ホップ無し）。
            // 足場中央には支柱が立つため、支柱を避けて到着の延長方向（相手ステーションと反対側）へ
            // わずかにずらした足場上へ着地させる（足場は 2.6m 角なので中心から ~0.9m は確実に板の上）。
            Vector3 outward = ground - (atEndB ? _line.StationA : _line.StationB);
            outward.y = 0f;
            outward = outward.sqrMagnitude > 0.01f ? outward.normalized : transform.forward;
            Vector3 landing = ground + outward * 0.9f + Vector3.up * 1.0f;
            SnapToGround(ref landing);

            Dismount(landing, Vector3.zero);
            GameServices.Audio?.PlaySE(SoundId.LandSoft, landing);

            // チェックポイント側に着いたら到達 SE。
            if (atEndB)
                GameServices.Audio?.PlaySE(SoundId.Checkpoint, landing);
        }

        private void DismountAtCurrent()
        {
            Vector3 pos = _line != null ? _line.SampleCable(_t) + Vector3.down * _hangOffset : transform.position;
            if (_line != null) _line.ParkTrolley(_t > 0.5f ? 1f : 0f);
            SnapToGround(ref pos);
            Dismount(pos, Vector3.zero);
        }

        private void AbortRide()
        {
            Vector3 pos = transform.position;
            SnapToGround(ref pos);
            Dismount(pos, Vector3.zero);
        }

        private void Dismount(Vector3 position, Vector3 exitVelocity)
        {
            StopRideLoop();
            _rb.isKinematic = _prevKinematic;
            _rb.position = position;
            transform.position = position;
            if (!_rb.isKinematic)
            {
                _rb.linearVelocity = exitVelocity;
                _rb.angularVelocity = Vector3.zero;
            }

            SuppressLocomotion(false);
            _state = State.Free;
            _line = null;
        }

        // ── 走行ループ音（WinchLoop） ──────────────────────────────
        private bool _loopPlaying;

        private void StartRideLoop()
        {
            if (_loopPlaying) return;
            // WinchLoop は SoundLibrary で loop=true。2D 再生で乗車中ずっと一定音量のモーター音を流す。
            GameServices.Audio?.PlaySE2D(SoundId.WinchLoop);
            _loopPlaying = true;
        }

        private void StopRideLoop()
        {
            if (!_loopPlaying) return;
            GameServices.Audio?.StopLoop(SoundId.WinchLoop);
            _loopPlaying = false;
        }

        // ── 補助 ─────────────────────────────────────────────────
        private void SnapToGround(ref Vector3 pos)
        {
            Vector3 origin = pos + Vector3.up * 60f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
                pos.y = hit.point.y + 1.0f;
        }

        /// <summary>
        /// 通常移動・登攀・ロープ系コンポーネントを一括で無効/有効化する。搭乗中は本コンポーネントだけが
        /// Rigidbody を制御するようにし、降車時に元の有効状態へ戻す。
        /// </summary>
        private void SuppressLocomotion(bool suppress)
        {
            if (suppress)
            {
                var list = new System.Collections.Generic.List<MonoBehaviour>();
                AddIfPresent<ExplorerController>(list);
                AddIfPresent<WireRopeActionController>(list);
                AddIfPresent<ScrambleClimbController>(list);
                AddIfPresent<ClimbingController>(list);
                _suppressed = list.ToArray();
                foreach (var mb in _suppressed)
                    if (mb != null) mb.enabled = false;
            }
            else if (_suppressed != null)
            {
                foreach (var mb in _suppressed)
                    if (mb != null) mb.enabled = true;
                _suppressed = null;
            }
        }

        private void AddIfPresent<T>(System.Collections.Generic.List<MonoBehaviour> list) where T : MonoBehaviour
        {
            var c = GetComponent<T>();
            if (c != null && c.enabled) list.Add(c);
        }
    }
}
