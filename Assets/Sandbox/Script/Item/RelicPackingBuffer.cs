using UnityEngine;

/// <summary>
/// 梱包キットによるダメージ軽減バッファ。RelicBase と同じ GO にアタッチされる。
/// RelicBase のダメージ計算段階で軽減率を適用する。
/// </summary>
[RequireComponent(typeof(RelicBase))]
public class RelicPackingBuffer : MonoBehaviour, IRelicDamageModifier
{
    private float     _reductionFactor = 0.5f;

    public void SetReductionFactor(float factor)
    {
        _reductionFactor = Mathf.Clamp01(factor);
    }

    /// <summary>RelicBase.CalculateDamage の結果を軽減する。</summary>
    public float ModifyDamage(float baseDamage, Collision collision, RelicBase relic)
    {
        if (baseDamage <= 0f) return 0f;
        return baseDamage * (1f - _reductionFactor);
    }
}
