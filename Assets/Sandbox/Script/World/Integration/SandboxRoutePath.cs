using System.Collections.Generic;
using UnityEngine;
using Sandbox.World.Generation.Route;

namespace Sandbox.World.Integration
{
    /// <summary>
    /// 生成済み登攀ルートの保持・可視化・サンプリング API。
    /// SandboxBootstrap が IsAllBaked 後に RouteGraphGenerator を回し、SetRoute(...) でセット。
    /// SandboxCheckpointBaker / SandboxGrappableHints から参照される。
    /// </summary>
    public sealed class SandboxRoutePath : MonoBehaviour
    {
        [SerializeField] private bool drawLine = true;
        [SerializeField] private float lineWidth = 0.6f;
        [SerializeField] private Color lineColorBase = new Color(0.2f, 0.9f, 0.4f, 0.9f); // 緑（緩斜面）
        [SerializeField] private Color lineColorHard = new Color(1.0f, 0.3f, 0.2f, 0.9f); // 赤（難所）

        private LineRenderer _line;
        private readonly List<RouteNode> _nodes = new List<RouteNode>();

        public IReadOnlyList<RouteNode> Nodes => _nodes;
        public bool HasRoute => _nodes.Count >= 2;

        public void SetRoute(List<RouteNode> nodes)
        {
            _nodes.Clear();
            _nodes.AddRange(nodes);
            UpdateLine();
        }

        /// <summary>標高 fraction (0..1) に対応するルート上の位置を返す（CP 等価距離より「登山らしい」配置）。</summary>
        public Vector3 SampleAtAltitudeFraction(float t)
        {
            t = Mathf.Clamp01(t);
            if (_nodes.Count == 0) return Vector3.zero;
            if (_nodes.Count == 1) return _nodes[0].Position;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < _nodes.Count; i++)
            {
                float y = _nodes[i].Position.y;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
            float targetY = Mathf.Lerp(minY, maxY, t);
            // 最初に targetY 以上の Y に到達する node を選ぶ
            for (int i = 0; i < _nodes.Count; i++)
                if (_nodes[i].Position.y >= targetY) return _nodes[i].Position;
            return _nodes[_nodes.Count - 1].Position;
        }

        /// <summary>累積距離での fraction (0..1) に対応する位置を返す。</summary>
        public Vector3 SampleAtFraction(float t)
        {
            t = Mathf.Clamp01(t);
            if (_nodes.Count == 0) return Vector3.zero;
            if (_nodes.Count == 1) return _nodes[0].Position;

            float total = 0f;
            for (int i = 1; i < _nodes.Count; i++) total += Vector3.Distance(_nodes[i - 1].Position, _nodes[i].Position);
            float target = total * t;
            float acc = 0f;
            for (int i = 1; i < _nodes.Count; i++)
            {
                float seg = Vector3.Distance(_nodes[i - 1].Position, _nodes[i].Position);
                if (acc + seg >= target)
                {
                    float u = seg < 1e-3f ? 0 : (target - acc) / seg;
                    return Vector3.Lerp(_nodes[i - 1].Position, _nodes[i].Position, u);
                }
                acc += seg;
            }
            return _nodes[_nodes.Count - 1].Position;
        }

        /// <summary>難所（slope &gt; thresholdDeg）の RouteNode index を列挙。</summary>
        public IEnumerable<int> EnumerateDifficultIndices(float thresholdDeg)
        {
            for (int i = 0; i < _nodes.Count; i++)
                if (_nodes[i].SlopeDeg >= thresholdDeg) yield return i;
        }

        private void UpdateLine()
        {
            if (!drawLine) return;
            if (_line == null)
            {
                _line = gameObject.AddComponent<LineRenderer>();
                _line.useWorldSpace = true;
                _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _line.receiveShadows = false;
                var sh = Shader.Find("Universal Render Pipeline/Unlit");
                _line.material = new Material(sh) { name = "RouteLineMat" };
            }
            if (_nodes.Count == 0) return;

            _line.positionCount = _nodes.Count;
            _line.startWidth = _line.endWidth = lineWidth;

            // 位置は全ノード反映
            for (int i = 0; i < _nodes.Count; i++)
                _line.SetPosition(i, _nodes[i].Position + Vector3.up * 0.5f); // 地面より少し浮かす

            // 斜度に応じた色 gradient。Unity の Gradient は最大 8 色キーなので、
            // ノード数に関わらず最大 8 点でサンプリングして上限超過の警告を防ぐ。
            const int maxKeys = 8;
            int keyCount = Mathf.Clamp(_nodes.Count, 1, maxKeys);
            var keys = new GradientColorKey[keyCount];
            for (int k = 0; k < keyCount; k++)
            {
                float t = keyCount == 1 ? 0f : (float)k / (keyCount - 1);
                int idx = Mathf.RoundToInt(t * (_nodes.Count - 1));
                var c = Color.Lerp(lineColorBase, lineColorHard, Mathf.Clamp01(_nodes[idx].SlopeDeg / 60f));
                keys[k] = new GradientColorKey(c, t);
            }
            var grad = new Gradient();
            grad.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            _line.colorGradient = grad;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_nodes.Count < 2) return;
            for (int i = 1; i < _nodes.Count; i++)
            {
                Gizmos.color = Color.Lerp(lineColorBase, lineColorHard, Mathf.Clamp01(_nodes[i].SlopeDeg / 60f));
                Gizmos.DrawLine(_nodes[i - 1].Position, _nodes[i].Position);
            }
        }
#endif
    }
}
