using UnityEngine;

namespace Sandbox.World.Integration
{
    /// <summary>
    /// ChunkColliderBaker が観測した「全チャンク中の最高点」に SummitGoal を配置する。
    /// 既存の global namespace の `SummitGoal`（OnTriggerEnter で Player タグ判定）をそのまま使う。
    /// 最初は `minChunksReadyToPlace` 個のチャンクがコライダーベイク済みになるまで待ち、その後は
    /// より高いピークを観測したら追従して移動（暫定実装。Step 3 で curated 山頂位置に置き換え予定）。
    /// </summary>
    public sealed class SandboxSummitGoal : MonoBehaviour
    {
        [SerializeField] private float triggerRadius = 4f;
        [SerializeField] private int minChunksReadyToPlace = 3;
        [SerializeField] private bool followHighestPeak = true;

        private SandboxBootstrap _bootstrap;
        private GameObject _goal;
        private float _placedAtY = float.MinValue;

        /// <summary>山頂ゴールが配置済みか（FX 群が追従判定に使う唯一の真実）。</summary>
        public bool HasSummit => _goal != null;

        /// <summary>確定済みの山頂ワールド座標。未配置時は Vector3.zero。</summary>
        public Vector3 SummitPosition => _goal != null ? _goal.transform.position : Vector3.zero;

        private void Awake() { _bootstrap = GetComponent<SandboxBootstrap>(); }

        private void Update()
        {
            if (_bootstrap == null || _bootstrap.ColliderBaker == null) return;
            var baker = _bootstrap.ColliderBaker;
            // 全コライダーがベイクされるまで設置を遅延（局所最大の "first peak" が確定するため）
            if (!baker.IsAllBaked(minChunksReadyToPlace)) return;
            if (baker.GlobalMaxY == float.MinValue) return;

            if (_goal == null)
            {
                _goal = CreateGoal(baker.GlobalMaxPos);
                _placedAtY = baker.GlobalMaxY;
                Debug.Log($"[SandboxSummitGoal] placed at {baker.GlobalMaxPos}");
            }
            else if (followHighestPeak && baker.GlobalMaxY > _placedAtY + 0.5f)
            {
                _goal.transform.position = baker.GlobalMaxPos;
                _placedAtY = baker.GlobalMaxY;
            }
        }

        private GameObject CreateGoal(Vector3 worldPos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SandboxSummitGoal";
            go.transform.SetParent(_bootstrap.TerrainGenerator.transform, false);
            go.transform.position = worldPos;
            go.transform.localScale = Vector3.one * triggerRadius * 2f;

            // 衝突は trigger 化（既存 SummitGoal は OnTriggerEnter ベース）
            var col = go.GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.5f; // 局所半径（スケール 2*radius で実半径 = triggerRadius）

            // 視認用の発光マテリアル（URP/Lit、emission 黄）
            var mr = go.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "SummitGoalMat" };
            mat.SetColor("_BaseColor", new Color(1f, 0.85f, 0.1f, 1f));
            mat.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.0f, 1f) * 1.5f);
            mat.EnableKeyword("_EMISSION");
            mr.sharedMaterial = mat;

            // 既存 SummitGoal をアタッチ（クリア処理を流用）
            go.AddComponent<SummitGoal>();

            AddFlag(go.transform, triggerRadius);
            return go;
        }

        // 山頂ランドマークの旗（ポール + 旗布）。トリガー球の子として装飾配置（コライダー無し）。
        private static void AddFlag(Transform parent, float radius)
        {
            // 親はスケール 2*radius されているので、子はローカル基準（親スケールを打ち消し）
            float inv = 1f / Mathf.Max(0.001f, parent.localScale.x);

            // ポール（細い Cylinder）
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "FlagPole";
            DestroyCollider(pole);
            pole.transform.SetParent(parent, false);
            float poleH = 6f * inv;
            pole.transform.localScale = new Vector3(0.12f * inv, poleH * 0.5f, 0.12f * inv);
            pole.transform.localPosition = new Vector3(0f, poleH * 0.5f, 0f);
            SetLitColor(pole, new Color(0.30f, 0.22f, 0.15f), 0f, null);

            // 旗布（Quad）
            var flag = GameObject.CreatePrimitive(PrimitiveType.Quad);
            flag.name = "FlagCloth";
            DestroyCollider(flag);
            flag.transform.SetParent(parent, false);
            float flagW = 3.0f * inv, flagH = 1.8f * inv;
            flag.transform.localScale = new Vector3(flagW, flagH, 1f);
            flag.transform.localPosition = new Vector3(flagW * 0.5f, poleH - flagH * 0.6f, 0f);
            SetLitColor(flag, new Color(0.85f, 0.12f, 0.12f), 0f, new Color(0.5f, 0.05f, 0.05f));
            // 両面表示のため裏面も
            var back = GameObject.Instantiate(flag, flag.transform.parent);
            back.name = "FlagClothBack";
            back.transform.localPosition = flag.transform.localPosition;
            back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        private static void DestroyCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
        }

        private static void SetLitColor(GameObject go, Color baseCol, float smooth, Color? emission)
        {
            var mr = go.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", baseCol);
            mat.SetFloat("_Smoothness", smooth);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
            }
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
