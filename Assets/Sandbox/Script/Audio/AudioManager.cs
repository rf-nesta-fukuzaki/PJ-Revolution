using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace PeakPlunder.Audio
{
    /// <summary>
    /// GDD §15 — サウンド管理のシングルトン。
    ///   * BGM: ゾーン切替時に 2 秒クロスフェード（§15.1）
    ///   * SE:  SoundId + 位置指定で 3D / 2D 再生（§15.2）
    ///   * AudioMixer: Master / BGM / SE / Voice / Environment (§15.3)
    ///
    /// 使用方法:
    ///   AudioManager.Instance.PlaySE(SoundId.FootstepWalk, transform.position);
    ///   AudioManager.Instance.PlayBGM(zoneBgm);
    ///
    /// プレハブを起動シーンに配置するか、GameServices 経由で初期化する。
    /// DontDestroyOnLoad でシーン遷移中も保持される。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Library (GDD §15.2)")]
        [SerializeField] private SoundLibrary _library;

        [Header("AudioMixer (GDD §15.3)")]
        [SerializeField] private AudioMixer _mixer;
        [SerializeField] private AudioMixerGroup _bgmGroup;
        [SerializeField] private AudioMixerGroup _seGroup;
        [SerializeField] private AudioMixerGroup _environmentGroup;

        [Header("Pool")]
        [Tooltip("3D SE 用 AudioSource プール上限。GDD §15.3 の最大距離 50m を想定した同時発声数。")]
        [SerializeField, Min(4)] private int _sePoolSize = 24;

        [Tooltip("SE の最大距離（m）— 通常 SE (§15.3)")]
        [SerializeField, Min(1f)] private float _seMaxDistance = 50f;

        [Tooltip("SE の最小距離（m）— 3D Rolloff のフルボリューム範囲 (§15.3)")]
        [SerializeField, Min(0.1f)] private float _seMinDistance = 2f;

        [Header("BGM Crossfade (GDD §15.1)")]
        [SerializeField, Min(0.1f)] private float _bgmCrossfadeDuration = 2f;

        // SoundId → SoundEntry 辞書
        private Dictionary<SoundId, SoundLibrary.SoundEntry> _lookup;

        // SE プール（AudioSource 使い回し）
        private readonly List<AudioSource> _sePool = new();

        // 同時発声数カウント（SoundId ごとに maxConcurrent で上限制御）
        private readonly Dictionary<SoundId, int> _activeCount = new();

        // BGM 用 AudioSource（2 本でクロスフェード）
        private AudioSource _bgmA;
        private AudioSource _bgmB;
        private AudioSource _bgmCurrent;

        // GDD §15.1 — 天候連動ダック用（吹雪中 -40% など）
        private float _bgmBaseVolume = 0.5f;
        private float _bgmVolumeScale = 1f;
        private Coroutine _bgmScaleCoroutine;
        private const float BGM_SCALE_FADE_DURATION = 1.5f;

        /// <summary>GDD §15.1 — 現在の BGM ダック倍率（0〜1）。</summary>
        public float BgmVolumeScale => _bgmVolumeScale;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeLookup();
            InitializeSePool();
            InitializeBgmSources();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void InitializeLookup()
        {
            _lookup = _library != null
                ? _library.BuildLookup()
                : new Dictionary<SoundId, SoundLibrary.SoundEntry>();
        }

        private void InitializeSePool()
        {
            for (int i = 0; i < _sePoolSize; i++)
            {
                var go = new GameObject($"SE_Src_{i}");
                go.transform.SetParent(transform);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.minDistance = _seMinDistance;
                src.maxDistance = _seMaxDistance;
                src.dopplerLevel = 0f; // GDD §15.3 ドップラー OFF
                src.outputAudioMixerGroup = _seGroup;
                _sePool.Add(src);
            }
        }

        private void InitializeBgmSources()
        {
            _bgmA = CreateBgmSource("BGM_A");
            _bgmB = CreateBgmSource("BGM_B");
            _bgmCurrent = _bgmA;
        }

        private AudioSource CreateBgmSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f; // BGM は 2D
            src.outputAudioMixerGroup = _bgmGroup;
            return src;
        }

        // ── SE 再生 ──────────────────────────────────────────────
        /// <summary>3D 位置指定で SE を再生（GDD §15.2）。</summary>
        public void PlaySE(SoundId id, Vector3 worldPosition, float volumeScale = 1f)
        {
            if (!TryGetEntry(id, out var entry)) return;
            if (!AcquireConcurrentSlot(id, entry.maxConcurrent)) return;

            var src = GetFreeSeSource();
            if (src == null)
            {
                ReleaseConcurrentSlot(id);
                return;
            }

            ConfigureSource(src, entry, volumeScale);
            src.transform.position = worldPosition;
            src.Play();
            StartCoroutine(ReleaseSlotWhenFinished(id, src, entry));
        }

        /// <summary>2D 再生（UI SE など）。spatialBlend はエントリ設定を上書きして 0 にする。</summary>
        public void PlaySE2D(SoundId id, float volumeScale = 1f)
        {
            if (!TryGetEntry(id, out var entry)) return;
            if (!AcquireConcurrentSlot(id, entry.maxConcurrent)) return;

            var src = GetFreeSeSource();
            if (src == null)
            {
                ReleaseConcurrentSlot(id);
                return;
            }

            ConfigureSource(src, entry, volumeScale);
            src.spatialBlend = 0f; // 強制 2D
            src.Play();
            StartCoroutine(ReleaseSlotWhenFinished(id, src, entry));
        }

        private void ConfigureSource(AudioSource src, SoundLibrary.SoundEntry entry, float volumeScale)
        {
            src.clip = entry.clip;
            src.volume = entry.volume * Mathf.Clamp01(volumeScale);
            src.spatialBlend = entry.spatialBlend;
            src.loop = entry.loop;
            src.pitch = 1f + Random.Range(-entry.pitchVariation, entry.pitchVariation);
            src.outputAudioMixerGroup = entry.mixerGroup != null ? entry.mixerGroup : _seGroup;
        }

        private AudioSource GetFreeSeSource()
        {
            foreach (var src in _sePool)
            {
                if (!src.isPlaying) return src;
            }
            return null;
        }

        private bool AcquireConcurrentSlot(SoundId id, int maxConcurrent)
        {
            int current = _activeCount.TryGetValue(id, out var c) ? c : 0;
            if (current >= maxConcurrent) return false;
            _activeCount[id] = current + 1;
            return true;
        }

        private void ReleaseConcurrentSlot(SoundId id)
        {
            if (!_activeCount.TryGetValue(id, out var c)) return;
            if (c <= 1) _activeCount.Remove(id);
            else        _activeCount[id] = c - 1;
        }

        private IEnumerator ReleaseSlotWhenFinished(SoundId id, AudioSource src, SoundLibrary.SoundEntry entry)
        {
            // ループ SE は明示的に StopLoop() されるまで解放しない
            if (entry.loop) yield break;

            // 再生終了まで待機（ソース使い回し時にも対応するため isPlaying を確認）
            while (src != null && src.isPlaying) yield return null;
            ReleaseConcurrentSlot(id);
        }

        /// <summary>ループ SE を個別に停止する（例: wind_ambient / stamina_warning）。</summary>
        public void StopLoop(SoundId id)
        {
            if (!TryGetEntry(id, out var entry) || !entry.loop) return;

            foreach (var src in _sePool)
            {
                if (src.isPlaying && src.clip == entry.clip)
                {
                    src.Stop();
                    ReleaseConcurrentSlot(id);
                }
            }
        }

        private bool TryGetEntry(SoundId id, out SoundLibrary.SoundEntry entry)
        {
            entry = null;
            if (id == SoundId.None) return false;
            if (_lookup == null || !_lookup.TryGetValue(id, out entry) || entry.clip == null)
            {
                // Clip 未設定は警告のみ。呼び出し側をクラッシュさせない。
                return false;
            }
            return true;
        }

        // ── BGM ──────────────────────────────────────────────────
        /// <summary>BGM をクロスフェード再生（GDD §15.1 — 2 秒）。</summary>
        public void PlayBGM(AudioClip clip, float volume = 0.5f)
        {
            if (clip == null) return;

            AudioSource next = _bgmCurrent == _bgmA ? _bgmB : _bgmA;
            AudioSource prev = _bgmCurrent;

            next.clip = clip;
            next.volume = 0f;
            next.Play();

            _bgmBaseVolume = volume;
            StartCoroutine(CrossfadeBgm(prev, next, volume * _bgmVolumeScale));
            _bgmCurrent = next;
        }

        private IEnumerator CrossfadeBgm(AudioSource from, AudioSource to, float targetVolume)
        {
            float elapsed = 0f;
            float startFromVol = from != null ? from.volume : 0f;

            while (elapsed < _bgmCrossfadeDuration)
            {
                float t = elapsed / _bgmCrossfadeDuration;
                if (from != null) from.volume = Mathf.Lerp(startFromVol, 0f, t);
                if (to != null)   to.volume   = Mathf.Lerp(0f, targetVolume, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (from != null) { from.volume = 0f; from.Stop(); }
            if (to != null)   to.volume = targetVolume;
        }

        /// <summary>BGM を即時停止（フェードアウトあり）。</summary>
        public void StopBGM()
        {
            if (_bgmCurrent == null) return;
            StartCoroutine(CrossfadeBgm(_bgmCurrent, null, 0f));
        }

        /// <summary>
        /// GDD §15.1 — BGM ダック（天候連動）。scale=0.6 で吹雪中 -40%。
        /// 値は 0〜1 にクランプされ、<see cref="BGM_SCALE_FADE_DURATION"/> 秒かけて滑らかに適用される。
        /// </summary>
        public void SetBGMVolumeScale(float scale)
        {
            scale = Mathf.Clamp01(scale);
            if (Mathf.Approximately(scale, _bgmVolumeScale)) return;

            _bgmVolumeScale = scale;
            if (_bgmScaleCoroutine != null) StopCoroutine(_bgmScaleCoroutine);
            _bgmScaleCoroutine = StartCoroutine(ApplyBgmScale());
        }

        private IEnumerator ApplyBgmScale()
        {
            if (_bgmCurrent == null) yield break;

            float startVol = _bgmCurrent.volume;
            float target   = _bgmBaseVolume * _bgmVolumeScale;
            float elapsed  = 0f;

            while (elapsed < BGM_SCALE_FADE_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / BGM_SCALE_FADE_DURATION);
                if (_bgmCurrent != null)
                    _bgmCurrent.volume = Mathf.Lerp(startVol, target, t);
                yield return null;
            }

            if (_bgmCurrent != null) _bgmCurrent.volume = target;
            _bgmScaleCoroutine = null;
        }
    }
}
