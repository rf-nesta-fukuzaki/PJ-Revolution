using UnityEngine;

/// <summary>
/// サーマルケースによる衝撃ダメージ軽減バッファ。RelicBase と同じ GO にアタッチされる。
/// 梱包キット(<see cref="RelicPackingBuffer"/>)と同じく <see cref="IRelicDamageModifier"/> として
/// ダメージ「適用前」に乗算軽減を掛ける。
///
/// 旧実装(ThermalCaseItem の OnDamaged 購読＋事後 Repair)は、1 撃で HP が 0 になる致命傷を
/// 防げなかった（破壊後は TryRepair が弾かれる）。事前軽減に統一することで致命傷も
/// 軽減後の値で評価され、梱包キットと挙動が一貫する。
/// </summary>
[RequireComponent(typeof(RelicBase))]
public class RelicThermalBuffer : MonoBehaviour, IRelicDamageModifier
{
    private float _reductionFactor;

    /// <summary>軽減率 (0〜1)。0 で素通し（保護解除時）。</summary>
    public void SetReductionFactor(float factor)
    {
        _reductionFactor = Mathf.Clamp01(factor);
    }

    public float ModifyDamage(float baseDamage, Collision collision, RelicBase relic)
    {
        if (baseDamage <= 0f) return 0f;
        return baseDamage * (1f - _reductionFactor);
    }
}
