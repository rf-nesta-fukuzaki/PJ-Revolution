using UnityEngine;

/// <summary>
/// プレイヤー 1 人分の近接音声出力コンポーネント。
/// AudioSource と AudioReverbFilter を管理し、
/// ProximityAudioManager から受け取った音量・リバーブ値を毎フレーム適用する。
///
/// [設計方針]
///   - シングルプレイのため、常にローカルプレイヤーとして扱う。
///   - AudioReverbFilter で洞窟エコーを演出する。
/// </summary>
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(AudioReverbFilter))]
public class ProximityAudioSource : MonoBehaviour
{
    // ─────────────── Inspector ───────────────

    [Header("デフォルト音量")]
    [Tooltip("基準音量")]
    [Range(0f, 1f)]
    [SerializeField] private float _baseVolume = 1f;

    [Header("エコー設定")]
    [Tooltip("近距離（ドライ）時の AudioReverbFilter reverbLevel (dB)。-10000 で完全オフ")]
    [SerializeField] private float _dryReverbLevel = -10000f;

    [Tooltip("遠距離（エコー最大）時の AudioReverbFilter reverbLevel (dB)。0 付近で最大エコー")]
    [SerializeField] private float _wetReverbLevel = 0f;

    // ─────────────── コンポーネント参照 ───────────────

    private AudioSource       _audioSource;
    private AudioReverbFilter _reverbFilter;

    // ─────────────── 公開プロパティ ───────────────

    /// <summary>このプレイヤーの AudioSource。FootstepAudio 等が SE を鳴らすために参照する。</summary>
    public AudioSource AudioSource => _audioSource;

    /// <summary>シングルプレイでは常に true（ローカルプレイヤー）。</summary>
    public bool IsLocalPlayer => true;

    // ─────────────── Unity Lifecycle ───────────────

    private void Awake()
    {
        _audioSource  = GetComponent<AudioSource>();
        _reverbFilter = GetComponent<AudioReverbFilter>();

        _audioSource.spatialBlend = 1f;
        _audioSource.rolloffMode  = AudioRolloffMode.Linear;
        _audioSource.minDistance  = 1f;
        _audioSource.maxDistance  = 50f;

        _reverbFilter.reverbLevel = _dryReverbLevel;
        _reverbFilter.enabled     = true;
    }

    // ─────────────── 公開 API ───────────────

    /// <summary>距離に基づいて音量とリバーブ強度を適用する。</summary>
    public void ApplyProximity(float volume, float reverbBlend)
    {
        _audioSource.volume = _baseVolume * volume;

        float reverbLevel = Mathf.Lerp(_dryReverbLevel, _wetReverbLevel, reverbBlend);
        _reverbFilter.reverbLevel = reverbLevel;
    }

    /// <summary>フル音量・ドライ状態にリセットする。</summary>
    public void ResetToFull()
    {
        _audioSource.volume       = _baseVolume;
        _reverbFilter.reverbLevel = _dryReverbLevel;
    }

    /// <summary>指定した AudioClip を OneShot で再生する。</summary>
    public void PlayOneShot(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null) return;
        _audioSource.PlayOneShot(clip, volumeScale);
    }
}
