using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

// ─── CaveGenerator ────────────────────────────────────────────────────────────

/// <summary>
/// 洞窟生成の統合コンポーネント。
/// Marching Cubes（3D）で洞窟を生成する。
/// </summary>
public class CaveGenerator : MonoBehaviour
{
    // ─── 3D Settings (Marching Cubes) ────────────────────────────────

    [Header("3D Cave Size")]
    [Tooltip("Number of chunks in X direction. 1 chunk = 16m. Recommended: 6-10")]
    [SerializeField] int chunkCountX = 8;

    [Tooltip("Number of chunks in Y direction (height). Recommended: 3-5")]
    [SerializeField] int chunkCountY = 4;

    [Tooltip("Number of chunks in Z direction. Recommended: 6-10")]
    [SerializeField] int chunkCountZ = 8;

    [Tooltip("World size of one cell (meters). Recommended: 1.0")]
    [SerializeField] float cellSize3D = 1f;

    [Header("Noise Settings")]
    [SerializeField] NoiseSettings noiseSettings = NoiseSettings.Default;

    [Header("Special Areas")]
    [Tooltip("Forced cavity radius at start point (grid center) (meters)")]
    [SerializeField] float startRadius = 8f;

    [Tooltip("Forced cavity radius at goal point (grid edge) (meters)")]
    [SerializeField] float goalRadius = 6f;

    [Header("Material")]
    [Tooltip("Material to apply to cave mesh (null = default material)")]
    [SerializeField] Material caveMaterial;

    [Header("Debug")]
    [Tooltip("Show generation time in Console when generation completes")]
    [SerializeField] bool showGenerationTime = true;

    // ─── 公開プロパティ ───────────────────────────────────────────

    /// <summary>生成に使ったシード値</summary>
    public int UsedSeed { get; private set; }

    /// <summary>3D モードの 1 セルあたりのワールドサイズ（m）</summary>
    public float CellSize3D => cellSize3D;

    /// <summary>3D モードのチャンク数（XYZ）</summary>
    public Vector3Int ChunkCount3D => new Vector3Int(chunkCountX, chunkCountY, chunkCountZ);

    /// <summary>3D モードで生成済みのチャンクリスト（CaveContentPlacer がスカラー場を参照する）</summary>
    public IReadOnlyList<CaveChunk> Chunks => _chunks;

    /// <summary>3D ノイズ設定（isoLevel 参照用）</summary>
    public NoiseSettings NoiseConfig => noiseSettings;

    /// <summary>
    /// スタート空洞のワールド座標。
    /// Generate() 呼び出し後に確定する。
    /// </summary>
    public Vector3 StartWorldPosition { get; private set; }

    /// <summary>
    /// Inspector で設定されたシード値を返す（0 = ランダム）。
    /// </summary>
    public int InspectorSeed => noiseSettings.seed;

    /// <summary>
    /// 洞窟の半幅オフセット（チャンク全体サイズの 1/2）。
    /// CaveContentPlacer がワールド座標変換の基準として参照する。
    /// Generate() 実行後に確定する。
    /// </summary>
    public Vector3 CenterOffset { get; private set; }

    /// <summary>
    /// ゴール強制空洞の中心ワールド座標。
    /// EscapeGate の自動配置に使用する。
    /// Generate() 実行後に確定する。
    /// </summary>
    public Vector3 GoalCenterPosition { get; private set; }

    /// <summary>
    /// 洞窟生成が完了したときに発火するイベント。
    /// BatSpawner がスポーンタイミングに利用する。
    /// </summary>
    public event System.Action OnCaveGenerated;

    // ─── 内部状態 ─────────────────────────────────────────────

    private readonly List<CaveChunk> _chunks    = new List<CaveChunk>();
    private GameObject               _chunkRoot;

    // ─── 生成制御フラグ ───────────────────────────────────────────

    /// <summary>
    /// true のとき Start() での自動生成をスキップする。
    /// SetNetworkControlled(true) を呼び出すことで
    /// 外部から GenerateCave(seed) を呼ぶ方式に切り替わる。
    /// </summary>
    private bool _networkControlled = false;

    /// <summary>
    /// GenerateCave(int seed) 呼び出し時に一時的に設定されるシードオーバーライド。
    /// 0 のときは Inspector の設定値を使用する（従来動作）。
    /// </summary>
    private int _overrideSeed = 0;

    // ─── Unity ライフサイクル ─────────────────────────────────────

    void Start()
    {
        if (!_networkControlled)
            Generate();
    }

    // ─── 外部制御 API ─────────────────────────────────────────────

    /// <summary>
    /// 外部制御モードの ON/OFF を切り替える。
    /// true にすると Start() での自動生成を抑制し、GenerateCave(seed) で生成できるようにする。
    /// </summary>
    public void SetNetworkControlled(bool value) => _networkControlled = value;

    /// <summary>
    /// 指定シード値で洞窟を生成する。
    /// </summary>
    /// <param name="seed">使用するシード値（0 不可）</param>
    public void GenerateCave(int seed)
    {
        _overrideSeed = seed;
        Generate();
        _overrideSeed = 0;
    }

    // ─── 生成エントリポイント ─────────────────────────────────────

    [ContextMenu("Regenerate Cave")]
    public void Generate()
    {
        Generate3D();
    }

    // ═══════════════════════════════════════════════════════════════
    // 3D: Marching Cubes
    // ═══════════════════════════════════════════════════════════════

    void Generate3D()
    {
        var sw = showGenerationTime ? Stopwatch.StartNew() : null;

        // Determine seed（_overrideSeed が設定されている場合はそちらを優先）
        UsedSeed = (_overrideSeed    != 0) ? _overrideSeed
                 : (noiseSettings.seed == 0) ? Random.Range(1, 99999)
                 : noiseSettings.seed;

        // CenterOffset を確定（CaveContentPlacer のワールド座標変換に使用）
        CenterOffset = new Vector3(
            chunkCountX * CaveChunk.ChunkSize * cellSize3D * 0.5f,
            chunkCountY * CaveChunk.ChunkSize * cellSize3D * 0.5f,
            chunkCountZ * CaveChunk.ChunkSize * cellSize3D * 0.5f);

        // Clear existing chunks
        ClearChunks();

        // Create chunk root
        _chunkRoot = new GameObject("ChunkRoot");
        _chunkRoot.transform.SetParent(transform, false);

        float worldHeight = chunkCountY * CaveChunk.ChunkSize * cellSize3D;

        // Generate all chunks
        for (int cx = 0; cx < chunkCountX; cx++)
        for (int cy = 0; cy < chunkCountY; cy++)
        for (int cz = 0; cz < chunkCountZ; cz++)
        {
            var chunk = CreateChunk(new Vector3Int(cx, cy, cz), worldHeight);
            _chunks.Add(chunk);
        }

        // Apply forced cavities
        ApplyForcedCavity(GetStartWorldPos3D(), startRadius);
        ApplyForcedCavity(GetGoalWorldPos3D(),  goalRadius);

        // Rebuild mesh after applying forced cavities
        foreach (var chunk in _chunks)
            chunk.RebuildMesh(noiseSettings.isoLevel);

        // スタート空洞のワールド座標を確定
        StartWorldPosition = GetStartWorldPos3D();

        // ゴール空洞のワールド座標を確定（EscapeGate 自動配置が参照する）
        GoalCenterPosition = GetGoalWorldPos3D();

        if (sw != null)
        {
            sw.Stop();
            UnityEngine.Debug.Log($"[CaveGenerator 3D] Generation complete: {sw.ElapsedMilliseconds} ms / Chunk count: {_chunks.Count}");
        }

        // コンテンツ配置（CaveContentPlacer が同 GameObject にあれば実行）
        GetComponent<CaveContentPlacer>()?.PlaceContent(UsedSeed);

        UnityEngine.Debug.Log($"[CaveGenerator] Generate完了: mode=MarchingCubes3D, StartWorldPosition={StartWorldPosition}, GoalCenterPosition={GoalCenterPosition}");

        // 生成完了を通知（BatSpawner 等が購読してスポーン処理を行う）
        OnCaveGenerated?.Invoke();
    }

    CaveChunk CreateChunk(Vector3Int coord, float worldHeight)
    {
        var go = new GameObject($"Chunk_{coord.x}_{coord.y}_{coord.z}");
        go.transform.SetParent(_chunkRoot.transform, false);
        go.transform.localPosition = new Vector3(
            coord.x * CaveChunk.ChunkSize * cellSize3D,
            coord.y * CaveChunk.ChunkSize * cellSize3D,
            coord.z * CaveChunk.ChunkSize * cellSize3D);

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = caveMaterial != null
            ? caveMaterial
            : new Material(Shader.Find("Universal Render Pipeline/Lit"));

        var chunk = go.AddComponent<CaveChunk>();
        chunk.Initialize(coord, UsedSeed, noiseSettings, cellSize3D, worldHeight);
        return chunk;
    }

    void ApplyForcedCavity(Vector3 worldCenter, float radius)
    {
        float isoLevel = noiseSettings.isoLevel;

        foreach (var chunk in _chunks)
        {
            Vector3 chunkWorldPos = chunk.transform.position;

            for (int lx = 0; lx <= CaveChunk.ChunkSize; lx++)
            for (int ly = 0; ly <= CaveChunk.ChunkSize; ly++)
            for (int lz = 0; lz <= CaveChunk.ChunkSize; lz++)
            {
                Vector3 pointWorld = chunkWorldPos + new Vector3(lx, ly, lz) * cellSize3D;
                if (Vector3.Distance(pointWorld, worldCenter) <= radius)
                    chunk.SetScalar(lx, ly, lz, Mathf.Min(chunk.GetScalar(lx, ly, lz), isoLevel - 0.1f));
            }
        }
    }

    Vector3 GetStartWorldPos3D()
    {
        return transform.position + new Vector3(
            chunkCountX * CaveChunk.ChunkSize * cellSize3D * 0.5f,
            chunkCountY * CaveChunk.ChunkSize * cellSize3D * 0.5f,
            chunkCountZ * CaveChunk.ChunkSize * cellSize3D * 0.5f);
    }

    Vector3 GetGoalWorldPos3D()
    {
        return transform.position + new Vector3(
            chunkCountX * CaveChunk.ChunkSize * cellSize3D * 0.9f,
            chunkCountY * CaveChunk.ChunkSize * cellSize3D * 0.5f,
            chunkCountZ * CaveChunk.ChunkSize * cellSize3D * 0.9f);
    }

    void ClearChunks()
    {
        _chunks.Clear();
        if (_chunkRoot != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(_chunkRoot);
#else
            Destroy(_chunkRoot);
#endif
            _chunkRoot = null;
        }
    }
}
