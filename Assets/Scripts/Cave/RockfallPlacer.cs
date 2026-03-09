using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 洞窟天井に落石トラップを自動配置するコンポーネント。
///
/// [天井面検出]
///   CaveChunk.GetScalar() でスカラー場を直接参照（CLAUDE.md 設計制約準拠）。
///   「岩（>= isoLevel）の直下が空洞（&lt; isoLevel）」となるセルを天井面と判定する。
///   BatSpawner と同じ方式。
///
/// [スポーン位置選定]
///   全天井候補を収集 → System.Random(seed + 55555) で Fisher-Yates シャッフル
///   → _safeRadiusFromStart 外 &amp; _minDistanceBetweenTraps 間隔フィルタで 5〜10 箇所配置。
/// </summary>
public class RockfallPlacer : MonoBehaviour
{
    // ─────────────── Inspector ───────────────

    [Header("🪨 スポーン設定")]
    [Tooltip("最小トラップ数")]
    [Range(1, 10)]
    [SerializeField] private int _minTraps = 5;

    [Tooltip("最大トラップ数")]
    [Range(1, 20)]
    [SerializeField] private int _maxTraps = 10;

    [Tooltip("トラップ間の最小距離（m）")]
    [Range(5f, 30f)]
    [SerializeField] private float _minDistanceBetweenTraps = 12f;

    [Tooltip("スタート地点からの安全半径（m）")]
    [Range(5f, 40f)]
    [SerializeField] private float _safeRadiusFromStart = 20f;

    [Tooltip("RockfallTrap Prefab（null の場合は空 GameObject に RockfallTrap を自動追加）")]
    [SerializeField] private GameObject _trapPrefab;

    [Tooltip("トラップが使用する岩 Prefab（RockfallTrap._rockPrefab に転送）")]
    [SerializeField] private GameObject _rockPrefab;

    [Header("🏔️ 洞窟参照")]
    [SerializeField] private CaveGenerator _caveGenerator;

    [Header("🔧 デバッグ")]
    [SerializeField] private int _debugSpawnedCount;

    // ─────────────── 内部状態 ───────────────

    private readonly List<GameObject> _spawnedTraps = new();

    // ─────────────── Unity Lifecycle ───────────────

    private void Awake()
    {
        if (_caveGenerator == null)
            _caveGenerator = FindFirstObjectByType<CaveGenerator>();

        if (_caveGenerator != null)
            _caveGenerator.OnCaveGenerated += OnCaveGeneratedHandler;
        else
            Debug.LogError("[RockfallPlacer] CaveGenerator が見つかりません。Inspector で設定してください。");
    }

    private void OnDestroy()
    {
        if (_caveGenerator != null)
            _caveGenerator.OnCaveGenerated -= OnCaveGeneratedHandler;
    }

    // ─────────────── 洞窟生成完了ハンドラ ───────────────

    private void OnCaveGeneratedHandler()
    {
        // 再生成時に前のトラップを消す
        foreach (var trap in _spawnedTraps)
        {
            if (trap != null) Destroy(trap);
        }
        _spawnedTraps.Clear();

        PlaceTraps();
        _debugSpawnedCount = _spawnedTraps.Count;
    }

    // ─────────────── 天井面スポーン処理 ───────────────

    private void PlaceTraps()
    {
        if (_caveGenerator == null) return;

        var chunks = _caveGenerator.Chunks;
        if (chunks == null || chunks.Count == 0)
        {
            Debug.LogWarning("[RockfallPlacer] チャンクが空です。");
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
                // 上から下へスキャンして最初の「天井面」を検出する。
                // 天井面 = この Y が岩（>= isoLevel）かつ 1 つ下が空洞（< isoLevel）
                for (int ly = cs; ly >= 1; ly--)
                {
                    bool isRockHere = chunk.GetScalar(lx, ly,     lz) >= isoLevel;
                    bool isAirBelow = chunk.GetScalar(lx, ly - 1, lz) < isoLevel;

                    if (!isRockHere || !isAirBelow) continue;

                    // 天井直下の空洞セル中心をトラップ位置とする（岩の真下）
                    Vector3 ceilPos = chunkOrigin + new Vector3(
                        (lx + 0.5f) * cellSize,
                        (ly - 1)     * cellSize,
                        (lz + 0.5f) * cellSize);

                    if ((ceilPos - startPos).sqrMagnitude >= _safeRadiusFromStart * _safeRadiusFromStart)
                        candidates.Add(ceilPos);

                    break; // このカラムの最初の天井面のみ使用
                }
            }
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning("[RockfallPlacer] 天井スポーン候補が見つかりませんでした。");
            return;
        }

        // Fisher-Yates シャッフル（seed + 55555 で他スポーナーと乱数列を分離）
        var rng = new System.Random(_caveGenerator.UsedSeed + 55555);
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int     j   = rng.Next(i + 1);
            Vector3 tmp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = tmp;
        }

        // 配置数を seed で 5〜10 の範囲に決定
        int targetCount = _minTraps + rng.Next(_maxTraps - _minTraps + 1);

        float sqrMinDist = _minDistanceBetweenTraps * _minDistanceBetweenTraps;
        var   placed     = new List<Vector3>();

        foreach (var pos in candidates)
        {
            if (placed.Count >= targetCount) break;

            bool tooClose = false;
            foreach (var p in placed)
            {
                if ((pos - p).sqrMagnitude < sqrMinDist) { tooClose = true; break; }
            }
            if (tooClose) continue;

            SpawnOneTrap(pos);
            placed.Add(pos);
        }

        Debug.Log($"[RockfallPlacer] 落石トラップを {placed.Count} 箇所配置完了" +
                  $"（候補: {candidates.Count} / 目標: {targetCount}）");
    }

    private void SpawnOneTrap(Vector3 position)
    {
        GameObject trapObj;

        if (_trapPrefab != null)
        {
            trapObj = Instantiate(_trapPrefab, position, Quaternion.identity);
        }
        else
        {
            // Prefab 未設定時は空 GameObject に RockfallTrap を自動追加
            trapObj = new GameObject("RockfallTrap");
            trapObj.transform.position = position;
        }

        // RockfallTrap がなければ追加し、岩 Prefab を転送
        var trap = trapObj.GetComponent<RockfallTrap>();
        if (trap == null)
            trap = trapObj.AddComponent<RockfallTrap>();

        // Inspector で設定した岩 Prefab を RockfallTrap に反映
        // （Prefab 経由で既に設定済みの場合は上書きしない）
        if (_rockPrefab != null)
            trap.SetRockPrefab(_rockPrefab);

        _spawnedTraps.Add(trapObj);
    }
}
