using UnityEngine;

namespace Sandbox.World.Generation.Route
{
    /// <summary>
    /// 登攀ルート上の 1 ノード。RouteGraphGenerator が生成、SandboxRoutePath が保持。
    /// </summary>
    public readonly struct RouteNode
    {
        /// <summary>ワールド座標（Y = 地形 surface）。</summary>
        public readonly Vector3 Position;
        /// <summary>当ノードの斜度 [deg]（地形 normal と Up の角度）。難所判定に使用。</summary>
        public readonly float SlopeDeg;
        /// <summary>前ノード→当ノードの区間の難易度（区間内最大斜度ベース、0..1）。</summary>
        public readonly float SegmentDifficulty;

        public RouteNode(Vector3 pos, float slopeDeg, float segDifficulty)
        {
            Position = pos;
            SlopeDeg = slopeDeg;
            SegmentDifficulty = segDifficulty;
        }
    }
}
