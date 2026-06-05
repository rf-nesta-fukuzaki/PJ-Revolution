using System;
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

    private static RopeManager _instance;

    [System.Obsolete("GameServices.Ropes を使用してください")]
    public static RopeManager Instance => _instance;

    [Header("ロープ Prefab")]
    [SerializeField] private PlayerRopeSystem _ropePrefab;

    // プレイヤーID → Rigidbody
    private readonly Dictionary<int, Rigidbody> _players = new();

    // 接続ペア (idA, idB) → RopeSystem
    private readonly Dictionary<(int, int), PlayerRopeSystem> _ropes = new();

    // プレイヤー → 遺物ロープ（ロングロープ括り付け）
    private readonly Dictionary<int, PlayerRopeSystem> _playerRelicRopes = new();
    private readonly Dictionary<int, Rigidbody>        _playerRelicBodies = new();

    // プレイヤー → アンカーボルト固定点
    private readonly Dictionary<int, PlayerRopeSystem> _playerAnchorRopes = new();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            DestroyObject(gameObject);
            return;
        }
        _instance = this;
        GameServices.Register(this);
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
        GameServices.ClearRopeIf(this);
    }

    private static void DestroyObject(UnityEngine.Object target)
    {
        if (target == null) return;
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
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

        DisconnectPlayerRelic(id);
        DisconnectPlayerAnchor(id);

        _players.Remove(id);
    }

    // ── ロープ接続 ───────────────────────────────────────────
    /// <summary>2人のプレイヤーをロープで繋ぐ。</summary>
    public bool ConnectRope(int idA, int idB, float length = 10f, float breakForce = -1f, ItemBase durabilitySource = null)
    {
        if (!TryGetPlayerRb(idA, out var rbA)) return false;
        if (!TryGetPlayerRb(idB, out var rbB)) return false;

        var key = NormalizeKey(idA, idB);
        if (_ropes.ContainsKey(key))
        {
            Debug.LogWarning("[RopeManager] 既に繋がっています");
            return false;
        }

        var rope = SpawnRopeSystem();
        rope.Connect(rbA, rbB, length, breakForce);
        rope.SetDurabilitySource(durabilitySource);
        SubscribeRopeLifecycle(key, rope, durabilitySource);
        _ropes[key] = rope;
        return true;
    }

    /// <summary>指定ペアのロープを切断する。</summary>
    public void DisconnectRope(int idA, int idB)
    {
        var key = NormalizeKey(idA, idB);
        if (!_ropes.TryGetValue(key, out var rope)) return;

        rope.Disconnect();
        DestroyObject(rope.gameObject);
        _ropes.Remove(key);
    }

    /// <summary>全ロープを切断する。</summary>
    public void DisconnectAll()
    {
        foreach (var rope in _ropes.Values)
        {
            rope.Disconnect();
            DestroyObject(rope.gameObject);
        }
        _ropes.Clear();
    }

    public bool IsConnected(int idA, int idB) => _ropes.ContainsKey(NormalizeKey(idA, idB));

    public bool HasAnyRope => _ropes.Count > 0 || _playerRelicRopes.Count > 0;

    /// <summary>GDD §4.6 — ロングロープでプレイヤーと遺物を接続する。</summary>
    public bool ConnectPlayerToRelic(
        int playerId,
        Rigidbody relicRb,
        float length = 25f,
        float breakForce = -1f,
        ItemBase durabilitySource = null)
    {
        if (relicRb == null) return false;
        if (!TryGetPlayerRb(playerId, out var playerRb)) return false;
        if (_playerRelicRopes.ContainsKey(playerId)) return false;

        var rope = SpawnRopeSystem();
        rope.Connect(playerRb, relicRb, length, breakForce);
        rope.SetDurabilitySource(durabilitySource);
        SubscribeSingleRopeLifecycle(() => _playerRelicRopes.Remove(playerId), rope, durabilitySource);
        _playerRelicRopes[playerId] = rope;
        _playerRelicBodies[playerId] = relicRb;
        return true;
    }

    /// <summary>GDD §5.1 — プレイヤーとアンカーボルト固定点を接続。</summary>
    public bool ConnectPlayerToAnchor(
        int playerId,
        Transform anchor,
        float length,
        float breakForce,
        ItemBase durabilitySource)
    {
        if (anchor == null) return false;
        if (!TryGetPlayerRb(playerId, out var playerRb)) return false;
        if (_playerAnchorRopes.ContainsKey(playerId)) return false;

        var anchorRb = EnsureAnchorRigidbody(anchor);
        if (anchorRb == null) return false;

        var rope = SpawnRopeSystem();
        rope.Connect(playerRb, anchorRb, length, breakForce);
        rope.SetDurabilitySource(durabilitySource);
        SubscribeSingleRopeLifecycle(() => _playerAnchorRopes.Remove(playerId), rope, durabilitySource);
        _playerAnchorRopes[playerId] = rope;
        return true;
    }

    public void DisconnectPlayerAnchor(int playerId)
    {
        if (!_playerAnchorRopes.TryGetValue(playerId, out var rope)) return;

        rope.Disconnect();
        DestroyObject(rope.gameObject);
        _playerAnchorRopes.Remove(playerId);
    }

    public bool IsPlayerConnectedToAnchor(int playerId) => _playerAnchorRopes.ContainsKey(playerId);

    /// <summary>プレイヤーに紐づくショップロープ接続をすべて切断（GDD §5.1 Xキー）。</summary>
    public void DisconnectAllForPlayer(int playerId)
    {
        var keysToRemove = new List<(int, int)>();
        foreach (var kv in _ropes)
        {
            if (kv.Key.Item1 == playerId || kv.Key.Item2 == playerId)
                keysToRemove.Add(kv.Key);
        }

        foreach (var key in keysToRemove)
            DisconnectRope(key.Item1, key.Item2);

        DisconnectPlayerRelic(playerId);
        DisconnectPlayerAnchor(playerId);
    }

    public bool IsPlayerInAnyShopRope(int playerId) =>
        HasRopePair(playerId) || _playerRelicRopes.ContainsKey(playerId) || _playerAnchorRopes.ContainsKey(playerId);

    private bool HasRopePair(int playerId)
    {
        foreach (var kv in _ropes)
        {
            if (kv.Key.Item1 == playerId || kv.Key.Item2 == playerId)
                return true;
        }
        return false;
    }

    private static Rigidbody EnsureAnchorRigidbody(Transform anchor)
    {
        var rb = anchor.GetComponent<Rigidbody>();
        if (rb != null) return rb;

        rb = anchor.gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;
        return rb;
    }

    private PlayerRopeSystem SpawnRopeSystem()
    {
        if (_ropePrefab != null)
            return Instantiate(_ropePrefab, transform);

        return CreateDefaultRope();
    }

    public void DisconnectPlayerRelic(int playerId)
    {
        if (!_playerRelicRopes.TryGetValue(playerId, out var rope)) return;

        rope.Disconnect();
        DestroyObject(rope.gameObject);
        _playerRelicRopes.Remove(playerId);
        _playerRelicBodies.Remove(playerId);
    }

    public bool IsPlayerConnectedToRelic(int playerId) => _playerRelicRopes.ContainsKey(playerId);

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

    private void SubscribeRopeLifecycle((int, int) key, PlayerRopeSystem rope, ItemBase durabilitySource)
    {
        rope.OnDisconnected += broken =>
        {
            if (_ropes.TryGetValue(key, out var existing) && existing == rope)
                _ropes.Remove(key);
            if (broken && durabilitySource is IShopRopeItem shop)
                shop.CutRopeLocalOnly();
        };
    }

    private void SubscribeSingleRopeLifecycle(Action removeEntry, PlayerRopeSystem rope, ItemBase durabilitySource)
    {
        rope.OnDisconnected += broken =>
        {
            removeEntry?.Invoke();
            if (broken && durabilitySource is IShopRopeItem shop)
                shop.CutRopeLocalOnly();
        };
    }

    private bool TryGetPlayerRb(int playerId, out Rigidbody rb)
    {
        if (_players.TryGetValue(playerId, out rb) && rb != null)
            return true;

        foreach (var inv in PlayerInventory.RegisteredInventories)
        {
            if (inv == null) continue;
            if (PlayerScoreId.FromMember(inv) != playerId) continue;

            rb = inv.GetComponent<Rigidbody>();
            if (rb == null) continue;

            RegisterPlayer(playerId, rb);
            return true;
        }

        rb = null;
        return false;
    }

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
