using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// チェックポイントの記録・リスポーン管理。
/// </summary>
public class CheckpointSystem : MonoBehaviour
{
    public static CheckpointSystem Instance { get; private set; }

    [Header("Respawn")]
    [SerializeField] private float respawnDelay = 1.5f;
    [SerializeField] private float fallDeathY = -20f;

    private List<Transform> _checkpoints = new List<Transform>();
    private int _currentCheckpointIndex = -1;
    private Transform _playerTransform;
    private Rigidbody _playerRb;
    private bool _isRespawning;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
            _playerRb = player.GetComponent<Rigidbody>();
        }
    }

    private void Update()
    {
        if (_playerTransform == null || _isRespawning) return;

        // 落下死判定
        if (_playerTransform.position.y < fallDeathY)
        {
            StartCoroutine(RespawnCoroutine());
        }
    }

    public void RegisterCheckpoint(Transform cp)
    {
        _checkpoints.Add(cp);
    }

    public void OnCheckpointReached(int index)
    {
        if (index <= _currentCheckpointIndex) return;
        _currentCheckpointIndex = index;

        HudManager.Instance?.ShowCheckpointMessage($"Checkpoint {index + 1}!");
        AudioManager.Instance?.PlaySE("checkpoint");
    }

    public int GetCurrentCheckpointIndex() => _currentCheckpointIndex;
    public int GetTotalCheckpoints() => _checkpoints.Count;

    private System.Collections.IEnumerator RespawnCoroutine()
    {
        _isRespawning = true;

        // フェードアウト
        HudManager.Instance?.FadeOut();
        yield return new WaitForSeconds(respawnDelay * 0.5f);

        // リスポーン位置に移動
        Vector3 respawnPos = GetRespawnPosition();
        if (_playerRb != null)
        {
            _playerRb.linearVelocity = Vector3.zero;
            _playerRb.angularVelocity = Vector3.zero;
        }
        if (_playerTransform != null)
            _playerTransform.position = respawnPos;

        yield return new WaitForSeconds(respawnDelay * 0.5f);

        // フェードイン
        HudManager.Instance?.FadeIn();
        _isRespawning = false;
    }

    private Vector3 GetRespawnPosition()
    {
        if (_currentCheckpointIndex >= 0 && _currentCheckpointIndex < _checkpoints.Count)
            return _checkpoints[_currentCheckpointIndex].position + Vector3.up * 2f;

        // チェックポイントなし: 地形スタート地点
        return new Vector3(0f, 5f, 0f);
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
        CheckpointSystem.Instance?.OnCheckpointReached(index);
    }
}
