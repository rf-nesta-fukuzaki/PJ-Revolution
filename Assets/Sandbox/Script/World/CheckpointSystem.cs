using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.World
{
/// <summary>
/// チェックポイントの記録・リスポーン管理。
/// UI への直接依存を排除し、イベントで通知する。
/// </summary>
public class CheckpointSystem : MonoBehaviour, ICheckpointProgressService
{
    private static CheckpointSystem _instance;

    [System.Obsolete("GameServices.Checkpoints を使用してください")]
    public static CheckpointSystem Instance => _instance;

    /// <summary>チェックポイント通過時 (index, displayMessage)。</summary>
    public event Action<int, string> CheckpointReached;

    /// <summary>チェックポイント進捗が変化した際 (currentIndex, totalCount)。</summary>
    public event Action<int, int> CheckpointProgressChanged;

    /// <summary>リスポーン演出開始。</summary>
    public event Action RespawnStarted;

    /// <summary>リスポーン演出完了。</summary>
    public event Action RespawnCompleted;

    int ICheckpointProgressService.CurrentCheckpointIndex => _currentCheckpointIndex;
    int ICheckpointProgressService.TotalCheckpoints => _checkpoints.Count;
    event Action<int, string> ICheckpointProgressService.CheckpointReached
    {
        add => CheckpointReached += value;
        remove => CheckpointReached -= value;
    }
    event Action<int, int> ICheckpointProgressService.CheckpointProgressChanged
    {
        add => CheckpointProgressChanged += value;
        remove => CheckpointProgressChanged -= value;
    }
    event Action ICheckpointProgressService.RespawnStarted
    {
        add => RespawnStarted += value;
        remove => RespawnStarted -= value;
    }
    event Action ICheckpointProgressService.RespawnCompleted
    {
        add => RespawnCompleted += value;
        remove => RespawnCompleted -= value;
    }

    void ICheckpointProgressService.RegisterCheckpoint(Transform checkpoint) => RegisterCheckpoint(checkpoint);
    void ICheckpointProgressService.RecordCheckpoint(int index) => OnCheckpointReached(index);

    [Header("設定 (ScriptableObject — 未設定時は Inspector デフォルト)")]
    [SerializeField] private CheckpointConfigSO _config;

    [Header("Respawn (Config 未設定時のフォールバック)")]
    [SerializeField] private float respawnDelay = 1.5f;
    [SerializeField] private float fallDeathY = -20f;
    [SerializeField] private float respawnHeightOffset = 2f;
    [SerializeField] private float respawnGroundProbeHeight = 120f;
    [SerializeField] private float respawnGroundProbeDistance = 300f;

    private float RespawnDelayValue => _config != null ? _config.RespawnDelay : respawnDelay;
    private float FallDeathYValue => _config != null ? _config.FallDeathY : fallDeathY;
    private float RespawnHeightOffsetValue => _config != null ? _config.RespawnHeightOffset : respawnHeightOffset;
    private float RespawnGroundProbeHeightValue => _config != null ? _config.RespawnGroundProbeHeight : respawnGroundProbeHeight;
    private float RespawnGroundProbeDistanceValue => _config != null ? _config.RespawnGroundProbeDistance : respawnGroundProbeDistance;

    private List<Transform> _checkpoints = new List<Transform>();
    private int _currentCheckpointIndex = -1;
    private Transform _playerTransform;
    private Rigidbody _playerRb;
    private bool _isRespawning;
    private Vector3 _defaultRespawnPosition = new Vector3(0f, 5f, 0f);

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        GameServices.Register((ICheckpointProgressService)this);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Start()
    {
        TryAcquirePlayer();
    }

    private void Update()
    {
        if (_isRespawning) return;
        // プレイヤーが非同期生成のため Start 時に未取得な場合がある。null の間は毎フレーム遅延取得を試みる。
        if (_playerTransform == null)
        {
            TryAcquirePlayer();
            if (_playerTransform == null) return;
        }

        // 落下死判定
        if (_playerTransform.position.y < FallDeathYValue)
        {
            StartCoroutine(RespawnCoroutine());
        }
    }

    private void TryAcquirePlayer()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        _playerTransform = player.transform;
        _playerRb = player.GetComponent<Rigidbody>();
        // 初回取得時の位置を「最終リスポーン位置（基準）」とみなす（通常はスポーン地点）。
        _defaultRespawnPosition = _playerTransform.position - Vector3.up * RespawnHeightOffsetValue;
    }

    public void RegisterCheckpoint(Transform cp)
    {
        _checkpoints.Add(cp);
    }

    public void OnCheckpointReached(int index)
    {
        if (index <= _currentCheckpointIndex) return;
        _currentCheckpointIndex = index;

        string message = $"Checkpoint {index + 1}!";
        CheckpointReached?.Invoke(index, message);
        CheckpointProgressChanged?.Invoke(_currentCheckpointIndex, _checkpoints.Count);
        ExpeditionEvents.RaiseCheckpointReached(_currentCheckpointIndex + 1, _checkpoints.Count);
        // L AudioManager 退役済み。チェックポイント SE は P 側 PeakPlunder.Audio.AudioManager の
        // SoundId 拡張＋シーン配線後に再有効化する。
    }

    public int GetCurrentCheckpointIndex() => _currentCheckpointIndex;
    public int GetTotalCheckpoints() => _checkpoints.Count;

    private System.Collections.IEnumerator RespawnCoroutine()
    {
        _isRespawning = true;
        RespawnStarted?.Invoke();

        yield return new WaitForSeconds(RespawnDelayValue * 0.5f);

        // リスポーン位置に移動
        Vector3 respawnPos = GetRespawnPosition();
        if (_playerRb != null)
        {
            _playerRb.linearVelocity = Vector3.zero;
            _playerRb.angularVelocity = Vector3.zero;
            _playerRb.position = respawnPos;
        }
        else if (_playerTransform != null)
            _playerTransform.position = respawnPos;

        yield return new WaitForSeconds(RespawnDelayValue * 0.5f);

        _isRespawning = false;
        RespawnCompleted?.Invoke();
    }

    private Vector3 GetRespawnPosition()
    {
        Vector3 basePos;
        if (_currentCheckpointIndex >= 0 && _currentCheckpointIndex < _checkpoints.Count)
            basePos = _checkpoints[_currentCheckpointIndex].position;
        else
            basePos = _defaultRespawnPosition;

        if (TryGetGroundY(basePos, out float groundY))
            basePos.y = Mathf.Max(basePos.y, groundY);

        return basePos + Vector3.up * RespawnHeightOffsetValue;
    }

    private bool TryGetGroundY(Vector3 position, out float groundY)
    {
        Vector3 rayStart = position + Vector3.up * RespawnGroundProbeHeightValue;
        if (Physics.Raycast(
            rayStart,
            Vector3.down,
            out RaycastHit hit,
            RespawnGroundProbeDistanceValue,
            ~0,
            QueryTriggerInteraction.Ignore))
        {
            groundY = hit.point.y;
            return true;
        }

        Terrain activeTerrain = Terrain.activeTerrain;
        if (activeTerrain != null)
        {
            groundY = activeTerrain.SampleHeight(position) + activeTerrain.transform.position.y;
            return true;
        }

        groundY = position.y;
        return false;
    }
}

/// <summary>
/// 各チェックポイント GameObject にアタッチするトリガー。
/// </summary>
public class CheckpointTrigger : MonoBehaviour
{
    public int index;
    public string label;

    private bool _triggered;

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;

        _triggered = true;
        GameServices.Checkpoints?.RecordCheckpoint(index);

        // 拠点⇄当該チェックポイントのジップラインを開通させ、登りをショートカットできるようにする。
        Sandbox.World.Zipline.ZiplineNetwork.Ensure().InstallLine(index, transform.position);
    }
}
}
