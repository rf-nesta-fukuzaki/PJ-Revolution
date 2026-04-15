using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GDD §6.2 — 遺物⑧「磁力の兜」
/// 物理軸：装備引き寄せ。近くの金属アイテムを引き寄せる。
/// 「俺のピッケルが！」「お前近づくな！」
/// 難易度：★★★  壊れやすさ：低
/// </summary>
public class MagneticHelmetRelic : RelicBase
{
    [Header("磁力設定")]
    [SerializeField] private float _magnetRadius    = 6f;
    [SerializeField] private float _magnetForce     = 25f;
#pragma warning disable CS0414
    [SerializeField] private float _maxPullDistance = 10f;    // 将来の距離制限実装用
#pragma warning restore CS0414

    private readonly List<Rigidbody> _affectedItems = new();

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

        // MagneticTarget コンポーネントを持つ全 Rigidbody を引き寄せる
        foreach (var target in MagneticTarget.RegisteredTargets)
        {
            float dist = Vector3.Distance(transform.position, target.transform.position);
            if (dist > _magnetRadius) continue;

            var rb = target.TargetRigidbody;
            if (rb == null) continue;

            _affectedItems.Add(rb);

            Vector3 dir        = (transform.position - target.transform.position).normalized;
            float   forceMag   = _magnetForce * (1f - dist / _magnetRadius);
            rb.AddForce(dir * forceMag, ForceMode.Force);

            if (dist < 1f)
                Debug.Log($"[MagneticHelmet] 「俺の{target.ItemName}が！」");
        }
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
        Gizmos.color = new Color(0.5f, 0f, 1f, 0.15f);
        Gizmos.DrawSphere(transform.position, _magnetRadius);
    }
}
