using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.World.Generation.Route
{
    /// <summary>
    /// 既にベイク済みの地形 MeshCollider に対し、上方からの Raycast で高度マップを構築し、
    /// A* で「歩ける勾配」優先のルートを生成する。CPU 完結・1 度きりの起動時計算。
    ///
    /// Cost = step_distance + slopeFactor * slopeDeg + cliffPenalty (if slope &gt; maxClimbable)
    /// Heuristic = Euclidean XZ to goal（admissible）。
    /// 計算後に collinear 簡約化で waypoint を ~10-20 個に圧縮。
    /// </summary>
    public static class RouteGraphGenerator
    {
        public sealed class Config
        {
            public Vector2 BoundsMin;     // XZ min（loaded chunk 群の bbox）
            public Vector2 BoundsMax;     // XZ max
            public int GridResolution = 48;
            public float RaycastHeight = 1000f;
            public float MaxRaycastDistance = 2000f;
            public LayerMask GroundLayer = ~0;
            // ── cost 設計 ──
            // step + SlopeFactor*slopeDeg + max(0, goal.Y - cur.Y)*UpwardBias - max(0, cur.Y - prev.Y)*ClimbReward + (slope>maxClimbable? CliffPenalty : 0)
            // SlopeFactor を抑えめにしないと A* が低地周回を選ぶ。Cliff のみ強く嫌う設計。
            public float SlopeFactor = 0.3f;        // 斜度 1deg あたりのコスト加算
            public float MaxClimbableSlope = 70f;   // これを超える斜面は cliffPenalty を適用
            public float CliffPenalty = 200f;       // 崖侵入の追加コスト
            public float UpwardBias = 0.0f;         // 高度差ペナルティ（未使用、ヒューリスティック側で吸収）
            public float ClimbReward = 3.0f;        // 登り 1m あたりコスト減（強めにして「登る方が安価」を成立させる）
            public float CollinearAngleDeg = 6f;    // 簡約化しきい値
        }

        /// <summary>
        /// Step 3 v1: シンプルな「spawn→summit XZ 直線 + 地形 Y スナップ」の curated 風ルート生成。
        /// 山岳の Ridged Multifractal 地形では尖った peak への gentle 道がそもそも存在しないため、
        /// A* は「低地周回→最終ジャンプ」になりがち。Step 3 では直線を採用し、登攀の意図を明示する。
        /// A* バリアントは GenerateAStar として private 保持（将来の改良候補）。
        /// </summary>
        public static List<RouteNode> Generate(Vector3 startWorld, Vector3 goalWorld, Config cfg)
        {
            int N = Mathf.Max(8, cfg.GridResolution);
            var nodes = new List<RouteNode>(N);
            for (int i = 0; i < N; i++)
            {
                float t = (float)i / (N - 1);
                var xz = Vector3.Lerp(startWorld, goalWorld, t);
                float y;
                float slope;
                var origin = new Vector3(xz.x, cfg.RaycastHeight, xz.z);
                if (Physics.Raycast(origin, Vector3.down, out var hit, cfg.MaxRaycastDistance, cfg.GroundLayer, QueryTriggerInteraction.Ignore))
                {
                    y = hit.point.y;
                    slope = Vector3.Angle(hit.normal, Vector3.up);
                }
                else { y = xz.y; slope = 0f; }
                // 終端は実 summit Y を保証（量子化誤差でずれるのを防ぐ）
                if (i == N - 1) y = goalWorld.y;
                nodes.Add(new RouteNode(new Vector3(xz.x, y, xz.z), slope, Mathf.Clamp01(slope / 90f)));
            }
            return nodes;
        }

        /// <summary>A* バリアント（保存用・現状未使用）。山岳地形では狙った結果が得にくいため Step 3 では未採用。</summary>
        public static List<RouteNode> GenerateAStar(Vector3 startWorld, Vector3 goalWorld, Config cfg)
        {
            int gn = Mathf.Max(8, cfg.GridResolution);
            var size = cfg.BoundsMax - cfg.BoundsMin;
            float cellSizeX = size.x / (gn - 1);
            float cellSizeZ = size.y / (gn - 1);

            var grid = SampleHeightGrid(gn, cfg, cellSizeX, cellSizeZ);

            var startIdx = WorldToCell(new Vector2(startWorld.x, startWorld.z), cfg.BoundsMin, cellSizeX, cellSizeZ, gn);
            var goalIdx  = WorldToCell(new Vector2(goalWorld.x,  goalWorld.z),  cfg.BoundsMin, cellSizeX, cellSizeZ, gn);
            startIdx = SnapToNearestValid(grid, gn, startIdx);
            goalIdx  = SnapToNearestValid(grid, gn, goalIdx);
            if (grid[goalIdx.x, goalIdx.y].Valid)
            {
                var g = grid[goalIdx.x, goalIdx.y];
                g.Pos = goalWorld;
                grid[goalIdx.x, goalIdx.y] = g;
            }
            var rawPath = AStar(grid, gn, startIdx, goalIdx, cfg, cellSizeX, cellSizeZ);
            if (rawPath == null) return new List<RouteNode>();
            var worldPath = new List<Vector3>(rawPath.Count);
            var slopeList = new List<float>(rawPath.Count);
            foreach (var idx in rawPath)
            {
                var cell = grid[idx.x, idx.y];
                if (!cell.Valid) continue;
                worldPath.Add(cell.Pos);
                slopeList.Add(cell.SlopeDeg);
            }
            var simplified = SimplifyCollinear(worldPath, slopeList, cfg.CollinearAngleDeg);
            return BuildRouteNodes(simplified.points, simplified.slopes);
        }

        // ───────── grid sampling ─────────

        private struct Cell
        {
            public Vector3 Pos;       // ワールド座標
            public float SlopeDeg;    // 0..90
            public bool Valid;        // Raycast hit & ground layer
        }

        private static Cell[,] SampleHeightGrid(int gn, Config cfg, float cellSizeX, float cellSizeZ)
        {
            var grid = new Cell[gn, gn];
            for (int z = 0; z < gn; z++)
            {
                float wz = cfg.BoundsMin.y + z * cellSizeZ;
                for (int x = 0; x < gn; x++)
                {
                    float wx = cfg.BoundsMin.x + x * cellSizeX;
                    var origin = new Vector3(wx, cfg.RaycastHeight, wz);
                    if (Physics.Raycast(origin, Vector3.down, out var hit, cfg.MaxRaycastDistance, cfg.GroundLayer, QueryTriggerInteraction.Ignore))
                    {
                        float slope = Vector3.Angle(hit.normal, Vector3.up);
                        grid[x, z] = new Cell { Pos = hit.point, SlopeDeg = slope, Valid = true };
                    }
                }
            }
            return grid;
        }

        private static Vector2Int WorldToCell(Vector2 xz, Vector2 boundsMin, float csX, float csZ, int gn)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt((xz.x - boundsMin.x) / csX), 0, gn - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt((xz.y - boundsMin.y) / csZ), 0, gn - 1);
            return new Vector2Int(x, z);
        }

        private static Vector2Int SnapToNearestValid(Cell[,] grid, int gn, Vector2Int idx)
        {
            if (grid[idx.x, idx.y].Valid) return idx;
            for (int r = 1; r < gn; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue; // 現在のリングのみ
                        int nx = idx.x + dx, nz = idx.y + dz;
                        if (nx < 0 || nz < 0 || nx >= gn || nz >= gn) continue;
                        if (grid[nx, nz].Valid) return new Vector2Int(nx, nz);
                    }
                }
            }
            return idx; // 全 invalid
        }

        // ───────── A* ─────────

        private static readonly (int dx, int dz)[] Neigh = new (int, int)[]
        {
            (1,0),(-1,0),(0,1),(0,-1),(1,1),(1,-1),(-1,1),(-1,-1)
        };

        private static List<Vector2Int> AStar(Cell[,] grid, int gn, Vector2Int start, Vector2Int goal, Config cfg, float csX, float csZ)
        {
            if (!grid[start.x, start.y].Valid || !grid[goal.x, goal.y].Valid) return null;

            var open = new MinHeap<Vector2Int>();
            var gScore = new Dictionary<Vector2Int, float>();
            var came = new Dictionary<Vector2Int, Vector2Int>();
            gScore[start] = 0f;
            open.Push(start, Heuristic(start, goal, grid, csX, csZ));

            while (open.Count > 0)
            {
                var cur = open.Pop();
                if (cur == goal) return Reconstruct(came, cur);

                foreach (var (dx, dz) in Neigh)
                {
                    int nx = cur.x + dx, nz = cur.y + dz;
                    if (nx < 0 || nz < 0 || nx >= gn || nz >= gn) continue;
                    var n = new Vector2Int(nx, nz);
                    if (!grid[nx, nz].Valid) continue;

                    float step = Mathf.Sqrt((dx * csX) * (dx * csX) + (dz * csZ) * (dz * csZ));
                    float slope = grid[nx, nz].SlopeDeg;
                    float dy = grid[nx, nz].Pos.y - grid[cur.x, cur.y].Pos.y;
                    // 登りはコスト軽減（goal が高い場合に有効）、降りは neutral
                    float climbBonus = (dy > 0f) ? -cfg.ClimbReward * dy : 0f;
                    float cost = step + cfg.SlopeFactor * slope + climbBonus;
                    if (slope > cfg.MaxClimbableSlope) cost += cfg.CliffPenalty;
                    if (cost < 0.01f) cost = 0.01f; // 非正コスト防止

                    float tentative = gScore[cur] + cost;
                    if (!gScore.TryGetValue(n, out var existing) || tentative < existing)
                    {
                        gScore[n] = tentative;
                        came[n] = cur;
                        open.Push(n, tentative + Heuristic(n, goal, grid, csX, csZ));
                    }
                }
            }
            return null;
        }

        private static float Heuristic(Vector2Int a, Vector2Int b, Cell[,] grid, float csX, float csZ)
        {
            float dx = (a.x - b.x) * csX;
            float dz = (a.y - b.y) * csZ;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static List<Vector2Int> Reconstruct(Dictionary<Vector2Int, Vector2Int> came, Vector2Int end)
        {
            var path = new List<Vector2Int> { end };
            while (came.TryGetValue(end, out var prev)) { end = prev; path.Add(end); }
            path.Reverse();
            return path;
        }

        // ───────── simplify ─────────

        private static (List<Vector3> points, List<float> slopes) SimplifyCollinear(List<Vector3> pts, List<float> slopes, float angleDeg)
        {
            if (pts.Count <= 2) return (pts, slopes);
            var outPts = new List<Vector3> { pts[0] };
            var outSlopes = new List<float> { slopes[0] };
            for (int i = 1; i < pts.Count - 1; i++)
            {
                var a = (pts[i] - pts[i - 1]); a.y = 0;
                var b = (pts[i + 1] - pts[i]); b.y = 0;
                if (a.sqrMagnitude < 1e-4f || b.sqrMagnitude < 1e-4f) continue;
                float ang = Vector3.Angle(a, b);
                if (ang >= angleDeg)
                {
                    outPts.Add(pts[i]);
                    outSlopes.Add(slopes[i]);
                }
            }
            outPts.Add(pts[pts.Count - 1]);
            outSlopes.Add(slopes[slopes.Count - 1]);
            return (outPts, outSlopes);
        }

        private static List<RouteNode> BuildRouteNodes(List<Vector3> pts, List<float> slopes)
        {
            var nodes = new List<RouteNode>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                float seg = 0f;
                if (i > 0) seg = Mathf.Clamp01(slopes[i] / 90f);
                nodes.Add(new RouteNode(pts[i], slopes[i], seg));
            }
            return nodes;
        }

        // ───────── 簡易 binary heap（System.Collections.Generic.PriorityQueue が当環境で未提供のため自前実装） ─────────
        private sealed class MinHeap<T>
        {
            private readonly List<(T item, float pri)> _h = new List<(T, float)>();
            public int Count => _h.Count;
            public void Push(T item, float pri)
            {
                _h.Add((item, pri));
                int i = _h.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (_h[p].pri <= _h[i].pri) break;
                    (_h[p], _h[i]) = (_h[i], _h[p]);
                    i = p;
                }
            }
            public T Pop()
            {
                var top = _h[0].item;
                int last = _h.Count - 1;
                _h[0] = _h[last];
                _h.RemoveAt(last);
                int i = 0; int n = _h.Count;
                while (true)
                {
                    int l = i * 2 + 1, r = i * 2 + 2, s = i;
                    if (l < n && _h[l].pri < _h[s].pri) s = l;
                    if (r < n && _h[r].pri < _h[s].pri) s = r;
                    if (s == i) break;
                    (_h[s], _h[i]) = (_h[i], _h[s]);
                    i = s;
                }
                return top;
            }
        }
    }
}
