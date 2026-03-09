using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 洞窟天井にコウモリを自動スポーンし、プレイヤー参照を動的に管理するコンポーネント。
///
/// [責務]
///   - CaveGenerator.OnCaveGenerated を受けて天井面スポーン位置を検出し、コウモリを生成する。
///   - Instantiate でローカル生成する。
///   - BatPerception.AddTarget / RemoveTarget でプレイヤー参照を動的に管理する。
///
/// [天井面検出]
///   CaveChunk.GetScalar(lx, ly, lz) でスカラー場を直接参照する（CLAUDE.md 設計制約準拠）。
///
/// [スポーン位置選定]
///   全天井候補を収集 → System.Random(seed + 12345) で Fisher-Yates シャッフル
///   → _safeRadiusFromStart 外 & _minDistanceBetweenBats 間隔フィルタで _maxBats 体まで配置。
/// </summary>
public class BatSpawner : MonoBehaviour
{
    // ─────────────── Inspector (スポーン設定) ───────────────

    [Header("🦇 スポーン設定")]
    [Tooltip("最大スポーン数")]
    [Range(1, 20)]
    [SerializeField] private int _maxBats = 8;

    [Tooltip("コウモリ間の最小距離（m）")]
    [Range(5f, 30f)]
    [SerializeField] private float _minDistanceBetweenBats = 8f;

    [Tooltip("スタート地点からの安全半径（m）")]
    [Range(10f, 40f)]
    [SerializeField] private float _safeRadiusFromStart = 20f;

    [Tooltip("コウモリ Prefab（BatAI + BatPerception が必須）")]
    [SerializeField] private GameObject _batPrefab;

    // ─────────────── Inspector (洞窟参照) ───────────────

    [Header("🏔️ 洞窟参照")]
    [SerializeField] private CaveGenerator _caveGenerator;

    // ─────────────── Inspector (デバッグ) ───────────────

    [Header("🔧 デバッグ")]
    [SerializeField] private int _debugSpawnedCount;
    [SerializeField] private int _debugPlayerCount;

    // ─────────────── 内部状態 ───────────────

    private readonly List<BatAI> _spawnedBats = new();

    // ─────────────── Unity Lifecycle ───────────────

    private void Awake()
    {
        if (_caveGenerator == null)
            _caveGenerator = FindFirstObjectByType<CaveGenerator>();

        if (_caveGenerator != null)
            _caveGenerator.OnCaveGenerated += OnCaveGeneratedHandler;
        else
            Debug.LogError("[BatSpawner] CaveGenerator が見つかりません。Inspector で設定してください。");
    }

    private void OnDestroy()
    {
        if (_caveGenerator != null)
            _caveGenerator.OnCaveGenerated -= OnCaveGeneratedHandler;

        _spawnedBats.Clear();
    }

    // ─────────────── 洞窟生成完了ハンドラ ───────────────

    private void OnCaveGeneratedHandler()
    {
        SpawnBats();
        _debugSpawnedCount = _spawnedBats.Count;

        StartCoroutine(FindAndRegisterPlayerStandalone());
    }

    // ─────────────── 天井スポーン処理 ───────────────

    private void SpawnBats()
    {
        if (_batPrefab == null)
        {
            Debug.LogError("[BatSpawner] _batPrefab が未設定です。");
            return;
        }
        if (_caveGenerator == null) return;

        var chunks = _caveGenerator.Chunks;
        if (chunks == null || chunks.Count == 0)
        {
            Debug.LogWarning("[BatSpawner] チャンクが空です。");
            return;
        }

        float   isoLevel = _caveGenerator.NoiseConfig.isoLevel;
        float   cellSize = _caveGenerator.CellSize3D;
        int     cs       = CaveChunk.ChunkSize;
        Vector3 startPos = _caveGenerator.StartWorldPosition;

        var candidates = new List<Vector3>();

        foreach (var chunk in chunks)
        {
            if (chunk == null) continue;
            Vector3 chunkOrigin = chunk.transform.position;

            for (int lx = 0; lx < cs; lx++)
            for (int lz = 0; lz < cs; lz++)
            {
                for (int ly = cs; ly >= 1; ly--)
                {
                    bool isRockHere = chunk.GetScalar(lx, ly,     lz) >= isoLevel;
                    bool isAirBelow = chunk.GetScalar(lx, ly - 1, lz) < isoLevel;

                    if (!isRockHere || !isAirBelow) continue;

                    Vector3 ceilPos = chunkOrigin + new Vector3(
                        (lx + 0.5f) * cellSize,
                        (ly - 1)     * cellSize,
                        (lz + 0.5f) * cellSize);

                    if ((ceilPos - startPos).sqrMagnitude >= _safeRadiusFromStart * _safeRadiusFromStart)
                        candidates.Add(ceilPos);

                    break;
                }
            }
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning("[BatSpawner] 天井スポーン候補が見つかりませんでした。");
            return;
        }

        var rng = new System.Random(_caveGenerator.UsedSeed + 12345);
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int     j   = rng.Next(i + 1);
            Vector3 tmp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = tmp;
        }

        float sqrMinDist = _minDistanceBetweenBats * _minDistanceBetweenBats;
        var   placed     = new List<Vector3>();

        foreach (var pos in candidates)
        {
            if (placed.Count >= _maxBats) break;

            bool tooClose = false;
            foreach (var p in placed)
            {
                if ((pos - p).sqrMagnitude < sqrMinDist) { tooClose = true; break; }
            }
            if (tooClose) continue;

            SpawnOneBat(pos);
            placed.Add(pos);
        }

        Debug.Log($"[BatSpawner] コウモリを {placed.Count} 体スポーン完了（候補: {candidates.Count} / 最大: {_maxBats}）");
    }

    private void SpawnOneBat(Vector3 position)
    {
        var bat = Instantiate(_batPrefab, position, Quaternion.identity);

        var ai = bat.GetComponent<BatAI>();
        if (ai != null)
            _spawnedBats.Add(ai);
        else
            Debug.LogWarning("[BatSpawner] BatPrefab に BatAI コンポーネントが見つかりません。");
    }

    // ─────────────── プレイヤー参照注入 ───────────────

    private IEnumerator FindAndRegisterPlayerStandalone()
    {
        const int   maxRetries = 10;
        const float interval   = 0.5f;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                RegisterPlayer(playerObj);
                _debugPlayerCount = 1;
                yield break;
            }
            yield return new WaitForSeconds(interval);
        }

        Debug.LogWarning("[BatSpawner] 「Player」タグのオブジェクトが見つかりませんでした。" +
                         "RegisterPlayerExternal() を呼んでください。");
    }

    private void RegisterPlayer(GameObject playerObj)
    {
        if (playerObj == null) return;

        var playerTransform = playerObj.transform;
        var torch           = playerObj.GetComponentInChildren<TorchSystem>();
        var stats           = playerObj.GetComponent<SurvivalStats>();
        var stateMgr        = playerObj.GetComponent<PlayerStateManager>();

        foreach (var bat in _spawnedBats)
        {
            if (bat == null) continue;
            bat.GetComponent<BatPerception>()
               ?.AddTarget(playerTransform, torch, stats, stateMgr);
        }

        Debug.Log($"[BatSpawner] プレイヤー登録完了: {playerObj.name}" +
                  $"（torch={torch != null}, stats={stats != null}, stateMgr={stateMgr != null}）");
    }

    private void UnregisterPlayer(GameObject playerObj)
    {
        if (playerObj == null) return;

        var playerTransform = playerObj.transform;

        foreach (var bat in _spawnedBats)
        {
            if (bat == null) continue;
            bat.GetComponent<BatPerception>()?.RemoveTarget(playerTransform);
        }

        _debugPlayerCount = Mathf.Max(0, _debugPlayerCount - 1);
        Debug.Log($"[BatSpawner] プレイヤー除去: {playerObj.name}");
    }

    // ─────────────── 外部注入 API ───────────────

    /// <summary>プレイヤースポーン後に直接登録する。</summary>
    public void RegisterPlayerExternal(GameObject playerObj)
    {
        if (playerObj == null) return;
        RegisterPlayer(playerObj);
        _debugSpawnedCount = _spawnedBats.Count;
    }

    /// <summary>プレイヤーを除去する外部 API。</summary>
    public void UnregisterPlayerExternal(GameObject playerObj)
    {
        UnregisterPlayer(playerObj);
    }

    /// <summary>DepthManager から深度遷移時に最大スポーン数を更新する。</summary>
    public void SetMaxBats(int count) => _maxBats = count;
}
