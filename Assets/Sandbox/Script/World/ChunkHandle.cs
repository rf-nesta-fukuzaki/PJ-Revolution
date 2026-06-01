namespace Sandbox.World
{
    public enum ChunkState
    {
        Reserved,
        BaseBuilt,
        Eroded,
        BiomeReady,
        Ready,
        Evicting
    }

    /// <summary>
    /// アクティブなチャンクのランタイム表現。
    /// 後続フェーズで Erosion/Placement 用バッファ参照を追加予定。
    /// </summary>
    public sealed class ChunkHandle
    {
        public ChunkCoord Coord { get; }
        public ChunkBufferSet Buffers { get; }
        public ChunkContext Context { get; }
        public ChunkState State { get; internal set; }
        public int CurrentLod { get; internal set; }

        public ChunkHandle(ChunkCoord coord, ChunkBufferSet buffers, ChunkContext context)
        {
            Coord = coord;
            Buffers = buffers;
            Context = context;
            State = ChunkState.Reserved;
            CurrentLod = context.Lod;
        }
    }
}
