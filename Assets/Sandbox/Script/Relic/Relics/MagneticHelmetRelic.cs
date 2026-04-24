using System.Collections.Generic;
using UnityEngine;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

/// <summary>
/// GDD §6.2 — 遺物⑧「磁力の兜」
/// 物理軸：装備引き寄せ。近くの金属アイテムを引き寄せる。
/// 「俺のピッケルが！」「お前近づくな！」
/// 難易度：★★★  壊れやすさ：低
/// </summary>
public class MagneticHelmetRelic : RelicBase
{
    [Header("磁力設定 (GDD §6.2)")]
    [Tooltip("フル引力圏。この半径内では最大トルク (_magnetForce) が適用される。")]
    [SerializeField] private float _magnetRadius    = 6f;
    [SerializeField] private float _magnetForce     = 25f;
    [Tooltip("絶対到達距離。_magnetRadius → _maxPullDistance の間で線形に力が弱まり、これ以遠は無効化。")]
    [SerializeField] private float _maxPullDistance = 10f;

    private readonly List<Rigidbody> _affectedItems = new();

    // GDD §15.2 — relic_magnet_pull のエッジトリガー用（引き寄せ開始の瞬間だけ鳴らす）
    private bool _wasAttracting;

    protected override void Awake()
    {
        _relicName        = "磁力の兜";
        _baseValue        = 160;
        _maxHp            = 120f;   // 高密度で壊れにくい
        _damageMultiplier = 0.6f;
        _impactThreshold  = 4f;

        base.Awake();

        _rb.mass = 5f;  // 高密度・重い
    }

    private void FixedUpdate()
    {
        if (_isDestroyed) return;
        AttractNearbyMetal();
    }

    private void AttractNearbyMetal()
    {
        _affectedItems.Clear();

        // _maxPullDistance が _magnetRadius 以下だと二層カーブが退化するため、
        // Inspector での誤設定に対するガード（最低でも等価）。
        float outer = Mathf.Max(_maxPullDistance, _magnetRadius);

        // MagneticTarget コンポーネントを持つ全 Rigidbody を引き寄せる
        foreach (var target in MagneticTarget.RegisteredTargets)
        {
            float dist = Vector3.Distance(transform.position, target.transform.position);
            if (dist > outer) continue;

            var rb = target.TargetRigidbody;
            if (rb == null) continue;

            _affectedItems.Add(rb);

            Vector3 dir = (transform.position - target.transform.position).normalized;

            // _magnetRadius 内は最大力。外側は線形フォールオフ。
            float forceMag;
            if (dist <= _magnetRadius)
            {
                forceMag = _magnetForce;
            }
            else
            {
                float falloffSpan = outer - _magnetRadius;
                float t = falloffSpan > 0f
                    ? 1f - (dist - _magnetRadius) / falloffSpan
                    : 0f;
                forceMag = _magnetForce * Mathf.Clamp01(t);
            }

            rb.AddForce(dir * forceMag, ForceMode.Force);

            if (dist < 1f)
                Debug.Log($"[MagneticHelmet] 「俺の{target.ItemName}が！」");
        }

        // GDD §15.2 — relic_magnet_pull（引き寄せ開始のエッジトリガー）
        bool nowAttracting = _affectedItems.Count > 0;
        if (nowAttracting && !_wasAttracting)
            PPAudioManager.Instance?.PlaySE(SoundId.RelicMagnetPull, transform.position);
        _wasAttracting = nowAttracting;
    }

    protected override Color GizmoColor => new Color(0.42f, 0.49f, 0.42f);

    protected override void BuildVisual()
    {
        var metal = new Color(0.42f, 0.49f, 0.42f);
        var dark  = new Color(0.25f, 0.30f, 0.25f);
        var horn  = new Color(0.55f, 0.49f, 0.34f);

        // ドーム
        VizChild(PrimitiveType.Sphere, "dome",
            new Vector3(0f, 0.18f, 0f), new Vector3(1.0f, 0.82f, 1.0f),
            metal, metallic: 0.75f, smoothness: 0.65f);
        // ブリム
        VizChild(PrimitiveType.Cylinder, "brim",
            new Vector3(0f, -0.12f, 0f), new Vector3(1.35f, 0.14f, 1.35f),
            dark, metallic: 0.70f, smoothness: 0.6f);
        // バイザー
        VizChild(PrimitiveType.Cube, "visor",
            new Vector3(0f, 0.04f, 0.54f), new Vector3(0.65f, 0.32f, 0.12f),
            dark, metallic: 0.60f, smoothness: 0.4f);
        // 角 L
        VizChildRot(PrimitiveType.Cylinder, "horn_L",
            new Vector3(-0.47f, 0.42f, 0f),
            Quaternion.Euler(0f, 0f, -48f),
            new Vector3(0.12f, 0.55f, 0.12f),
            horn, metallic: 0.1f, smoothness: 0.3f);
        // 角 R
        VizChildRot(PrimitiveType.Cylinder, "horn_R",
            new Vector3(0.47f, 0.42f, 0f),
            Quaternion.Euler(0f, 0f, 48f),
            new Vector3(0.12f, 0.55f, 0.12f),
            horn, metallic: 0.1f, smoothness: 0.3f);
    }

    protected override void OnBroken()
    {
        base.OnBroken();
        Debug.Log("[MagneticHelmet] 磁力が消えた。装備が解放された。");
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        // 内側=フル引力圏、外側=フォールオフ終端
        Gizmos.color = new Color(0.5f, 0f, 1f, 0.20f);
        Gizmos.DrawSphere(transform.position, _magnetRadius);
        Gizmos.color = new Color(0.5f, 0f, 1f, 0.08f);
        Gizmos.DrawSphere(transform.position, Mathf.Max(_maxPullDistance, _magnetRadius));
    }
}
