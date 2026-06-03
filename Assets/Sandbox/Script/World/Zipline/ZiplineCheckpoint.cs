using UnityEngine;
using PeakPlunder.Audio;
using K = Sandbox.World.Integration.BasecampPropKit;

namespace Sandbox.World.Zipline
{
    /// <summary>
    /// ルート沿いに配置されるジップライン用チェックポイント。プレイヤーが初到達すると、拠点⇄当該地点の
    /// <see cref="Zipline"/> を <see cref="ZiplineNetwork"/> 経由で設置し、以後は拠点から登りを省略できる。
    /// 到達前は淡色で点滅、到達後は彩度を上げて点灯し「開通済み」を示す。
    /// 既存 HUD 連携のため <see cref="ExpeditionEvents"/> にもチェックポイント到達を流す。
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class ZiplineCheckpoint : MonoBehaviour
    {
        [SerializeField] private int _index;
        [SerializeField] private int _total = 3;
        [SerializeField] private float _triggerRadius = 4.5f;
        [SerializeField] private Color _color = new Color(0.35f, 0.80f, 1.0f);

        private bool _reached;
        private Material _beaconMat;
        private Light _beaconLight;
        private float _pulse;

        /// <summary>チェックポイント番号（0 始まり）。</summary>
        public int Index => _index;
        /// <summary>到達済み（ジップライン開通済み）か。</summary>
        public bool IsReached => _reached;

        /// <summary>外部（配置側）から識別子・演出色・トリガー半径を設定する。</summary>
        public void Configure(int index, int total, Color color, float triggerRadius = -1f)
        {
            _index = index;
            _total = Mathf.Max(1, total);
            _color = color;
            if (triggerRadius > 0f)
            {
                _triggerRadius = triggerRadius;
                var col = GetComponent<SphereCollider>();
                if (col != null) col.radius = _triggerRadius;
            }
            ApplyColor(reached: false);
        }

        private bool _beaconBuilt;

        private void Awake()
        {
            var col = GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = _triggerRadius;
            TrySetTag("Checkpoint");
        }

        private void Start()
        {
            // Configure（色・識別子）後にビルドして、配置側の指定色を確実に反映する。
            if (!_beaconBuilt) BuildBeacon();
        }

        private void Update()
        {
            // 到達前は弱く脈動して「目印」感を出す。到達後は安定点灯。
            _pulse += Time.deltaTime * (_reached ? 1.5f : 3.0f);
            float k = _reached ? 1f : (0.55f + 0.45f * Mathf.Abs(Mathf.Sin(_pulse)));
            if (_beaconLight != null) _beaconLight.intensity = (_reached ? 3.2f : 1.6f) * k;
            if (_beaconMat != null) _beaconMat.SetColor("_EmissionColor", _color * (_reached ? 2.4f : 1.3f) * k);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_reached) return;
            if (!other.CompareTag("Player")) return;
            Reach();
        }

        /// <summary>到達扱いにして拠点へのジップラインを開通させる（デバッグ強制解放にも使用・冪等）。</summary>
        public void ForceReach()
        {
            if (_reached) return;
            Reach();
        }

        private void Reach()
        {
            _reached = true;
            ApplyColor(reached: true);

            // 到達通知（HUD の CP 進捗更新）を先に出し、その後にジップライン開通通知で上書きする。
            ExpeditionEvents.RaiseCheckpointReached(_index + 1, _total);
            GameServices.Audio?.PlaySE(SoundId.Checkpoint, transform.position);

            var net = ZiplineNetwork.Ensure();
            net.InstallLine(_index, transform.position);

            Debug.Log($"[Zipline] チェックポイント {_index + 1}/{_total} 到達 → 拠点へのジップラインが開通");
        }

        // ── ビーコン（旗竿＋発光オーブ＋足元リング） ────────────────
        private void BuildBeacon()
        {
            _beaconBuilt = true;
            _beaconMat = MakeBeaconMat(); // オーブとリングで共有し、点滅を同期させる。

            K.Cyl("BeaconPole", transform, new Vector3(0f, 1.6f, 0f), 0.1f, 3.2f, K.Mat(new Color(0.22f, 0.23f, 0.25f), 0.3f, 0.5f));
            K.Cyl("BeaconOrb", transform, new Vector3(0f, 3.3f, 0f), 0.32f, 0.64f, _beaconMat, solid: false);
            // 足元の発光リング（範囲の可視化）。
            K.Cyl("BeaconRing", transform, new Vector3(0f, 0.06f, 0f), _triggerRadius, 0.06f, _beaconMat, solid: false);

            _beaconLight = K.PointLight("BeaconLight", transform, new Vector3(0f, 3.3f, 0f), _color, 12f, 2f);
            _beaconLight.shadows = LightShadows.None;
        }

        private void OnDestroy()
        {
            if (_beaconMat != null) Destroy(_beaconMat);
        }

        private Material MakeBeaconMat()
        {
            // チェックポイントごとに独立した発光マテリアル（点滅で個別に色を動かす）。
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader) { name = $"ZiplineBeacon_{_index}" };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", _color);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", _color * 1.3f);
            return m;
        }

        private void ApplyColor(bool reached)
        {
            if (_beaconMat != null)
                _beaconMat.SetColor("_EmissionColor", _color * (reached ? 2.4f : 1.3f));
        }

        private void TrySetTag(string tagName)
        {
            try { gameObject.tag = tagName; }
            catch (UnityException)
            {
                Debug.LogWarning($"[Zipline] Tag '{tagName}' 未定義。Project Settings で追加してください。");
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(_color.r, _color.g, _color.b, 0.3f);
            Gizmos.DrawSphere(transform.position, _triggerRadius);
        }
    }
}
