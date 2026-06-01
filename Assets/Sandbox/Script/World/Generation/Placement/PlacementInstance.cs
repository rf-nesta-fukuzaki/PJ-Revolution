using System.Runtime.InteropServices;
using UnityEngine;

namespace Sandbox.World.Generation.Placement
{
    /// <summary>
    /// GPU scatter パスが AppendStructuredBuffer に書き出す 1 インスタンス。
    /// HLSL 側の struct とレイアウト・stride を一致させること（PlacementScatter.compute）。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PlacementInstance
    {
        public Vector3 position;   // 12B — ワールド座標（チャンク原点相対ではなく絶対）
        public float   scale;      // 4B
        public float   rotationY;  // 4B — Y 軸回転 [rad]
        public uint    prototype;  // 4B — 0=Tree, 1=Rock など

        public const int Stride = 24; // 3*4 + 4 + 4 + 4
    }
}
