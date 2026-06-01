using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.World.Integration
{
    /// <summary>
    /// RoutePath 上で「難所」と判定された node 付近に Grappable プリミティブを追加配置する。
    /// 既存の GPU placement はそのままで、登攀補助の意味付け配置を上乗せする位置づけ。
    /// Step 4 でアセット差し替え可能（Inspector の `rockPrefab` に好きなメッシュを割当て）。
    /// </summary>
    public sealed class SandboxGrappableHints : MonoBehaviour
    {
        [Tooltip("これより斜度が大きい node を「難所」とみなす [deg]。")]
        [SerializeField] private float difficultSlopeDeg = 40f;
        [Tooltip("1 難所 node あたりの追加 Grappable 数。")]
        [SerializeField] private int hintsPerNode = 1;
        [Tooltip("Hint 同士の最小距離 [m]。これ未満なら skip し密集を防ぐ。")]
        [SerializeField] private float minSpacing = 8f;
        [Tooltip("最大 Hint 数。超えたら追加しない（性能と視覚整理のため）。")]
        [SerializeField] private int maxHints = 60;
        [Tooltip("Grappable Tag。プロジェクトに 'Grappable' タグがある前提。")]
        [SerializeField] private string grappableTag = "Grappable";
        [SerializeField] private float scatterRadius = 5f;
        [SerializeField] private float rockSize = 1.4f;
        [SerializeField] private float raycastFromAltitude = 1000f;
        [SerializeField] private bool snapToGround = true;
        [Tooltip("これを assign すると Cube プリミティブの代わりに使われる（Step 4/5 アート差し替え）。")]
        [SerializeField] private GameObject rockPrefab;

        private SandboxBootstrap _bootstrap;
        private bool _placed;
        public int PlacedCount { get; private set; }

        private void Awake() { _bootstrap = GetComponent<SandboxBootstrap>(); }

        private void Update()
        {
            if (_placed) return;
            if (_bootstrap == null || _bootstrap.ColliderBaker == null) return;
            if (!_bootstrap.ColliderBaker.IsAllBaked(1)) return;

            var route = GetComponent<SandboxRoutePath>();
            if (route == null || !route.HasRoute) return;

            int total = 0;
            var rng = new System.Random(12345); // 決定論的
            var placed = new System.Collections.Generic.List<Vector3>(maxHints);
            float minSpSq = minSpacing * minSpacing;

            foreach (var idx in route.EnumerateDifficultIndices(difficultSlopeDeg))
            {
                if (total >= maxHints) break;
                var center = route.Nodes[idx].Position;
                for (int i = 0; i < hintsPerNode; i++)
                {
                    if (total >= maxHints) break;
                    float ang = (float)(rng.NextDouble() * System.Math.PI * 2.0);
                    float r = (float)rng.NextDouble() * scatterRadius;
                    var xz = new Vector3(center.x + Mathf.Cos(ang) * r, 0, center.z + Mathf.Sin(ang) * r);
                    Vector3 pos;
                    if (snapToGround)
                    {
                        if (!Physics.Raycast(new Vector3(xz.x, raycastFromAltitude, xz.z), Vector3.down, out var hit, raycastFromAltitude * 2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        pos = hit.point;
                    }
                    else pos = xz;

                    // 最小距離フィルタ（密集回避）
                    bool tooClose = false;
                    for (int k = 0; k < placed.Count; k++)
                    {
                        if ((placed[k] - pos).sqrMagnitude < minSpSq) { tooClose = true; break; }
                    }
                    if (tooClose) continue;

                    CreateRock(pos, total);
                    placed.Add(pos);
                    total++;
                }
            }
            PlacedCount = total;
            _placed = true;
        }

        private void CreateRock(Vector3 worldPos, int idx)
        {
            GameObject go;
            if (rockPrefab != null)
            {
                go = Instantiate(rockPrefab, worldPos, Quaternion.identity, _bootstrap.TerrainGenerator.transform);
            }
            else
            {
                // プロシージャルなボルダー（低ポリ球を radial ノイズで変形）+ MeshCollider（grapple raycast 用）
                go = new GameObject($"GrappableHint_{idx}");
                go.transform.SetParent(_bootstrap.TerrainGenerator.transform, false);
                go.transform.position = worldPos + Vector3.up * (rockSize * 0.25f);
                float s = rockSize * (0.8f + (float)((idx * 1664525L + 1013904223L) & 0xFF) / 255f * 0.7f);
                go.transform.localScale = new Vector3(s, s * 0.85f, s);
                go.transform.rotation = Quaternion.Euler(0f, (idx * 47f) % 360f, 0f);

                var mesh = BuildBoulderMesh(idx, 10, 8);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "GrappableHintMat" };
                mat.SetColor("_BaseColor", new Color(0.46f, 0.40f, 0.34f));
                mat.SetFloat("_Smoothness", 0.12f);
                mr.sharedMaterial = mat;

                var col = go.AddComponent<MeshCollider>();
                col.sharedMesh = mesh;
                col.convex = true;
            }
            go.name = $"GrappableHint_{idx}";
            try { go.tag = grappableTag; }
            catch (UnityException) { /* タグ未定義 → Untagged のまま */ }
        }

        // 緯度経度球の頂点を seed 由来の radial ノイズで変形した低ポリ岩メッシュ。
        private static Mesh BuildBoulderMesh(int seed, int lon, int lat)
        {
            lon = Mathf.Max(6, lon); lat = Mathf.Max(4, lat);
            var rng = new System.Random(seed * 7919 + 17);
            // 各 (lat ring) ごとに乱数係数を持たせ、縦方向にも凹凸を出す
            int vCount = (lat + 1) * (lon + 1);
            var verts = new Vector3[vCount];
            var norms = new Vector3[vCount];
            int vi = 0;
            for (int y = 0; y <= lat; y++)
            {
                float v = (float)y / lat;
                float phi = v * Mathf.PI; // 0..pi
                for (int x = 0; x <= lon; x++)
                {
                    float u = (float)x / lon;
                    float theta = u * Mathf.PI * 2f;
                    var dir = new Vector3(
                        Mathf.Sin(phi) * Mathf.Cos(theta),
                        Mathf.Cos(phi),
                        Mathf.Sin(phi) * Mathf.Sin(theta));
                    // 複数周波の擬似ノイズで半径を乱す
                    float n =
                        0.18f * (float)(rng.NextDouble() - 0.5) +
                        0.12f * Mathf.Sin(theta * 3f + seed) +
                        0.10f * Mathf.Cos(phi * 4f + seed * 0.5f);
                    float r = 0.5f * (1f + n);
                    verts[vi] = dir * r;
                    vi++;
                }
            }
            var tris = new System.Collections.Generic.List<int>(lat * lon * 6);
            for (int y = 0; y < lat; y++)
            {
                for (int x = 0; x < lon; x++)
                {
                    int a = y * (lon + 1) + x;
                    int b = a + 1;
                    int c = a + (lon + 1);
                    int d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }
            var m = new Mesh { name = "Boulder" };
            m.vertices = verts;
            m.triangles = tris.ToArray();
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
