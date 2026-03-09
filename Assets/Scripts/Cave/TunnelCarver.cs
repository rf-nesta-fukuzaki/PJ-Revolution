using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 大部屋をランダム配置し、最小全域木（Prim 法）で接続した通路を彫るコンポーネント。
/// CaveGenerator と同じ GameObject にアタッチして使用する。
///
/// [生成フロー]
///   1. System.Random(seed + 99999) でシードを独立させ、CaveContentPlacer/BatSpawner と衝突回避。
///   2. スタート・ゴールを含む _roomCount 個の大部屋候補をランダム選定し球形空洞を彫る。
///   3. Prim 法で最小全域木（MST）を構築し、各辺を 0.5m ステップでトンネル補間する。
///   4. 追加で _extraTunnels 本のランダム辺を彫り、デッドエンドを減らす。
///   5. 大部屋の一部から垂直シャフト（縦穴）を彫り、上下探索ルートを追加する。
///
/// [設計制約]
///   - RebuildMesh は CaveGenerator が最後に一括で行うため本コンポーネントでは呼ばない。
///   - ランダムは必ず System.Random を使用（UnityEngine.Random 禁止）。
/// </summary>
public class TunnelCarver : MonoBehaviour
{
    [Header("大部屋設定")]
    [Tooltip("生成する大部屋数（スタート・ゴールを含む）")]
    [SerializeField] private int   _roomCount     = 6;

    [Tooltip("大部屋の最小半径 (m)")]
    [SerializeField] private float _roomRadiusMin = 4f;

    [Tooltip("大部屋の最大半径 (m)")]
    [SerializeField] private float _roomRadiusMax = 6f;

    [Header("トンネル設定")]
    [Tooltip("トンネルの彫刻半径 (m)")]
    [SerializeField] private float _tunnelRadius  = 2f;

    [Tooltip("MST に追加するループ辺の本数（デッドエンド回避）")]
    [SerializeField] private int   _extraTunnels  = 1;

    [Tooltip("false にするとトンネル彫刻をスキップする（デバッグ用）")]
    [SerializeField] private bool  _enableTunnels = true;

    [Header("垂直シャフト設定")]
    [Tooltip("生成する垂直シャフト数（大部屋リストから選択）")]
    [SerializeField] private int   _shaftCount  = 2;

    [Tooltip("垂直シャフトの彫刻半径 (m)")]
    [SerializeField] private float _shaftRadius = 1.5f;

    // ─── 公開 API ─────────────────────────────────────────────────

    /// <summary>
    /// トンネル・大部屋を彫る。
    /// CaveGenerator.Generate3D() が ApplyForcedCavity の直後に呼び出す。
    /// RebuildMesh は呼ばないこと。
    /// </summary>
    public void CarveTunnels(int seed)
    {
        if (!_enableTunnels) return;

        var caveGen = GetComponent<CaveGenerator>();
        if (caveGen == null)
        {
            Debug.LogError("[TunnelCarver] CaveGenerator が見つかりません");
            return;
        }

        var rng = new System.Random(seed + 99999);

        float isoLevel = caveGen.NoiseConfig.isoLevel;

        // ── Step 1: 大部屋候補の収集 ─────────────────────────────
        var rooms = CollectRoomPositions(caveGen, rng);

        // ── Step 2: 大部屋の彫刻 ────────────────────────────────
        foreach (var room in rooms)
        {
            float r = _roomRadiusMin + (float)rng.NextDouble() * (_roomRadiusMax - _roomRadiusMin);
            caveGen.CarveSphericalCavity(room, r);
        }

        // ── Step 3: MST 構築（Prim 法）────────────────────────────
        var mstEdges = BuildMST(rooms);

        // ── Step 4: MST 各辺のトンネル彫刻 ─────────────────────────
        foreach (var (a, b) in mstEdges)
            CarveTunnel(caveGen, rooms[a], rooms[b], isoLevel, rng);

        // ── Step 5: 追加ループ辺（デッドエンド回避） ────────────────
        for (int i = 0; i < _extraTunnels && rooms.Count >= 2; i++)
        {
            int a = rng.Next(rooms.Count);
            int b = rng.Next(rooms.Count - 1);
            if (b >= a) b++; // a と重複しないよう調整
            CarveTunnel(caveGen, rooms[a], rooms[b], isoLevel, rng);
        }

        // ── Step 6: 垂直シャフト（縦穴）────────────────────────────
        CarveVerticalShafts(caveGen, rooms, rng);

        Debug.Log($"[TunnelCarver] 完了: 大部屋={rooms.Count}, MSTエッジ={mstEdges.Count}, 追加辺={_extraTunnels}, シャフト={_shaftCount}");
    }

    // ─── 大部屋位置収集 ───────────────────────────────────────────

    private List<Vector3> CollectRoomPositions(CaveGenerator caveGen, System.Random rng)
    {
        var rooms = new List<Vector3>();

        // スタート・ゴールは必ず含める
        rooms.Add(caveGen.StartWorldPosition);
        rooms.Add(caveGen.GoalCenterPosition);

        // 残りをチャンク空間内でランダム選定
        Vector3Int cc  = caveGen.ChunkCount3D;
        float      cs  = caveGen.CellSize3D;
        float totalX   = cc.x * CaveChunk.ChunkSize * cs;
        float totalY   = cc.y * CaveChunk.ChunkSize * cs;
        float totalZ   = cc.z * CaveChunk.ChunkSize * cs;
        Vector3 origin = caveGen.transform.position;
        float margin   = _roomRadiusMax;

        int additionalRooms = Mathf.Max(0, _roomCount - 2);
        for (int i = 0; i < additionalRooms; i++)
        {
            float x = origin.x + margin + (float)rng.NextDouble() * Mathf.Max(0f, totalX - 2f * margin);
            float y = origin.y + margin + (float)rng.NextDouble() * Mathf.Max(0f, totalY - 2f * margin);
            float z = origin.z + margin + (float)rng.NextDouble() * Mathf.Max(0f, totalZ - 2f * margin);
            rooms.Add(new Vector3(x, y, z));
        }

        return rooms;
    }

    // ─── Prim 法による MST 構築 ───────────────────────────────────

    private List<(int a, int b)> BuildMST(List<Vector3> rooms)
    {
        int n = rooms.Count;
        var edges   = new List<(int, int)>();
        if (n <= 1) return edges;

        var  inMST   = new bool[n];
        var  minCost = new float[n];
        var  parent  = new int[n];

        for (int i = 0; i < n; i++) { minCost[i] = float.MaxValue; parent[i] = -1; }
        minCost[0] = 0f;

        for (int iter = 0; iter < n; iter++)
        {
            // 最小コストの未追加ノードを選択
            int u = -1;
            for (int i = 0; i < n; i++)
                if (!inMST[i] && (u == -1 || minCost[i] < minCost[u]))
                    u = i;

            if (u == -1) break;
            inMST[u] = true;

            if (parent[u] >= 0)
                edges.Add((parent[u], u));

            // 隣接コストの更新
            for (int v = 0; v < n; v++)
            {
                if (inMST[v]) continue;
                float dist = Vector3.Distance(rooms[u], rooms[v]);
                if (dist < minCost[v]) { minCost[v] = dist; parent[v] = u; }
            }
        }

        return edges;
    }

    // ─── トンネル彫刻 ────────────────────────────────────────────

    /// <summary>2 点間を 0.5m ステップで球形空洞を補間してトンネルを彫る。</summary>
    private void CarveTunnel(CaveGenerator caveGen, Vector3 from, Vector3 to,
                              float isoLevel, System.Random rng)
    {
        float dist  = Vector3.Distance(from, to);
        int   steps = Mathf.Max(1, Mathf.CeilToInt(dist / 0.5f));

        for (int i = 0; i <= steps; i++)
        {
            float   t   = (float)i / steps;
            Vector3 pos = Vector3.Lerp(from, to, t);

            // Y 方向の自然な揺らぎ ±0.5m
            pos.y += ((float)rng.NextDouble() - 0.5f);

            CarvePoint(caveGen, pos, _tunnelRadius, isoLevel);
        }
    }

    // ─── 垂直シャフト彫刻 ────────────────────────────────────────

    /// <summary>
    /// 大部屋リストから最大 _shaftCount 箇所を選択し、垂直シャフト（縦穴）を彫る。
    /// 各シャフトは上下に chunkCountY * ChunkSize * cellSize * 0.3f の長さで伸び、
    /// 端部には radius × 1.5 の広い空間を設けて足場とする。
    /// </summary>
    private void CarveVerticalShafts(CaveGenerator caveGen, List<Vector3> rooms, System.Random rng)
    {
        if (_shaftCount <= 0 || rooms.Count == 0) return;

        Vector3Int cc       = caveGen.ChunkCount3D;
        float      cs       = caveGen.CellSize3D;
        float      isoLevel = caveGen.NoiseConfig.isoLevel;
        float      halfLen  = cc.y * CaveChunk.ChunkSize * cs * 0.3f;

        // Fisher-Yates シャッフルでインデックスをランダム化して先頭から選ぶ
        var indices = new List<int>(rooms.Count);
        for (int i = 0; i < rooms.Count; i++) indices.Add(i);
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        int actualCount = Mathf.Min(_shaftCount, rooms.Count);
        for (int s = 0; s < actualCount; s++)
        {
            Vector3 origin = rooms[indices[s]];
            float   topY   = origin.y + halfLen;
            float   botY   = origin.y - halfLen;

            // 上方向に 0.5m ステップで掘削
            for (float y = origin.y; y <= topY; y += 0.5f)
                CarvePoint(caveGen, new Vector3(origin.x, y, origin.z), _shaftRadius, isoLevel);

            // 下方向に 0.5m ステップで掘削
            for (float y = origin.y; y >= botY; y -= 0.5f)
                CarvePoint(caveGen, new Vector3(origin.x, y, origin.z), _shaftRadius, isoLevel);

            // 上端・下端に広い足場空間
            caveGen.CarveSphericalCavity(new Vector3(origin.x, topY, origin.z), _shaftRadius * 1.5f);
            caveGen.CarveSphericalCavity(new Vector3(origin.x, botY, origin.z), _shaftRadius * 1.5f);
        }
    }

    /// <summary>単一の球形空洞を彫る（チャンク AABB で早期棄却する効率実装）。</summary>
    private void CarvePoint(CaveGenerator caveGen, Vector3 worldCenter,
                             float radius, float isoLevel)
    {
        float carveValue  = isoLevel - 0.15f;
        float cs          = caveGen.CellSize3D;
        float chunkExtent = CaveChunk.ChunkSize * cs;

        foreach (var chunk in caveGen.Chunks)
        {
            Vector3 cp = chunk.transform.position;

            // チャンク AABB vs 球の早期棄却
            Vector3 nearest = new Vector3(
                Mathf.Clamp(worldCenter.x, cp.x, cp.x + chunkExtent),
                Mathf.Clamp(worldCenter.y, cp.y, cp.y + chunkExtent),
                Mathf.Clamp(worldCenter.z, cp.z, cp.z + chunkExtent));
            if (Vector3.Distance(nearest, worldCenter) > radius) continue;

            Vector3 local = worldCenter - cp;
            int minLx = Mathf.Max(0, Mathf.FloorToInt((local.x - radius) / cs));
            int maxLx = Mathf.Min(CaveChunk.ChunkSize, Mathf.CeilToInt((local.x + radius) / cs));
            int minLy = Mathf.Max(0, Mathf.FloorToInt((local.y - radius) / cs));
            int maxLy = Mathf.Min(CaveChunk.ChunkSize, Mathf.CeilToInt((local.y + radius) / cs));
            int minLz = Mathf.Max(0, Mathf.FloorToInt((local.z - radius) / cs));
            int maxLz = Mathf.Min(CaveChunk.ChunkSize, Mathf.CeilToInt((local.z + radius) / cs));

            for (int lx = minLx; lx <= maxLx; lx++)
            for (int ly = minLy; ly <= maxLy; ly++)
            for (int lz = minLz; lz <= maxLz; lz++)
            {
                Vector3 pw = cp + new Vector3(lx, ly, lz) * cs;
                if (Vector3.Distance(pw, worldCenter) <= radius)
                    chunk.SetScalar(lx, ly, lz, Mathf.Min(chunk.GetScalar(lx, ly, lz), carveValue));
            }
        }
    }
}
