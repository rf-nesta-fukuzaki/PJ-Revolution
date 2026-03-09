using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 洞窟床面にトカゲを自動スポーンし、プレイヤー参照を動的に管理するコンポーネント。
///
/// [責務]
///   - CaveGenerator.OnCaveGenerated を受けて床面スポーン位置を検出し、トカゲを生成する。
///   - Instantiate でローカル生成する。
///   - BatPerception.AddTarget / RemoveTarget でプレイヤー参照を動的に管理する。
///
/// [床面検出]
///   CaveChunk.GetScalar(lx, ly, lz) でスカラー場を直接参照する（CLAUDE.md 設計制約準拠）。
///   上からの Raycast は天井岩で止まり床面に届かないため、スカラー場直接参照方式を採用。
///   「空洞（&lt; isoLevel）の直下が岩（&gt;= isoLevel）」となるセルを床面と判定する。
///
/// [スポーン位置選定]
///   全床面候補を収集 → System.Random(seed + 77777) で Fisher-Yates シャッフル
///   → _safeRadiusFromStart 外 &amp; _minDistanceBetweenLizards 間隔フィルタで _maxLizards 体まで配置。
/// </summary>
public class LizardSpawner : MonoBehaviour
{
    // ─────────────── Inspector (スポーン設定) ───────────────

    [Header("🦎 スポーン設定")]
    [Tooltip("最大スポーン数")]
    [Range(1, 20)]
    [SerializeField] private int _maxLizards = 3;

    [Tooltip("トカゲ間の最小距離（m）")]
    [Range(5f, 30f)]
    [SerializeField] private float _minDistanceBetweenLizards = 15f;

    [Tooltip("スタート地点からの安全半径（m）")]
    [Range(10f, 40f)]
    [SerializeField] private float _safeRadiusFromStart = 25f;

    [Tooltip("トカゲ Prefab（LizardAI + BatPerception が必須）")]
    [SerializeField] private GameObject _lizardPrefab;

    // ─────────────── Inspector (洞窟参照) ───────────────

    [Header("🏔️ 洞窟参照")]
    [SerializeField] private CaveGenerator _caveGenerator;

    // ─────────────── Inspector (デバッグ) ───────────────

    [Header("🔧 デバッグ")]
    [SerializeField] private int _debugSpawnedCount;
    [SerializeField] private int _debugPlayerCount;

    // ─────────────── 内部状態 ───────────────

    private readonly List<LizardAI> _spawnedLizards = new();

    // ─────────────── Unity Lifecycle ───────────────

    private void Awake()
    {
        if (_caveGenerator == null)
            _caveGenerator = FindFirstObjectByType<CaveGenerator>();

        if (_caveGenerator != null)
            _caveGenerator.OnCaveGenerated += OnCaveGeneratedHandler;
        else
            Debug.LogError("[LizardSpawner] CaveGenerator が見つかりません。Inspector で設定してください。");
    }

    private void OnDestroy()
    {
        if (_caveGenerator != null)
            _caveGenerator.OnCaveGenerated -= OnCaveGeneratedHandler;

        _spawnedLizards.Clear();
    }

    // ─────────────── 洞窟生成完了ハンドラ ───────────────

    private void OnCaveGeneratedHandler()
    {
        SpawnLizards();
        _debugSpawnedCount = _spawnedLizards.Count;

        StartCoroutine(FindAndRegisterPlayerStandalone());
    }

    // ─────────────── 床面スポーン処理 ───────────────

    private void SpawnLizards()
    {
        if (_lizardPrefab == null)
        {
            Debug.LogError("[LizardSpawner] _lizardPrefab が未設定です。");
            return;
        }
        if (_caveGenerator == null) return;

        var chunks = _caveGenerator.Chunks;
        if (chunks == null || chunks.Count == 0)
        {
            Debug.LogWarning("[LizardSpawner] チャンクが空です。");
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
                // 下から上へスキャンし、「空洞の直下が岩」となる最初の床面を検出する
                for (int ly = 1; ly < cs; ly++)
                {
                    bool isAirHere  = chunk.GetScalar(lx, ly,     lz) < isoLevel;
                    bool isRockBelow = chunk.GetScalar(lx, ly - 1, lz) >= isoLevel;

                    if (!isAirHere || !isRockBelow) continue;

                    Vector3 floorPos = chunkOrigin + new Vector3(
                        (lx + 0.5f) * cellSize,
                        ly           * cellSize,
                        (lz + 0.5f) * cellSize);

                    if ((floorPos - startPos).sqrMagnitude >= _safeRadiusFromStart * _safeRadiusFromStart)
                        candidates.Add(floorPos);

                    break; // このカラムの最初の床面のみ使用
                }
            }
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning("[LizardSpawner] 床面スポーン候補が見つかりませんでした。");
            return;
        }

        // Fisher-Yates シャッフル（BatSpawner とシードオフセットを変えて重複配置を防ぐ）
        var rng = new System.Random(_caveGenerator.UsedSeed + 77777);
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int     j   = rng.Next(i + 1);
            Vector3 tmp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = tmp;
        }

        float sqrMinDist = _minDistanceBetweenLizards * _minDistanceBetweenLizards;
        var   placed     = new List<Vector3>();

        foreach (var pos in candidates)
        {
            if (placed.Count >= _maxLizards) break;

            bool tooClose = false;
            foreach (var p in placed)
            {
                if ((pos - p).sqrMagnitude < sqrMinDist) { tooClose = true; break; }
            }
            if (tooClose) continue;

            SpawnOneLizard(pos);
            placed.Add(pos);
        }

        Debug.Log($"[LizardSpawner] トカゲを {placed.Count} 体スポーン完了（候補: {candidates.Count} / 最大: {_maxLizards}）");
    }

    private void SpawnOneLizard(Vector3 position)
    {
        var obj = Instantiate(_lizardPrefab, position, Quaternion.identity);

        var ai = obj.GetComponent<LizardAI>();
        if (ai != null)
            _spawnedLizards.Add(ai);
        else
            Debug.LogWarning("[LizardSpawner] LizardPrefab に LizardAI コンポーネントが見つかりません。");
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

        Debug.LogWarning("[LizardSpawner] 「Player」タグのオブジェクトが見つかりませんでした。" +
                         "RegisterPlayerExternal() を呼んでください。");
    }

    private void RegisterPlayer(GameObject playerObj)
    {
        if (playerObj == null) return;

        var playerTransform = playerObj.transform;
        var torch           = playerObj.GetComponentInChildren<TorchSystem>();
        var stats           = playerObj.GetComponent<SurvivalStats>();
        var stateMgr        = playerObj.GetComponent<PlayerStateManager>();

        foreach (var lizard in _spawnedLizards)
        {
            if (lizard == null) continue;
            lizard.GetComponent<BatPerception>()
                  ?.AddTarget(playerTransform, torch, stats, stateMgr);
        }

        Debug.Log($"[LizardSpawner] プレイヤー登録完了: {playerObj.name}" +
                  $"（torch={torch != null}, stats={stats != null}, stateMgr={stateMgr != null}）");
    }

    private void UnregisterPlayer(GameObject playerObj)
    {
        if (playerObj == null) return;

        var playerTransform = playerObj.transform;

        foreach (var lizard in _spawnedLizards)
        {
            if (lizard == null) continue;
            lizard.GetComponent<BatPerception>()?.RemoveTarget(playerTransform);
        }

        _debugPlayerCount = Mathf.Max(0, _debugPlayerCount - 1);
        Debug.Log($"[LizardSpawner] プレイヤー除去: {playerObj.name}");
    }

    // ─────────────── 外部注入 API ───────────────

    /// <summary>プレイヤースポーン後に直接登録する。</summary>
    public void RegisterPlayerExternal(GameObject playerObj)
    {
        if (playerObj == null) return;
        RegisterPlayer(playerObj);
        _debugSpawnedCount = _spawnedLizards.Count;
    }

    /// <summary>プレイヤーを除去する外部 API。</summary>
    public void UnregisterPlayerExternal(GameObject playerObj)
    {
        UnregisterPlayer(playerObj);
    }

    /// <summary>DepthManager から深度遷移時に最大スポーン数を更新する。</summary>
    public void SetMaxLizards(int count) => _maxLizards = count;
}
