using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CaveGenerator が生成した bool グリッドから床・天井・壁の Mesh を構築する。
/// 頂点数上限(65535)対策のため、チャンク分割して SubMesh に出力する。
/// [Fix] 全Meshを両面化・天井法線反転・MeshCollider凹形状対応 を適用済み。
/// </summary>
[RequireComponent(typeof(CaveGenerator))]
public class MeshGenerator : MonoBehaviour
{
    // 1チャンクあたりの最大グリッドセル数（頂点数の余裕を見た経験値）
    const int MAX_CELLS_PER_CHUNK = 4000;

    // ─── 公開エントリポイント ─────────────────────────────────────

    public void BuildMesh(
        bool[,]    grid,
        int        width,
        int        height,
        float      cellSize,
        MeshFilter floorFilter,
        MeshFilter ceilFilter,
        MeshFilter wallFilter)
    {
        // 床と天井
        if (floorFilter != null)
            ApplyMesh(floorFilter, BuildHorizontalMesh(grid, width, height, cellSize, isFloor: true));

        if (ceilFilter != null)
            ApplyMesh(ceilFilter, BuildHorizontalMesh(grid, width, height, cellSize, isFloor: false));

        // 壁
        if (wallFilter != null)
            ApplyMesh(wallFilter, BuildWallMesh(grid, width, height, cellSize));
    }

    // ─── 水平面（床 / 天井） ───────────────────────────────────────

    Mesh BuildHorizontalMesh(bool[,] grid, int width, int height, float cellSize, bool isFloor)
    {
        // 空洞セルを収集
        var cells = new List<Vector2Int>();
        for (int x = 0; x < width;  x++)
        for (int y = 0; y < height; y++)
            if (!grid[x, y]) cells.Add(new Vector2Int(x, y));

        float yPos = isFloor ? 0f : cellSize;         // 床=0, 天井=cellSize
        // [Fix] 両面化のため normal は RecalculateNormals() に任せる
        Vector3 normal = isFloor ? Vector3.up : Vector3.down;

        // チャンク分割してサブメッシュを集約
        var combineSources = new List<CombineInstance>();
        int start = 0;
        while (start < cells.Count)
        {
            int end = Mathf.Min(start + MAX_CELLS_PER_CHUNK, cells.Count);
            var chunk = BuildHorizontalChunk(cells, start, end, cellSize, yPos, normal, isFloor);
            combineSources.Add(new CombineInstance { mesh = chunk, transform = Matrix4x4.identity });
            start = end;
        }

        var combined = new Mesh { name = isFloor ? "FloorMesh" : "CeilMesh" };
        combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        combined.CombineMeshes(combineSources.ToArray(), mergeSubMeshes: true, useMatrices: false);
        combined.RecalculateNormals();
        combined.RecalculateBounds();

        // [Fix] 天井Meshの法線を全て下向きに反転（両面化後も念のため）
        if (!isFloor)
        {
            Vector3[] normals = combined.normals;
            for (int i = 0; i < normals.Length; i++)
                normals[i] = -normals[i];
            combined.normals = normals;
        }

        return combined;
    }

    Mesh BuildHorizontalChunk(
        List<Vector2Int> cells, int start, int end,
        float cellSize, float yPos, Vector3 normal, bool isFloor)
    {
        int count = end - start;
        // [Fix] 両面化のため頂点数・トライアングル数を2倍に拡張
        var verts  = new Vector3[count * 4];
        var tris   = new int[count * 12]; // 片面6 × 2面 = 12
        var uvs    = new Vector2[count * 4];

        for (int i = 0; i < count; i++)
        {
            var c  = cells[start + i];
            float x0 = c.x * cellSize,  x1 = x0 + cellSize;
            float z0 = c.y * cellSize,  z1 = z0 + cellSize;

            int vi = i * 4;
            verts[vi + 0] = new Vector3(x0, yPos, z0);
            verts[vi + 1] = new Vector3(x0, yPos, z1);
            verts[vi + 2] = new Vector3(x1, yPos, z1);
            verts[vi + 3] = new Vector3(x1, yPos, z0);

            uvs[vi + 0] = new Vector2(0, 0);
            uvs[vi + 1] = new Vector2(0, 1);
            uvs[vi + 2] = new Vector2(1, 1);
            uvs[vi + 3] = new Vector2(1, 0);

            int ti = i * 12;
            if (isFloor)
            {
                // 表面（上向き法線）
                tris[ti]   = vi; tris[ti+1] = vi+1; tris[ti+2] = vi+2;
                tris[ti+3] = vi; tris[ti+4] = vi+2; tris[ti+5] = vi+3;
                // [Fix] 裏面（インデックス逆順で法線反転）
                tris[ti+6]  = vi+2; tris[ti+7]  = vi+1; tris[ti+8]  = vi;
                tris[ti+9]  = vi+3; tris[ti+10] = vi+2; tris[ti+11] = vi;
            }
            else
            {
                // 天井は面の向きを反転（下向き法線）
                tris[ti]   = vi; tris[ti+1] = vi+2; tris[ti+2] = vi+1;
                tris[ti+3] = vi; tris[ti+4] = vi+3; tris[ti+5] = vi+2;
                // [Fix] 裏面（インデックス逆順で法線反転）
                tris[ti+6]  = vi+1; tris[ti+7]  = vi+2; tris[ti+8]  = vi;
                tris[ti+9]  = vi+2; tris[ti+10] = vi+3; tris[ti+11] = vi;
            }
        }

        var m = new Mesh();
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(verts);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        return m;
    }

    // ─── 壁面 ────────────────────────────────────────────────────

    Mesh BuildWallMesh(bool[,] grid, int width, int height, float cellSize)
    {
        // 空洞セルに隣接する壁を列挙（エッジ検出）
        var quads = new List<(Vector3 a, Vector3 b, Vector3 c, Vector3 d)>();
        float wallH = cellSize; // 壁の高さ = cellSize（床から天井まで）

        for (int x = 0; x < width;  x++)
        for (int y = 0; y < height; y++)
        {
            if (grid[x, y]) continue; // 空洞のみ処理

            float x0 = x * cellSize,  x1 = x0 + cellSize;
            float z0 = y * cellSize,  z1 = z0 + cellSize;

            // 4方向 (右・左・上・下) に壁がある面を生成
            if (IsWall(grid, x + 1, y, width, height))
                quads.Add((new Vector3(x1, 0,    z0),
                           new Vector3(x1, wallH, z0),
                           new Vector3(x1, wallH, z1),
                           new Vector3(x1, 0,    z1)));

            if (IsWall(grid, x - 1, y, width, height))
                quads.Add((new Vector3(x0, 0,    z1),
                           new Vector3(x0, wallH, z1),
                           new Vector3(x0, wallH, z0),
                           new Vector3(x0, 0,    z0)));

            if (IsWall(grid, x, y + 1, width, height))
                quads.Add((new Vector3(x1, 0,    z1),
                           new Vector3(x1, wallH, z1),
                           new Vector3(x0, wallH, z1),
                           new Vector3(x0, 0,    z1)));

            if (IsWall(grid, x, y - 1, width, height))
                quads.Add((new Vector3(x0, 0,    z0),
                           new Vector3(x0, wallH, z0),
                           new Vector3(x1, wallH, z0),
                           new Vector3(x1, 0,    z0)));
        }

        // チャンク分割
        var combineSources = new List<CombineInstance>();
        int start = 0;
        while (start < quads.Count)
        {
            int end = Mathf.Min(start + MAX_CELLS_PER_CHUNK, quads.Count);
            var chunk = BuildWallChunk(quads, start, end);
            combineSources.Add(new CombineInstance { mesh = chunk, transform = Matrix4x4.identity });
            start = end;
        }

        if (combineSources.Count == 0)
        {
            var empty = new Mesh { name = "WallMesh" };
            return empty;
        }

        var combined = new Mesh { name = "WallMesh" };
        combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        combined.CombineMeshes(combineSources.ToArray(), mergeSubMeshes: true, useMatrices: false);
        combined.RecalculateNormals();
        combined.RecalculateBounds();
        return combined;
    }

    Mesh BuildWallChunk(
        List<(Vector3 a, Vector3 b, Vector3 c, Vector3 d)> quads, int start, int end)
    {
        int count = end - start;
        // [Fix] 両面化のため頂点数・トライアングル数を2倍に拡張
        var verts = new Vector3[count * 4];
        var tris  = new int[count * 12]; // 片面6 × 2面 = 12
        var uvs   = new Vector2[count * 4];

        for (int i = 0; i < count; i++)
        {
            var (a, b, c, d) = quads[start + i];
            int vi = i * 4;
            verts[vi + 0] = a;
            verts[vi + 1] = b;
            verts[vi + 2] = c;
            verts[vi + 3] = d;

            uvs[vi + 0] = new Vector2(0, 0);
            uvs[vi + 1] = new Vector2(0, 1);
            uvs[vi + 2] = new Vector2(1, 1);
            uvs[vi + 3] = new Vector2(1, 0);

            int ti = i * 12;
            // 表面
            tris[ti]   = vi; tris[ti+1] = vi+1; tris[ti+2] = vi+2;
            tris[ti+3] = vi; tris[ti+4] = vi+2; tris[ti+5] = vi+3;
            // [Fix] 裏面（インデックス逆順で法線反転）
            tris[ti+6]  = vi+2; tris[ti+7]  = vi+1; tris[ti+8]  = vi;
            tris[ti+9]  = vi+3; tris[ti+10] = vi+2; tris[ti+11] = vi;
        }

        var m = new Mesh();
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(verts);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        return m;
    }

    // ─── ヘルパー ────────────────────────────────────────────────

    static bool IsWall(bool[,] grid, int x, int y, int width, int height)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return true;
        return grid[x, y];
    }

    // ─── MeshCollider の適用 ──────────────────────────────────────

    static void ApplyMesh(MeshFilter filter, Mesh mesh)
    {
        filter.sharedMesh = mesh;

        var col = filter.GetComponent<MeshCollider>();
        if (col == null) col = filter.gameObject.AddComponent<MeshCollider>();
        col.sharedMesh = mesh;

        // [Fix] 凹形状（洞窟）に対応するため convex=false を明示設定
        col.convex = false;
        // [Fix] メッシュクリーニングと頂点溶接を有効化して判定精度を向上
        col.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning
                           | MeshColliderCookingOptions.WeldColocatedVertices;
    }
}
