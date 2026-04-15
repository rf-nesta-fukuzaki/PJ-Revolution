using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GDD §3.2 — チーム全体のロープ接続を管理するシングルトン。
/// プレイヤー A-B-C-D を鎖状に繋いだり、任意ペアを繋いだりする。
/// 1人の滑落で全員に引っ張り力が伝播する。
/// </summary>
public class RopeManager : MonoBehaviour
{
    private static Material s_defaultRopeMaterial;

    public static RopeManager Instance { get; private set; }

    [Header("ロープ Prefab")]
    [SerializeField] private PlayerRopeSystem _ropePrefab;

    // プレイヤーID → Rigidbody
    private readonly Dictionary<int, Rigidbody> _players = new();

    // 接続ペア (idA, idB) → RopeSystem
    private readonly Dictionary<(int, int), PlayerRopeSystem> _ropes = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ── プレイヤー登録 ───────────────────────────────────────
    public void RegisterPlayer(int id, Rigidbody rb)
    {
        _players[id] = rb;
    }

    public void UnregisterPlayer(int id)
    {
        // このプレイヤーに関連する全ロープを切断
        var keysToRemove = new List<(int, int)>();
        foreach (var kv in _ropes)
        {
            if (kv.Key.Item1 == id || kv.Key.Item2 == id)
                keysToRemove.Add(kv.Key);
        }
        foreach (var key in keysToRemove)
            DisconnectRope(key.Item1, key.Item2);

        _players.Remove(id);
    }

    // ── ロープ接続 ───────────────────────────────────────────
    /// <summary>2人のプレイヤーをロープで繋ぐ。</summary>
    public bool ConnectRope(int idA, int idB, float length = 10f)
    {
        if (!_players.TryGetValue(idA, out var rbA)) return false;
        if (!_players.TryGetValue(idB, out var rbB)) return false;

        var key = NormalizeKey(idA, idB);
        if (_ropes.ContainsKey(key))
        {
            Debug.LogWarning("[RopeManager] 既に繋がっています");
            return false;
        }

        var rope = _ropePrefab != null
            ? Instantiate(_ropePrefab, transform)
            : CreateDefaultRope();

        rope.Connect(rbA, rbB, length);
        _ropes[key] = rope;
        return true;
    }

    /// <summary>指定ペアのロープを切断する。</summary>
    public void DisconnectRope(int idA, int idB)
    {
        var key = NormalizeKey(idA, idB);
        if (!_ropes.TryGetValue(key, out var rope)) return;

        rope.Disconnect();
        Destroy(rope.gameObject);
        _ropes.Remove(key);
    }

    /// <summary>全ロープを切断する。</summary>
    public void DisconnectAll()
    {
        foreach (var rope in _ropes.Values)
        {
            rope.Disconnect();
            Destroy(rope.gameObject);
        }
        _ropes.Clear();
    }

    public bool IsConnected(int idA, int idB) => _ropes.ContainsKey(NormalizeKey(idA, idB));

    public bool HasAnyRope => _ropes.Count > 0;

    // ── アンカーポイント管理 ──────────────────────────────────
    private readonly List<Transform> _anchorPoints = new();

    /// <summary>ロープを固定できるアンカーポイントを登録（AnchorBolt 等が使用）。</summary>
    public void RegisterAnchorPoint(Transform anchor)
    {
        if (anchor != null && !_anchorPoints.Contains(anchor))
            _anchorPoints.Add(anchor);
    }

    public void UnregisterAnchorPoint(Transform anchor) => _anchorPoints.Remove(anchor);

    public IReadOnlyList<Transform> AnchorPoints => _anchorPoints;

    // ── ユーティリティ ───────────────────────────────────────
    private static (int, int) NormalizeKey(int a, int b) => a < b ? (a, b) : (b, a);

    private PlayerRopeSystem CreateDefaultRope()
    {
        var go   = new GameObject("PlayerRope");
        go.transform.SetParent(transform);
        var lr   = go.AddComponent<LineRenderer>();
        lr.widthMultiplier = 0.04f;
        lr.sharedMaterial  = GetDefaultRopeMaterial();
        var rope = go.AddComponent<PlayerRopeSystem>();
        return rope;
    }

    private static Material GetDefaultRopeMaterial()
    {
        if (s_defaultRopeMaterial != null) return s_defaultRopeMaterial;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) return null;

        s_defaultRopeMaterial = new Material(shader)
        {
            name = "DefaultRopeSharedMaterial"
        };
        return s_defaultRopeMaterial;
    }
}
