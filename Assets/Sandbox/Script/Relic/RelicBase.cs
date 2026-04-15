using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GDD §6.1 — 全遺物の基底クラス。
/// HP（状態）システム：衝撃でダメージ蓄積。
///   完品 100%  → 最高報酬
///   損傷  50-99% → 減額
///   大破   1-49% → 大幅減額
///   破壊     0%  → 価値ゼロ
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public abstract class RelicBase : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static Material s_vizSharedMaterial;

    // ── 定数 ────────────────────────────────────────────────
    private const float DAMAGE_PERFECT_MIN  = 100f;
    private const float DAMAGE_DAMAGED_MIN  =  50f;
    private const float DAMAGE_BROKEN_MIN   =   1f;

    // ── Inspector ───────────────────────────────────────────
    [Header("データ駆動（任意）")]
    [SerializeField] private RelicDefinitionSO _definition;   // アサインすれば下記フィールドを上書き

    [Header("遺物設定")]
    [SerializeField] protected string _relicName  = "Unknown Relic";
    [SerializeField] protected int    _baseValue  = 100;

    [Header("耐久")]
    [SerializeField, Range(0f, 100f)] protected float _maxHp          = 100f;
    [SerializeField]                  protected float _impactThreshold = 2f;   // m/s — これ未満の衝突はダメージ無視
    [SerializeField]                  protected float _damageMultiplier = 1f;  // 壊れやすさ係数

    [Header("物理")]
    [SerializeField] protected bool _isHeld = false;

    // ── 状態 ────────────────────────────────────────────────
    protected float     _currentHp;
    protected Rigidbody _rb;
    protected bool      _isDestroyed;
    private readonly List<MonoBehaviour> _behaviourBuffer = new();

    // ── イベント ────────────────────────────────────────────
    public event Action<float, float>  OnDamaged;    // (damage, currentHp)
    public event Action<RelicBase>     OnRelicBroken;

    // ── プロパティ ───────────────────────────────────────────
    public string RelicName    => _relicName;
    public float  CurrentHp   => _currentHp;
    public float  MaxHp       => _maxHp;
    public float  HpPercent   => _maxHp > 0f ? _currentHp / _maxHp * 100f : 0f;
    public bool   IsDestroyed => _isDestroyed;
    public bool   IsHeld      => _isHeld;

    public RelicCondition Condition
    {
        get
        {
            float p = HpPercent;
            if (p >= DAMAGE_PERFECT_MIN)  return RelicCondition.Perfect;
            if (p >= DAMAGE_DAMAGED_MIN)  return RelicCondition.Damaged;
            if (p >= DAMAGE_BROKEN_MIN)   return RelicCondition.HeavilyDamaged;
            return RelicCondition.Destroyed;
        }
    }

    /// <summary>状態に応じた報酬倍率。</summary>
    public float RewardMultiplier => Condition switch
    {
        RelicCondition.Perfect        => 1.0f,
        RelicCondition.Damaged        => 0.6f,
        RelicCondition.HeavilyDamaged => 0.2f,
        _                             => 0.0f
    };

    public int CurrentValue => Mathf.RoundToInt(_baseValue * RewardMultiplier);

    // ── ライフサイクル ────────────────────────────────────────
    protected virtual void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        // SO がアサインされていれば Inspector フィールドを上書きする。
        // 派生クラスが Awake でフィールドを設定する場合は base.Awake() より前に行うこと。
        if (_definition != null)
            ApplyDefinition(_definition);

        _currentHp = _maxHp;
        RebuildVisual();
    }

    /// <summary>RelicDefinitionSO の値を自身のフィールドに適用する。</summary>
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

    // ── ダメージ計算 ──────────────────────────────────────────
    /// <summary>衝撃速度からダメージを計算する。派生クラスでオーバーライド可。</summary>
    protected virtual float CalculateDamage(float impactSpeed, Collision collision)
    {
        float excessSpeed = impactSpeed - _impactThreshold;
        return excessSpeed * _damageMultiplier * 5f;
    }

    public void ApplyDamage(float damage, GameObject source = null)
    {
        if (_isDestroyed || damage <= 0f) return;

        _currentHp = Mathf.Max(0f, _currentHp - damage);
        OnDamaged?.Invoke(damage, _currentHp);

        OnDamageReceived(damage, source);

        if (_currentHp <= 0f && !_isDestroyed)
            HandleDestruction();
    }

    /// <summary>遺物を修復する（HP を増加させる）。ThermalCase など保護アイテムが使用する。</summary>
    public void Repair(float amount)
    {
        if (_isDestroyed || amount <= 0f) return;
        _currentHp = Mathf.Min(_maxHp, _currentHp + amount);
    }

    protected virtual void OnDamageReceived(float damage, GameObject source) { }

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
        OnRelicBroken?.Invoke(this);
        OnBroken();
    }

    protected virtual void OnBroken()
    {
        // 派生クラスで演出をオーバーライド
        Debug.Log($"[Relic] {_relicName} が破壊されました");
    }

    // ── 掴み操作 ─────────────────────────────────────────────
    public virtual void OnPickedUp(Transform holder)
    {
        _isHeld = true;
        _rb.isKinematic = false;
    }

    public virtual void OnPutDown()
    {
        _isHeld = false;
    }

    // ── Gizmos ───────────────────────────────────────────────
    /// <summary>派生クラスで固有のGizmoカラーを返す。</summary>
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

    // ── ビジュアル構築 ───────────────────────────────────────
    private const string VIZ_PREFIX = "RelicViz_";

    /// <summary>派生クラスで固有の外観を構築する。Awake から自動呼び出し。</summary>
    protected virtual void BuildVisual() { }

    /// <summary>既存ビジュアル子を削除して BuildVisual を再実行する。</summary>
    public void RebuildVisual()
    {
        ClearVisualChildren();
        var mr = GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;
        BuildVisual();
    }

    private void ClearVisualChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i);
            if (!c.name.StartsWith(VIZ_PREFIX)) continue;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(c.gameObject); else
#endif
            Destroy(c.gameObject);
        }
    }

    /// <summary>LocalSpace にプリミティブ子オブジェクトを追加して返す。</summary>
    protected GameObject VizChild(
        PrimitiveType type, string label,
        Vector3 localPos, Vector3 localScale,
        Color color, float metallic = 0f, float smoothness = 0.5f)
        => VizChildRot(type, label, localPos, Quaternion.identity, localScale, color, metallic, smoothness);

    protected GameObject VizChildRot(
        PrimitiveType type, string label,
        Vector3 localPos, Quaternion localRot, Vector3 localScale,
        Color color, float metallic = 0f, float smoothness = 0.5f)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = VIZ_PREFIX + label;
        go.transform.SetParent(transform);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale    = localScale;

        var col = go.GetComponent<Collider>();
        if (col != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(col); else
#endif
            Destroy(col);
        }

        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            var sharedMaterial = GetSharedVizMaterial();
            if (sharedMaterial != null)
                renderer.sharedMaterial = sharedMaterial;

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            block.SetFloat(MetallicId, metallic);
            block.SetFloat(SmoothnessId, smoothness);
            renderer.SetPropertyBlock(block);
        }

        return go;
    }

    private static Material GetSharedVizMaterial()
    {
        if (s_vizSharedMaterial != null) return s_vizSharedMaterial;

        var shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard");
        if (shader == null) return null;

        s_vizSharedMaterial = new Material(shader)
        {
            name = "RelicVizSharedMaterial"
        };
        return s_vizSharedMaterial;
    }
}

/// <summary>遺物の状態区分。</summary>
public enum RelicCondition
{
    Perfect,
    Damaged,
    HeavilyDamaged,
    Destroyed
}

/// <summary>
/// RelicBase の衝突ダメージを計算段階で修飾する拡張ポイント。
/// </summary>
public interface IRelicDamageModifier
{
    float ModifyDamage(float baseDamage, Collision collision, RelicBase relic);
}
