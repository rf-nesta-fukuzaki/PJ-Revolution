using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Co-op 基盤（ローカルマルチ準備）。
/// 複数プレイヤーのインスタンス化・管理を担当する。
/// ネットワーク（NGO）統合は後の Step で行う。
/// </summary>
public class CoopManager : MonoBehaviour
{
    public static CoopManager Instance { get; private set; }

    [Header("Co-op Settings")]
    [SerializeField] private int playerCount = 1;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;

    private List<GameObject> _players = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // シーンに既存プレイヤーがいればそのまま使う
        var existing = GameObject.FindGameObjectsWithTag("Player");
        if (existing.Length > 0)
        {
            _players.AddRange(existing);
            return;
        }

        // Prefab が設定されていれば spawn
        if (playerPrefab != null)
            SpawnPlayers();
    }

    private void SpawnPlayers()
    {
        for (int i = 0; i < playerCount; i++)
        {
            Vector3 pos = spawnPoints != null && i < spawnPoints.Length
                ? spawnPoints[i].position
                : new Vector3(i * 2f, 5f, 0f);

            var p = Instantiate(playerPrefab, pos, Quaternion.identity);
            p.name = $"Player_{i + 1}";
            _players.Add(p);
        }
    }

    public IReadOnlyList<GameObject> GetPlayers() => _players;
}
