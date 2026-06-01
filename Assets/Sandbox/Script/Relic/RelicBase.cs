using System;
using System.Collections.Generic;
using UnityEngine;
using PeakPlunder.Audio;

/// <summary>
/// GDD §6.1 — 全遺物の基底クラス。
/// 耐久: <see cref="RelicDurabilityModel"/> / ビジュアル: <see cref="RelicVisualizer"/>
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(RelicVisualizer))]
public abstract class RelicBase : MonoBehaviour
{
    [Header("データ駆動（任意）")]
    [SerializeField] private RelicDefinitionSO _definition;

    [Header("遺物設定")]
    [SerializeField] protected string _relicName  = "Unknown Relic";
    [SerializeField] protected int    _baseValue  = 100;

    [Header("耐久")]
    [SerializeField, Range(0f, 100f)] protected float _maxHp          = 100f;
    [SerializeField]                  protected float _impactThreshold = 2f;
    [SerializeField]                  protected float _damageMultiplier = 1f;

    [Header("物理")]
    [SerializeField] protected bool _isHeld = false;

    private RelicDurabilityModel _durability;
    private RelicVisualizer _visualizer;
    protected Rigidbody _rb;
    protected bool _isDestroyed;
    private RelicCondition _lastCondition;
    private readonly List<MonoBehaviour> _behaviourBuffer = new();

    public event Action<float, float> OnDamaged;
    public event Action<RelicBase> OnRelicBroken;
    public event Action<RelicCondition, RelicCondition> OnConditionChanged;

    protected RelicDurabilityModel Durability => _durability;
    protected RelicVisualizer Visualizer => _visualizer;

    public string RelicName => _relicName;
    public float CurrentHp => _durability?.CurrentHp ?? 0f;
    public float MaxHp => _durability?.MaxHp ?? _maxHp;
    public float HpPercent => _durability?.HpPercent ?? 0f;
    public bool IsDestroyed => _isDestroyed;
    public bool IsHeld => _isHeld;
    public RelicCondition Condition => _durability?.Condition ?? RelicCondition.Destroyed;

    public float RewardMultiplier => Condition switch
    {
        RelicCondition.Perfect        => 1.0f,
        RelicCondition.Damaged        => 0.6f,
        RelicCondition.HeavilyDamaged => 0.2f,
        _                             => 0.0f
    };

    public int CurrentValue => Mathf.RoundToInt(_baseValue * RewardMultiplier);

    protected virtual void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _visualizer = GetComponent<RelicVisualizer>();

        if (_definition != null)
            ApplyDefinition(_definition);

        Contract.Invariant(_maxHp > 0f,
            $"RelicBase.Awake: _maxHp は 0 より大きくなければなりません (value={_maxHp}, relic={_relicName})");

        _durability = new RelicDurabilityModel(_maxHp, _impactThreshold, _damageMultiplier);
        _lastCondition = _durability.Condition;
        RebuildVisual();

        if (GetComponent<RelicDiscoveryTrigger>() == null)
            gameObject.AddComponent<RelicDiscoveryTrigger>();

        // RelicCarrier も自動付与する。OnPickedUp/OnPutDown の仲介・運搬役で、PlayerInteraction/NPCController/
        // RelicDamageTracker/TempleTraps など多くの系が relic 上の GetComponent<RelicCarrier> を前提にしている。
        // 従来どのプレハブ/シーンにも付いておらず拾い上げが成立しなかった（＝pickup SE/VFX が発火しない）配線漏れを解消。
        // [RequireComponent(typeof(RelicBase))] で安全、保持中以外は FixedUpdate が即 return で不活性。
        if (GetComponent<RelicCarrier>() == null)
            gameObject.AddComponent<RelicCarrier>();
    }

    protected void ApplyDefinition(RelicDefinitionSO def)
    {
        _relicName        = def.RelicName;
        _baseValue        = def.BaseValue;
        _maxHp            = def.MaxHp;
        _impactThreshold  = def.ImpactThreshold;
        _damageMultiplier = def.DamageMultiplier;
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (_isDestroyed) return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed < _impactThreshold) return;

        float damage = CalculateDamage(impactSpeed, collision);
        damage = ApplyDamageModifiers(damage, collision);
        if (damage <= 0f) return;
        ApplyDamage(damage, collision.gameObject);
    }

    protected virtual float CalculateDamage(float impactSpeed, Collision collision)
    {
        float excessSpeed = impactSpeed - _impactThreshold;
        return excessSpeed * _damageMultiplier * 5f;
    }

    public void ApplyDamage(float damage, GameObject source = null)
    {
        if (_isDestroyed) return;
        if (!_durability.TryApplyDamage(damage, out float applied)) return;

        OnDamaged?.Invoke(applied, _durability.CurrentHp);
        NotifyConditionChanged();
        OnDamageReceived(applied, source);

        if (_durability.IsDestroyed && !_isDestroyed)
            HandleDestruction();
    }

    public void ApplyEnvironmentalDamage(float damage) => ApplyDamage(damage, null);

    public void Repair(float amount)
    {
        if (_isDestroyed) return;
        _durability.TryRepair(amount);
    }

    protected virtual void OnDamageReceived(float damage, GameObject source) { }

    private void NotifyConditionChanged()
    {
        var newCondition = Condition;
        if (newCondition == _lastCondition) return;

        var prev = _lastCondition;
        _lastCondition = newCondition;

        Contract.Ensures(newCondition > prev || newCondition == RelicCondition.Destroyed,
            $"RelicBase: 状態は悪化方向にしか変化しません ({prev} → {newCondition})");

        OnConditionChanged?.Invoke(prev, newCondition);

        if (newCondition == RelicCondition.Damaged)
            GameServices.Audio?.PlaySE(SoundId.RelicDamageLight, transform.position);
        else if (newCondition == RelicCondition.HeavilyDamaged)
            GameServices.Audio?.PlaySE(SoundId.RelicDamageHeavy, transform.position);
    }

    private float ApplyDamageModifiers(float baseDamage, Collision collision)
    {
        if (baseDamage <= 0f) return 0f;

        _behaviourBuffer.Clear();
        GetComponents(_behaviourBuffer);

        float modifiedDamage = baseDamage;
        foreach (var behaviour in _behaviourBuffer)
        {
            if (behaviour is not IRelicDamageModifier modifier) continue;
            modifiedDamage = modifier.ModifyDamage(modifiedDamage, collision, this);
            if (modifiedDamage <= 0f) return 0f;
        }

        return modifiedDamage;
    }

    private void HandleDestruction()
    {
        _isDestroyed = true;
        GameServices.Audio?.PlaySE(SoundId.RelicDestroyed, transform.position);
        OnRelicBroken?.Invoke(this);
        OnBroken();
    }

    protected virtual void OnBroken()
    {
        Debug.Log($"[Relic] {_relicName} が破壊されました");
    }

    public virtual void OnPickedUp(Transform holder)
    {
        _isHeld = true;
        _rb.isKinematic = false;
        GameServices.Audio?.PlaySE(SoundId.RelicGrab, transform.position);

        // 持ち上げのポンッ（金、小さめ速め）。
        Sandbox.World.Environment.StylizedImpactFx.CollectPop(
            transform.position, new Color(1f, 0.80f, 0.25f), 0.9f, 24);
    }

    public virtual void OnPutDown() => _isHeld = false;

    protected virtual Color GizmoColor => Color.white;

    protected virtual void OnDrawGizmos()
    {
        Color c = GizmoColor;
        Gizmos.color = new Color(c.r, c.g, c.b, 0.55f);
        Gizmos.DrawSphere(transform.position + Vector3.up * 0.6f, 0.09f);
#if UNITY_EDITOR
        var style = new UnityEngine.GUIStyle { fontSize = 9 };
        style.normal.textColor = c;
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.9f,
            string.IsNullOrEmpty(_relicName) ? gameObject.name : _relicName,
            style);
#endif
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Condition switch
        {
            RelicCondition.Perfect        => Color.green,
            RelicCondition.Damaged        => Color.yellow,
            RelicCondition.HeavilyDamaged => new Color(1f, 0.5f, 0f),
            _                             => Color.red
        };
        Gizmos.DrawWireSphere(transform.position, 0.35f);
    }

    protected virtual void BuildVisual() { }

    public void RebuildVisual() => _visualizer.Rebuild(BuildVisual);

    protected GameObject VizChild(
        PrimitiveType type, string label,
        Vector3 localPos, Vector3 localScale,
        Color color, float metallic = 0f, float smoothness = 0.5f)
        => _visualizer.CreatePrimitive(type, label, localPos, localScale, color, metallic, smoothness);

    protected GameObject VizChildRot(
        PrimitiveType type, string label,
        Vector3 localPos, Quaternion localRot, Vector3 localScale,
        Color color, float metallic = 0f, float smoothness = 0.5f)
        => _visualizer.CreatePrimitiveRot(type, label, localPos, localRot, localScale, color, metallic, smoothness);
}

public enum RelicCondition
{
    Perfect,
    Damaged,
    HeavilyDamaged,
    Destroyed
}

public interface IRelicDamageModifier
{
    float ModifyDamage(float baseDamage, Collision collision, RelicBase relic);
}
