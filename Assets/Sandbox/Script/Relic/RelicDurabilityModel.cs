using System;

/// <summary>
/// 遺物耐久 HP の純粋 C# ドメインモデル。
/// MonoBehaviour (RelicBase) から耐久ロジックを分離し、テスト可能にする。
/// </summary>
public sealed class RelicDurabilityModel
{
    private readonly float _maxHp;
    private readonly float _impactThreshold;
    private readonly float _damageMultiplier;

    public RelicDurabilityModel(float maxHp, float impactThreshold, float damageMultiplier)
    {
        Contract.Requires(maxHp > 0f, "RelicDurabilityModel: maxHp は正の値でなければならない");
        Contract.Requires(impactThreshold >= 0f, "RelicDurabilityModel: impactThreshold は 0 以上");
        Contract.Requires(damageMultiplier >= 0f, "RelicDurabilityModel: damageMultiplier は 0 以上");

        _maxHp = maxHp;
        _impactThreshold = impactThreshold;
        _damageMultiplier = damageMultiplier;
        CurrentHp = maxHp;
    }

    public float MaxHp => _maxHp;
    public float ImpactThreshold => _impactThreshold;
    public float DamageMultiplier => _damageMultiplier;
    public float CurrentHp { get; private set; }
    public bool IsDestroyed => CurrentHp <= 0f;
    public float HpPercent => _maxHp > 0f ? CurrentHp / _maxHp * 100f : 0f;

    public RelicCondition Condition => EvaluateCondition(CurrentHp, _maxHp);

    public bool TryApplyImpact(float impactSpeed, out float damageApplied)
    {
        damageApplied = 0f;

        if (IsDestroyed) return false;
        if (impactSpeed < _impactThreshold) return false;

        float excessSpeed = impactSpeed - _impactThreshold;
        damageApplied = excessSpeed * _damageMultiplier * 5f;
        return TryApplyDamage(damageApplied, out _);
    }

    public bool TryApplyDamage(float damage, out float appliedDamage)
    {
        appliedDamage = 0f;

        if (!Contract.TryRequires(damage >= 0f, "RelicDurabilityModel.TryApplyDamage: damage は 0 以上"))
            return false;

        if (IsDestroyed || damage <= 0f) return false;

        appliedDamage = damage;
        CurrentHp = Math.Max(0f, CurrentHp - damage);

        Contract.Ensures(CurrentHp >= 0f && CurrentHp <= _maxHp,
            $"RelicDurabilityModel: HP が範囲外 ({CurrentHp})");

        return true;
    }

    public bool TryRepair(float amount)
    {
        if (!Contract.TryRequires(amount >= 0f, "RelicDurabilityModel.TryRepair: amount は 0 以上"))
            return false;

        if (IsDestroyed || amount <= 0f) return false;

        CurrentHp = Math.Min(_maxHp, CurrentHp + amount);

        Contract.Ensures(CurrentHp <= _maxHp,
            $"RelicDurabilityModel.TryRepair: HP が MaxHp を超えました ({CurrentHp})");

        return true;
    }

    public static RelicCondition EvaluateCondition(float currentHp, float maxHp)
    {
        float percent = maxHp > 0f ? currentHp / maxHp * 100f : 0f;
        if (percent >= 100f) return RelicCondition.Perfect;
        if (percent >= 50f)  return RelicCondition.Damaged;
        if (percent >= 1f)   return RelicCondition.HeavilyDamaged;
        return RelicCondition.Destroyed;
    }
}
