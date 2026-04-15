using UnityEngine;

/// <summary>
/// GDD §6.2 — 遺物②「古代クリスタルの杯」
/// 物理軸：超壊れやすい。少しの衝撃でヒビ。完品で超高額。
/// 難易度：★★☆  壊れやすさ：極高
/// </summary>
public class CrystalCupRelic : RelicBase
{
    [Header("クリスタル設定")]
    [SerializeField] private float _windKnockForce = 0.8f;   // 風で手から飛びそうになる力

    [Header("ヒビ演出")]
    [SerializeField] private Material _crackedMaterial;      // ヒビ入りマテリアル（任意）
    private Renderer[] _renderers;
    private bool       _crackApplied;

    protected override void Awake()
    {
        _relicName        = "古代クリスタルの杯";
        _baseValue        = 200;    // 完品で超高額
        _maxHp            = 100f;
        _damageMultiplier = 8f;     // 極めて壊れやすい
        _impactThreshold  = 0.3f;   // わずかな衝撃でダメージ

        base.Awake();

        _renderers = GetComponentsInChildren<Renderer>();
    }

    protected override float CalculateDamage(float impactSpeed, Collision collision)
    {
        // 杯は加速度的にダメージが増える
        float excess = impactSpeed - _impactThreshold;
        return excess * excess * _damageMultiplier;
    }

    protected override void OnDamageReceived(float damage, GameObject source)
    {
        // HP 50% を下回ったらヒビマテリアルに切り替え
        if (!_crackApplied && HpPercent < 50f && _crackedMaterial != null)
        {
            _crackApplied = true;
            foreach (var r in _renderers)
                r.sharedMaterial = _crackedMaterial;
        }

        if (damage > 5f)
            Debug.Log("[CrystalCup] 「息するのも怖い」");
    }

    /// <summary>
    /// WindSystem から呼ばれる。保持中プレイヤーの手から滑り落ちる可能性。
    /// </summary>
    public void ApplyWindKnock(Vector3 windDir)
    {
        if (!_isHeld) return;
        _rb.AddForce(windDir * _windKnockForce, ForceMode.Impulse);
    }

    protected override void OnBroken()
    {
        base.OnBroken();
        Debug.Log("[CrystalCup] 「あっ…（パリーン）」");

        // 破片演出（実装省略：Particle System を生成する）
        SpawnShatterParticles();
    }

    private void SpawnShatterParticles()
    {
        // Particle System が未アサインの場合はスキップ
    }

    protected override Color GizmoColor => new Color(0.55f, 0.87f, 0.83f);

    protected override void BuildVisual()
    {
        var ice = new Color(0.60f, 0.88f, 0.88f);

        // ベース
        VizChild(PrimitiveType.Cylinder, "base",
            new Vector3(0f, -0.55f, 0f), new Vector3(1.1f, 0.12f, 1.1f),
            ice, metallic: 0f, smoothness: 0.9f);
        // ステム
        VizChild(PrimitiveType.Cylinder, "stem",
            new Vector3(0f, -0.2f, 0f), new Vector3(0.20f, 0.55f, 0.20f),
            ice, metallic: 0f, smoothness: 0.9f);
        // カップ本体
        VizChild(PrimitiveType.Cylinder, "cup",
            new Vector3(0f, 0.38f, 0f), new Vector3(1.2f, 0.42f, 1.2f),
            ice, metallic: 0f, smoothness: 0.9f);
        // リム
        VizChild(PrimitiveType.Cylinder, "rim",
            new Vector3(0f, 0.65f, 0f), new Vector3(1.4f, 0.10f, 1.4f),
            ice, metallic: 0f, smoothness: 0.9f);
    }
}
