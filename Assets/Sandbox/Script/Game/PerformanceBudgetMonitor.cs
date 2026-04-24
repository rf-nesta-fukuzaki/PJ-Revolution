using UnityEngine;
using UnityEngine.Profiling;

namespace PeakPlunder.Game
{
    /// <summary>
    /// GDD §20.3 — パフォーマンス予算モニター。
    ///
    /// 開発ビルドでの超過検出を目的とする簡易モニター。
    /// 本番ビルドでは _activeInBuild が false の場合 Update をスキップ。
    ///
    /// 監視項目:
    ///   - FPS < 目標 (推奨 60fps / 最低 30fps)
    ///   - アクティブ Rigidbody > 200
    ///   - パーティクルシステム > 20
    ///   - メモリ使用量 > 4 GB
    ///
    /// しきい値を超過した場合 Debug.LogWarning で通知し、OnBudgetExceeded を発火する。
    /// </summary>
    public class PerformanceBudgetMonitor : MonoBehaviour
    {
        // GDD §20.3
        private const int   MAX_ACTIVE_RIGIDBODIES = 200;
        private const int   MAX_PARTICLE_SYSTEMS   = 20;
        private const long  MAX_MEMORY_BYTES       = 4L * 1024 * 1024 * 1024; // 4 GB
        private const float MIN_RECOMMENDED_FPS    = 60f;
        private const float MIN_ACCEPTABLE_FPS     = 30f;

        [Header("有効化")]
        [SerializeField] private bool _activeInBuild = false;   // 本番は false、開発は true
        [SerializeField] private float _sampleInterval = 2f;    // サンプリング間隔（秒）
        [SerializeField] private float _warnCooldown   = 10f;   // 警告のクールダウン（同項目）

        public event System.Action<string, float> OnBudgetExceeded;  // (category, measured)

        // FPS 計測
        private float _fpsAccum;
        private int   _fpsFrames;
        private float _sampleTimer;

        // 警告クールダウン
        private float _nextFpsWarn;
        private float _nextRbWarn;
        private float _nextPsWarn;
        private float _nextMemWarn;

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _ = _activeInBuild;
#else
            if (!_activeInBuild) return;
#endif
            _fpsAccum  += Time.unscaledDeltaTime;
            _fpsFrames += 1;

            _sampleTimer += Time.unscaledDeltaTime;
            if (_sampleTimer < _sampleInterval) return;

            float fps = _fpsFrames / _fpsAccum;
            _sampleTimer = 0f;
            _fpsAccum    = 0f;
            _fpsFrames   = 0;

            EvaluateFps(fps);
            EvaluateRigidbodies();
            EvaluateParticles();
            EvaluateMemory();
        }

        private void EvaluateFps(float fps)
        {
            if (fps < MIN_ACCEPTABLE_FPS && Time.time >= _nextFpsWarn)
            {
                Debug.LogWarning($"[PerfBudget] FPS が最低ラインを下回りました: {fps:F1}fps < {MIN_ACCEPTABLE_FPS}fps");
                OnBudgetExceeded?.Invoke("FPS_Critical", fps);
                _nextFpsWarn = Time.time + _warnCooldown;
            }
            else if (fps < MIN_RECOMMENDED_FPS && Time.time >= _nextFpsWarn)
            {
                Debug.LogWarning($"[PerfBudget] FPS が推奨ラインを下回りました: {fps:F1}fps < {MIN_RECOMMENDED_FPS}fps");
                OnBudgetExceeded?.Invoke("FPS_Recommended", fps);
                _nextFpsWarn = Time.time + _warnCooldown;
            }
        }

        private void EvaluateRigidbodies()
        {
            int count = CountActiveRigidbodies();
            if (count > MAX_ACTIVE_RIGIDBODIES && Time.time >= _nextRbWarn)
            {
                Debug.LogWarning($"[PerfBudget] アクティブ Rigidbody 超過: {count} > {MAX_ACTIVE_RIGIDBODIES}");
                OnBudgetExceeded?.Invoke("Rigidbody", count);
                _nextRbWarn = Time.time + _warnCooldown;
            }
        }

        private void EvaluateParticles()
        {
            var systems = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            int activeCount = 0;
            foreach (var ps in systems)
                if (ps != null && ps.isPlaying) activeCount++;

            if (activeCount > MAX_PARTICLE_SYSTEMS && Time.time >= _nextPsWarn)
            {
                Debug.LogWarning($"[PerfBudget] 同時 ParticleSystem 超過: {activeCount} > {MAX_PARTICLE_SYSTEMS}");
                OnBudgetExceeded?.Invoke("Particles", activeCount);
                _nextPsWarn = Time.time + _warnCooldown;
            }
        }

        private void EvaluateMemory()
        {
            long totalMemory = Profiler.GetTotalAllocatedMemoryLong();
            if (totalMemory > MAX_MEMORY_BYTES && Time.time >= _nextMemWarn)
            {
                float gb = totalMemory / (1024f * 1024f * 1024f);
                Debug.LogWarning($"[PerfBudget] メモリ使用量超過: {gb:F2} GB > 4.00 GB");
                OnBudgetExceeded?.Invoke("Memory", totalMemory);
                _nextMemWarn = Time.time + _warnCooldown;
            }
        }

        private static int CountActiveRigidbodies()
        {
            var bodies = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
            int active = 0;
            foreach (var rb in bodies)
            {
                if (rb == null) continue;
                if (!rb.IsSleeping() && !rb.isKinematic) active++;
            }
            return active;
        }
    }
}
