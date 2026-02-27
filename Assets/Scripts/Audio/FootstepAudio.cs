using UnityEngine;

/// <summary>
/// プレイヤーの移動に連動して足音 SE を再生する MonoBehaviour。
/// ProximityAudioSource の AudioSource を経由して再生することで、
/// 距離減衰・エコーが自動的に適用される。
///
/// [再生ルール]
///   - 地面接地中かつ移動速度が stepThreshold 以上のとき、
///     _stepInterval 秒ごとに歩行 SE を再生する。
///   - 移動速度が runThreshold 以上のとき、走り SE を使用する（設定されている場合）。
///   - 着地時（接地フラグが false → true に変化したとき）に着地 SE を再生する。
///   - 再生間隔は実際の移動速度に反比例してスケールする
///     （速いほど足音の間隔が短くなる）。
///
/// [Owner 判定]
///   - 自分の足音は常に再生する（ProximityAudioSource の音量制御とは独立）。
///   - リモートプレイヤーの足音も再生され、ProximityAudioManager が音量を調整する。
///
/// [セットアップ]
///   1. PlayerPrefab に ProximityAudioSource と同じ GameObject にアタッチする。
///   2. Inspector の AudioClip フィールドに SE アセットを割り当てる（未設定でも動作する）。
///   3. PlayerMovement の moveSpeed に合わせて stepSpeed* パラメータを調整する。
/// </summary>
[RequireComponent(typeof(ProximityAudioSource))]
public class FootstepAudio : MonoBehaviour
{
    // ─────────────── Inspector: SE クリップ ───────────────

    [Header("足音クリップ（未設定でも動作する）")]
    [Tooltip("歩き足音のクリップリスト。ランダムに1つ選ばれる")]
    [SerializeField] private AudioClip[] _walkClips = System.Array.Empty<AudioClip>();

    [Tooltip("走り足音のクリップリスト。未設定時は walkClips を使用")]
    [SerializeField] private AudioClip[] _runClips = System.Array.Empty<AudioClip>();

    [Tooltip("着地 SE クリップ")]
    [SerializeField] private AudioClip _landClip;

    [Header("音量")]
    [Tooltip("歩き SE の音量スケール（ProximityAudioSource の baseVolume に乗算）")]
    [Range(0f, 1f)]
    [SerializeField] private float _walkVolume = 0.6f;

    [Tooltip("走り SE の音量スケール")]
    [Range(0f, 1f)]
    [SerializeField] private float _runVolume = 0.8f;

    [Tooltip("着地 SE の音量スケール")]
    [Range(0f, 1f)]
    [SerializeField] private float _landVolume = 1f;

    [Header("再生間隔")]
    [Tooltip("歩き速度（m/s）。この速度のとき stepBaseInterval 秒間隔で足音が鳴る")]
    [SerializeField] private float _walkSpeed = 2.5f;

    [Tooltip("走りと判定する速度閾値（m/s）。これ以上で走り SE を使用")]
    [SerializeField] private float _runSpeed = 4.5f;

    [Tooltip("歩き速度（_walkSpeed）のときの足音間隔（秒）")]
    [SerializeField] private float _stepBaseInterval = 0.5f;

    [Tooltip("最小再生間隔（秒）。高速移動時でもこれより短くならない）")]
    [SerializeField] private float _minStepInterval = 0.2f;

    [Tooltip("この速度（m/s）未満では足音を鳴らさない")]
    [SerializeField] private float _stepThreshold = 0.3f;

    // ─────────────── 内部状態 ───────────────

    private ProximityAudioSource _proximityAudioSource;
    private PlayerMovement       _movement;

    private float   _stepTimer;
    private bool    _wasGrounded;
    private Vector3 _lastPosition;

    // ─────────────── Unity Lifecycle ───────────────

    private void Awake()
    {
        _proximityAudioSource = GetComponent<ProximityAudioSource>();
        _movement             = GetComponent<PlayerMovement>();
        _lastPosition         = transform.position;
    }

    private void Update()
    {
        if (_proximityAudioSource == null || _movement == null) return;

        bool isGrounded     = _movement.IsGrounded;
        float currentSpeed  = CalcHorizontalSpeed();

        // 着地検出（前フレーム空中 → 今フレーム接地）
        if (!_wasGrounded && isGrounded)
            PlayLandSound();
        _wasGrounded = isGrounded;

        // 足音再生
        if (isGrounded && currentSpeed >= _stepThreshold)
        {
            float interval = CalcStepInterval(currentSpeed);
            _stepTimer += Time.deltaTime;

            if (_stepTimer >= interval)
            {
                _stepTimer = 0f;
                PlayStepSound(currentSpeed);
            }
        }
        else
        {
            // 停止中または空中ではタイマーをリセットしない（次の一歩をすぐ鳴らすため 0 に戻す）
            _stepTimer = 0f;
        }

        _lastPosition = transform.position;
    }

    // ─────────────── 内部処理 ───────────────

    /// <summary>前フレームからの水平移動速度（m/s）を計算する。</summary>
    private float CalcHorizontalSpeed()
    {
        Vector3 delta = transform.position - _lastPosition;
        delta.y = 0f;
        return delta.magnitude / Mathf.Max(Time.deltaTime, 0.001f);
    }

    /// <summary>現在速度から足音の再生間隔（秒）を計算する。</summary>
    private float CalcStepInterval(float speed)
    {
        if (speed <= 0f) return _stepBaseInterval;
        // 速度に反比例: walkSpeed 時 = stepBaseInterval
        float interval = _stepBaseInterval * (_walkSpeed / speed);
        return Mathf.Max(interval, _minStepInterval);
    }

    /// <summary>歩き or 走り足音を1つランダム再生する。</summary>
    private void PlayStepSound(float speed)
    {
        bool isRunning = speed >= _runSpeed;

        AudioClip clip;
        float volumeScale;

        if (isRunning && _runClips.Length > 0)
        {
            clip        = _runClips[Random.Range(0, _runClips.Length)];
            volumeScale = _runVolume;
        }
        else if (_walkClips.Length > 0)
        {
            clip        = _walkClips[Random.Range(0, _walkClips.Length)];
            volumeScale = _walkVolume;
        }
        else
        {
            return; // クリップ未設定
        }

        _proximityAudioSource.PlayOneShot(clip, volumeScale);
    }

    /// <summary>着地 SE を再生する。</summary>
    private void PlayLandSound()
    {
        if (_landClip == null) return;
        _proximityAudioSource.PlayOneShot(_landClip, _landVolume);
    }
}
