using Unity.Mathematics;

namespace Sandbox.World
{
    public readonly struct ChunkContext
    {
        public readonly ChunkCoord Coord;
        public readonly float3 WorldOrigin;
        public readonly float CellSize;
        public readonly int Resolution;
        public readonly int Apron;
        public readonly int Lod;
        public readonly uint Seed;

        public ChunkContext(ChunkCoord coord, float3 worldOrigin, float cellSize,
                            int resolution, int apron, int lod, uint seed)
        {
            Coord = coord;
            WorldOrigin = worldOrigin;
            CellSize = cellSize;
            Resolution = resolution;
            Apron = apron;
            Lod = lod;
            Seed = seed;
        }

        public int FullResolution => Resolution + 2 * Apron;
        // 共有エッジタイリング: 隣接チャンクは境界の頂点列を共有する。
        // メッシュ/コライダーは local [0 .. (Resolution-1)*CellSize] を張るので、
        // チャンク間隔も (Resolution-1)*CellSize でなければ 1 セル分の隙間
        // (見た目のグリッド継ぎ目 + 当たり判定の抜け) が生じる。
        public float ChunkWorldSize => (Resolution - 1) * CellSize;
    }
}
