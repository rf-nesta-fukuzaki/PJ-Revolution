using Unity.Netcode;
using UnityEngine;

/// <summary>
/// GDD §3.3 — プロキシミティボイスチャット（距離減衰モデル）。
///
/// 現在の実装: Unity Audio Source の空間音響を使ったシミュレーション。
///   - 各プレイヤーの AudioSource の maxDistance / rolloffMode を制御
///   - 緊急無線機使用時に距離制限を一時解除
///
/// 本番化 TODO: Vivox SDK または Photon Voice への差し替え
///   - Vivox: com.unity.services.vivox パッケージを追加
///   - Photon Voice: com.exitgames.photonvoice2 パッケージを追加
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class ProximityVoiceChat : NetworkBehaviour
{
    // ── シングルトン（ローカルオーナーのコンポーネントを指す）──────
    /// <summary>自プレイヤーの ProximityVoiceChat。他プレイヤーのものは含まない。</summary>
    public static ProximityVoiceChat Instance { get; private set; }

    // ── Inspector ───────────────────────────────────────────────
    [Header("距離設定")]
    [SerializeField] private float _defaultMaxDistance   = 15f;  // 通常の聞こえる距離
    [SerializeField] private float _emergencyMaxDistance = 200f; // 緊急無線機使用時
    [SerializeField] private AudioRolloffMode _rolloffMode = AudioRolloffMode.Linear;

    [Header("妨害遺物")]
    [SerializeField] private float _jammingRadius = 8f;  // 歌う壺などの妨害半径

    // ── 同期変数 ─────────────────────────────────────────────────
    /// <summary>緊急無線機の残り有効時間（サーバー権威）。</summary>
    private readonly NetworkVariable<float> _emergencyRadioTimer = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ── ローカル状態 ─────────────────────────────────────────────
    private AudioSource   _voiceSource;
    private IVoiceBackend _voiceBackend;                    // Vivox / Photon Voice / null(フォールバック)
    private bool          _isJammed;
    private float         _currentJammingLevel;             // 0〜1: 現在の妨害強度
    private float         _shoutReportTimer;
    private const float   SHOUT_REPORT_INTERVAL = 5f;       // ScoreTracker への報告間隔（秒）
    private const float   SHOUT_JAM_THRESHOLD   = 0.6f;     // この妨害強度以上を「叫び」と見なす

    public bool  IsEmergencyActive => _emergencyRadioTimer.Value > 0f;
    public bool  IsJammed          => _isJammed;
    public float JammingLevel      => _currentJammingLevel;

    // ── ライフサイクル ────────────────────────────────────────────
    private void Awake()
    {
        _voiceSource      = GetComponent<AudioSource>();
        _shoutReportTimer = SHOUT_REPORT_INTERVAL;
        if (_voiceSource == null)
            _voiceSource = gameObject.AddComponent<AudioSource>();

        ConfigureAudioSource(_defaultMaxDistance);
    }

    public override void OnNetworkSpawn()
    {
        _emergencyRadioTimer.OnValueChanged += OnEmergencyTimerChanged;

        if (IsOwner)
        {
            Instance = this;
            // リアル音声バックエンドを初期化（SDK 未インストール時は null = AudioSource フォールバック）
            _voiceBackend = VoiceBackendFactory.Create(gameObject);
            _voiceBackend?.JoinChannel("expedition", OwnerClientId.ToString());
        }
    }

    public override void OnNetworkDespawn()
    {
        _emergencyRadioTimer.OnValueChanged -= OnEmergencyTimerChanged;

        if (IsOwner && Instance == this)
        {
            _voiceBackend?.LeaveChannel();
            _voiceBackend?.Dispose();
            _voiceBackend = null;
            Instance = null;
        }
    }

    /// <summary>緊急無線機アイテムから呼び出す互換 API。</summary>
    public void SetRangeOverride(bool active)
    {
        ActivateEmergencyRadioServerRpc(active ? _emergencyMaxDistance : 0f);
    }

    private void Update()
    {
        if (IsServer && _emergencyRadioTimer.Value > 0f)
        {
            _emergencyRadioTimer.Value -= Time.deltaTime;
            if (_emergencyRadioTimer.Value < 0f)
                _emergencyRadioTimer.Value = 0f;
        }

        CheckJamming();
    }

    // ── 緊急無線機起動（クライアント → サーバー）────────────────
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ActivateEmergencyRadioServerRpc(float duration = 30f)
    {
        _emergencyRadioTimer.Value = duration;
        Debug.Log($"[VoiceChat] 緊急無線機起動: {duration}秒");
    }

    // ── タイマー変化コールバック ──────────────────────────────────
    private void OnEmergencyTimerChanged(float oldVal, float newVal)
    {
        bool wasActive = oldVal > 0f;
        bool isActive  = newVal > 0f;

        if (isActive && !wasActive)
        {
            ConfigureAudioSource(_emergencyMaxDistance);
            Debug.Log("[VoiceChat] 緊急無線機 ON → 全域ボイスチャット有効");
        }
        else if (!isActive && wasActive)
        {
            ConfigureAudioSource(_jammingRadius > 0 && _isJammed
                ? 0f : _defaultMaxDistance);
            Debug.Log("[VoiceChat] 緊急無線機 OFF → 通常範囲に戻る");
        }
    }

    // ── 妨害チェック（歌う壺 SingingVaseRelic 周辺は音が乱れる）──
    private void CheckJamming()
    {
        if (IsEmergencyActive)
        {
            // 緊急無線中は妨害を即時解除
            if (_isJammed) ClearJamming();
            return;
        }

        float totalInterference = 0f;

        foreach (var vase in SingingVaseRelic.RegisteredRelics)
        {
            if (vase == null) continue;
            float dist = Vector3.Distance(transform.position, vase.transform.position);
            if (dist >= _jammingRadius) continue;

            // 距離に反比例した近接係数 × 壺の実際の音量（揺れ強度）
            float proximity = 1f - dist / _jammingRadius;
            totalInterference += proximity * vase.VoiceChatInterference;
        }

        totalInterference = Mathf.Clamp01(totalInterference);
        bool nowJammed = totalInterference > 0.05f;

        // 状態変化または強度変化が閾値を超えたとき適用
        if (nowJammed != _isJammed || Mathf.Abs(totalInterference - _currentJammingLevel) > 0.02f)
        {
            _isJammed            = nowJammed;
            _currentJammingLevel = totalInterference;
            ApplyJamming(totalInterference);
        }

        // 強妨害中は「叫び」として ScoreTracker に報告（IsOwner のみ）
        if (IsOwner && totalInterference >= SHOUT_JAM_THRESHOLD)
        {
            _shoutReportTimer -= Time.deltaTime;
            if (_shoutReportTimer <= 0f)
            {
                _shoutReportTimer = SHOUT_REPORT_INTERVAL;
                ScoreTracker.Instance?.RecordShout((int)OwnerClientId);
            }
        }
        else
        {
            _shoutReportTimer = Mathf.Min(_shoutReportTimer, SHOUT_REPORT_INTERVAL);
        }
    }

    private void ClearJamming()
    {
        _isJammed            = false;
        _currentJammingLevel = 0f;
        ApplyJamming(0f);
    }

    /// <summary>妨害強度（0=無し / 1=最大）に応じてボリュームとピッチをグラデーション制御する。</summary>
    private void ApplyJamming(float level)
    {
        float vol;
        float pitch;

        if (level <= 0.05f)
        {
            vol   = 1f;
            pitch = 1f;
            if (_isJammed) Debug.Log("[VoiceChat] 妨害解除");
        }
        else
        {
            // 妨害強度に比例してボリューム低下・ピッチ乱れ
            vol   = Mathf.Lerp(1f, Random.Range(0f, 0.15f), level);
            pitch = Mathf.Lerp(1f, Random.Range(0.5f, 1.6f), level);
            Debug.Log($"[VoiceChat] 妨害中 強度:{level:F2}（歌う壺）");
        }

        // AudioSource フォールバック
        if (_voiceSource != null)
        {
            _voiceSource.volume = vol;
            _voiceSource.pitch  = pitch;
        }

        // リアル音声バックエンドにも反映（SDK 送信音量を絞る）
        _voiceBackend?.SetTransmitVolume(vol);
    }

    // ── AudioSource 設定ヘルパー ─────────────────────────────────
    private void ConfigureAudioSource(float maxDist)
    {
        if (_voiceSource == null) return;

        _voiceSource.spatialBlend = 1f;          // 3D音響
        _voiceSource.rolloffMode  = _rolloffMode;
        _voiceSource.maxDistance  = maxDist;
        _voiceSource.minDistance  = 1f;
        _voiceSource.loop         = false;
        _voiceSource.playOnAwake  = false;
    }

    // ── デバッグ Gizmos ──────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _defaultMaxDistance);

        if (_jammingRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, _jammingRadius);
        }
    }
}
