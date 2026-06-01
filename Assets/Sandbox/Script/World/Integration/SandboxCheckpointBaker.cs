using UnityEngine;

namespace Sandbox.World.Integration
{
    /// <summary>
    /// チェックポイントを「スポーン地点 → 暫定山頂」を結ぶ直線上に 3 点（25/50/75%）配置する暫定実装。
    /// 各点で下方 Raycast して地面 Y を取得し、Sphere トリガー + `CheckpointTrigger` を生成、
    /// 既存の `GameServices.Checkpoints.RegisterCheckpoint(...)` に登録する。
    /// Step 3 でルートグラフベースの本格配置に置換予定。
    /// </summary>
    public sealed class SandboxCheckpointBaker : MonoBehaviour
    {
        [SerializeField] private float triggerRadius = 3f;
        [SerializeField] private int minChunksReadyToPlace = 4;
        [SerializeField] private float[] altitudeFractions = { 0.25f, 0.50f, 0.75f };
        [SerializeField] private float raycastFromAltitude = 1000f;

        private SandboxBootstrap _bootstrap;
        private SandboxExplorerPositioner _positioner;
        private bool[] _placedIndex;
        private Vector3[] _targetXZ; // 初回 summit 観測時にキャッシュ。以降固定（summit が follow しても影響なし）

        public int PlacedCount
        {
            get
            {
                if (_placedIndex == null) return 0;
                int n = 0; for (int i = 0; i < _placedIndex.Length; i++) if (_placedIndex[i]) n++;
                return n;
            }
        }
        public bool AllPlaced => _placedIndex != null && PlacedCount == _placedIndex.Length;

        private void Awake()
        {
            _bootstrap = GetComponent<SandboxBootstrap>();
            _positioner = GetComponent<SandboxExplorerPositioner>();
            _placedIndex = new bool[altitudeFractions.Length];
        }

        private void Update()
        {
            if (AllPlaced) return;
            if (_bootstrap == null || _bootstrap.ColliderBaker == null) return;
            // 全コライダーのベイクが完了するまで待機
            if (!_bootstrap.ColliderBaker.IsAllBaked(minChunksReadyToPlace)) return;
            if (_bootstrap.ColliderBaker.GlobalMaxY == float.MinValue) return;

            // 初回のみ：RoutePath が利用可能ならルート上の fraction、そうでなければ spawn→summit 直線 fallback で XZ をキャッシュ。
            if (_targetXZ == null)
            {
                var route = GetComponent<SandboxRoutePath>();
                _targetXZ = new Vector3[altitudeFractions.Length];
                if (route != null && route.HasRoute)
                {
                    // 標高 fraction で配置（距離 fraction より登山らしい分散）
                    for (int i = 0; i < altitudeFractions.Length; i++)
                    {
                        float f = Mathf.Clamp01(altitudeFractions[i]);
                        _targetXZ[i] = route.SampleAtAltitudeFraction(f);
                    }
                }
                else
                {
                    var summit = _bootstrap.ColliderBaker.GlobalMaxPos;
                    var spawnXZ = _positioner != null
                        ? _positioner.SpawnXZ
                        : new Vector3(128f, 0f, 128f);
                    var spawnPos = new Vector3(spawnXZ.x, 0f, spawnXZ.z);
                    for (int i = 0; i < altitudeFractions.Length; i++)
                    {
                        float f = Mathf.Clamp01(altitudeFractions[i]);
                        _targetXZ[i] = Vector3.Lerp(spawnPos, summit, f);
                    }
                }
            }

            var system = GameServices.Checkpoints;
            if (system == null)
            {
                var sysGO = new GameObject("CheckpointSystem");
                system = sysGO.AddComponent<CheckpointSystem>();
            }

            // 未配置 index のみ毎フレーム再試行（async collider bake 完了待ち）
            for (int i = 0; i < _targetXZ.Length; i++)
            {
                if (_placedIndex[i]) continue;
                var xz = _targetXZ[i];
                if (!TryGround(new Vector3(xz.x, raycastFromAltitude, xz.z), out var hit)) continue;
                var cp = CreateCheckpoint(hit, i, $"CP{i + 1}");
                system.RegisterCheckpoint(cp.transform);
                _placedIndex[i] = true;
            }
        }

        private bool TryGround(Vector3 origin, out Vector3 hitPoint)
        {
            if (Physics.Raycast(origin, Vector3.down, out var hit, raycastFromAltitude * 2f))
            {
                hitPoint = hit.point;
                return true;
            }
            hitPoint = default;
            return false;
        }

        private GameObject CreateCheckpoint(Vector3 worldPos, int index, string label)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"SandboxCheckpoint_{index}";
            go.transform.SetParent(_bootstrap.TerrainGenerator.transform, false);
            go.transform.position = worldPos + Vector3.up * (triggerRadius * 0.5f);
            go.transform.localScale = Vector3.one * triggerRadius * 2f;

            var col = go.GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.5f;

            var mr = go.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = $"CheckpointMat_{index}" };
            mat.SetColor("_BaseColor", new Color(0.2f, 0.7f, 1f, 1f));
            mat.SetColor("_EmissionColor", new Color(0.1f, 0.45f, 0.9f, 1f) * 1.0f);
            mat.EnableKeyword("_EMISSION");
            mr.sharedMaterial = mat;

            var trg = go.AddComponent<CheckpointTrigger>();
            trg.index = index;
            trg.label = label;
            return go;
        }

    }
}
