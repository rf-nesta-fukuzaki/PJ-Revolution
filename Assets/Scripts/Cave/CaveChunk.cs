using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 16×16×16 グリッドで Marching Cubes を実行して Mesh を生成するチャンク。
/// CaveWorldGenerator から Initialize() を呼び出して使う。
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class CaveChunk : MonoBehaviour
{
    // チャンク1辺のグリッド数
    public const int ChunkSize = 16;

    // ─── 内部状態 ────────────────────────────────────────────────

    private float[,,] _scalarField; // [x, y, z] スカラー値（0〜1）
    private Vector3Int _chunkCoord; // チャンク座標（グリッド単位）
    private float      _cellSize;   // 1セルのワールドサイズ

    // ─── 公開 API ────────────────────────────────────────────────

    /// <summary>チャンクを初期化してMeshを生成する</summary>
    public void Initialize(Vector3Int chunkCoord, int seed,
                           NoiseSettings settings, float cellSize,
                           float worldHeight)
    {
        _chunkCoord = chunkCoord;
        _cellSize   = cellSize;

        // スカラー場をサンプリング（ChunkSize+1 点 × 3軸）
        _scalarField = new float[ChunkSize + 1, ChunkSize + 1, ChunkSize + 1];
        Vector3 worldOrigin = new Vector3(
            chunkCoord.x * ChunkSize * cellSize,
            chunkCoord.y * ChunkSize * cellSize,
            chunkCoord.z * ChunkSize * cellSize);

        for (int x = 0; x <= ChunkSize; x++)
        for (int y = 0; y <= ChunkSize; y++)
        for (int z = 0; z <= ChunkSize; z++)
        {
            float wx = worldOrigin.x + x * cellSize;
            float wy = worldOrigin.y + y * cellSize;
            float wz = worldOrigin.z + z * cellSize;
            _scalarField[x, y, z] = CaveNoiseGenerator.Sample(wx, wy, wz,
                                        settings, seed, worldHeight);
        }

        BuildMesh(settings.isoLevel);
    }

    /// <summary>スカラー場を外部から上書きして再Mesh生成する（強制空洞用）</summary>
    public void OverrideScalarField(float[,,] field, float isoLevel)
    {
        _scalarField = field;
        BuildMesh(isoLevel);
    }

    /// <summary>指定ローカルグリッド点のスカラー値を取得する</summary>
    public float GetScalar(int lx, int ly, int lz)
    {
        if (lx < 0 || lx > ChunkSize || ly < 0 || ly > ChunkSize || lz < 0 || lz > ChunkSize)
            return 1f; // 範囲外は岩扱い
        return _scalarField[lx, ly, lz];
    }

    /// <summary>指定ローカルグリッド点のスカラー値を設定する</summary>
    public void SetScalar(int lx, int ly, int lz, float value)
    {
        if (lx < 0 || lx > ChunkSize || ly < 0 || ly > ChunkSize || lz < 0 || lz > ChunkSize)
            return;
        _scalarField[lx, ly, lz] = value;
    }

    /// <summary>現在のスカラー場から Mesh を再構築する</summary>
    public void RebuildMesh(float isoLevel) => BuildMesh(isoLevel);

    // ─── Marching Cubes ──────────────────────────────────────────

    private void BuildMesh(float isoLevel)
    {
        var vertices  = new List<Vector3>();
        var triangles = new List<int>();
        var uvs       = new List<Vector2>();

        // コーナーのワールド相対オフセット（Marching Cubes 標準配置）
        // コーナー番号は Paul Bourke の定義に従う
        var cornerOffsets = new Vector3[8]
        {
            new Vector3(0, 0, 0), // 0
            new Vector3(1, 0, 0), // 1
            new Vector3(1, 0, 1), // 2
            new Vector3(0, 0, 1), // 3
            new Vector3(0, 1, 0), // 4
            new Vector3(1, 1, 0), // 5
            new Vector3(1, 1, 1), // 6
            new Vector3(0, 1, 1), // 7
        };

        for (int x = 0; x < ChunkSize; x++)
        for (int y = 0; y < ChunkSize; y++)
        for (int z = 0; z < ChunkSize; z++)
        {
            // 8コーナーのスカラー値を取得
            float[] cornerValues = new float[8];
            for (int c = 0; c < 8; c++)
            {
                int cx = x + (int)cornerOffsets[c].x;
                int cy = y + (int)cornerOffsets[c].y;
                int cz = z + (int)cornerOffsets[c].z;
                cornerValues[c] = _scalarField[cx, cy, cz];
            }

            // 8ビットマスクを計算（isoLevel より小さい = 空洞 = ビット0）
            int cubeIndex = 0;
            for (int c = 0; c < 8; c++)
                if (cornerValues[c] < isoLevel)
                    cubeIndex |= (1 << c);

            // 完全に岩 or 完全に空洞はスキップ
            if (cubeIndex == 0 || cubeIndex == 255) continue;

            // 辺上の補間頂点を計算（12辺分）
            var edgeVertices = new Vector3[12];
            for (int e = 0; e < 12; e++)
            {
                int a = MarchingCubesTable.cornerIndexAFromEdge[e];
                int b = MarchingCubesTable.cornerIndexBFromEdge[e];

                Vector3 posA = (new Vector3(x, y, z) + cornerOffsets[a]) * _cellSize;
                Vector3 posB = (new Vector3(x, y, z) + cornerOffsets[b]) * _cellSize;
                float   valA = cornerValues[a];
                float   valB = cornerValues[b];

                // 等値面位置を線形補間で求める
                float t = Mathf.Approximately(valB - valA, 0f)
                    ? 0.5f
                    : (isoLevel - valA) / (valB - valA);

                edgeVertices[e] = Vector3.Lerp(posA, posB, t);
            }

            // テーブルから三角形を生成
            for (int t = 0; MarchingCubesTable.triangulation[cubeIndex, t] != -1; t += 3)
            {
                int e0 = MarchingCubesTable.triangulation[cubeIndex, t];
                int e1 = MarchingCubesTable.triangulation[cubeIndex, t + 1];
                int e2 = MarchingCubesTable.triangulation[cubeIndex, t + 2];

                int baseIndex = vertices.Count;
                vertices.Add(edgeVertices[e0]);
                vertices.Add(edgeVertices[e1]);
                vertices.Add(edgeVertices[e2]);

                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);

                // Triplanar UV（法線方向で投影面を選択）
                Vector3 v0 = edgeVertices[e0];
                Vector3 v1 = edgeVertices[e1];
                Vector3 v2 = edgeVertices[e2];
                Vector3 faceNormal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                uvs.Add(TriplanarUV(v0, faceNormal));
                uvs.Add(TriplanarUV(v1, faceNormal));
                uvs.Add(TriplanarUV(v2, faceNormal));
            }
        }

        // Mesh を組み立てて適用
        var mesh = new Mesh
        {
            name        = $"CaveChunk_{_chunkCoord.x}_{_chunkCoord.y}_{_chunkCoord.z}",
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
        };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var mf = GetComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        var mc = GetComponent<MeshCollider>();
        if (vertices.Count > 0)
        {
            mc.sharedMesh     = mesh;
            mc.convex         = false;
            mc.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning
                              | MeshColliderCookingOptions.WeldColocatedVertices;
        }
        else
        {
            mc.sharedMesh = null;
        }
    }

    // ─── ヘルパー ────────────────────────────────────────────────

    /// <summary>
    /// 法線方向に応じた Triplanar UV を返す。
    /// 岩肌テクスチャが自然に貼られるようにする。
    /// </summary>
    private static Vector2 TriplanarUV(Vector3 pos, Vector3 normal)
    {
        float ax = Mathf.Abs(normal.x);
        float ay = Mathf.Abs(normal.y);
        float az = Mathf.Abs(normal.z);

        if (ax >= ay && ax >= az)
            return new Vector2(pos.z, pos.y); // YZ面
        if (ay >= ax && ay >= az)
            return new Vector2(pos.x, pos.z); // XZ面（水平面）
        return new Vector2(pos.x, pos.y);     // XY面
    }
}
