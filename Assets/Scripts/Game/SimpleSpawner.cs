using UnityEngine;

/// <summary>
/// シングルプレイ用スポーナー。
/// CaveGenerator の洞窟生成完了後、プレイヤーを開始地点に移動させる。
/// </summary>
public class SimpleSpawner : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("シーン上の CaveGenerator")]
    [SerializeField] private CaveGenerator _caveGenerator;

    [Tooltip("移動させるプレイヤーの Transform")]
    [SerializeField] private Transform _playerTransform;

    [Header("設定")]
    [Tooltip("スポーン時にプレイヤーのRigidbodyのvelocityをリセットするか")]
    [SerializeField] private bool _resetVelocity = true;

    private void OnEnable()
    {
        if (_caveGenerator != null)
            _caveGenerator.OnCaveGenerated += HandleCaveGenerated;
    }

    private void OnDisable()
    {
        if (_caveGenerator != null)
            _caveGenerator.OnCaveGenerated -= HandleCaveGenerated;
    }

    private void HandleCaveGenerated()
    {
        if (_playerTransform == null)
        {
            Debug.LogError("[SimpleSpawner] playerTransform が未設定です");
            return;
        }

        Vector3 spawnPos = _caveGenerator.StartWorldPosition;
        Debug.Log($"[SimpleSpawner] プレイヤーを {spawnPos} にスポーンします");

        _playerTransform.position = spawnPos;

        if (_resetVelocity)
        {
            var rb = _playerTransform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity  = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}
