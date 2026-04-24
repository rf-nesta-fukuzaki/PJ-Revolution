using System;
using UnityEngine;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

/// <summary>
/// GDD §5.1 — 全アイテムの基底クラス。
/// 耐久度制（使用・衝撃で減少、ゼロで消失）。
/// 全アイテムが物理オブジェクト（落とす・壊す・投げるが可能）。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public abstract class ItemBase : MonoBehaviour
{
    [Header("データ駆動（任意）")]
    [SerializeField] private ItemDefinitionSO _definition;   // アサインすれば下記フィールドを上書き

    [Header("アイテム情報")]
    [SerializeField] protected string _itemName  = "Unknown Item";
    [SerializeField] protected int    _cost      = 5;
    [SerializeField] protected float  _weight    = 1f;
    [SerializeField] protected int    _slots     = 1;

    [Header("耐久")]
    [SerializeField] protected float _maxDurability     = 100f;
    [SerializeField] protected float _impactDmgScale    = 1f;
    [SerializeField] protected float _impactDmgThreshold = 3f;

    protected float            _currentDurability;
    protected bool             _isBroken;
    protected PlayerInventory  _owner;
    protected Rigidbody        _rb;

    public string ItemName        => _itemName;
    public int    Cost            => _cost;
    public float  Weight          => _weight;
    public int    Slots           => _slots;
    public float  DurabilityPct   => _currentDurability / _maxDurability;
    public bool   IsBroken        => _isBroken;

    public event Action<ItemBase> OnBroken;

    protected virtual void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        // SO がアサインされていれば派生クラスで事前に設定されたフィールドを上書きする。
        // 派生クラスが Awake でフィールドを設定する場合は base.Awake() より前に行うこと。
        if (_definition != null)
            ApplyDefinition(_definition);

        _currentDurability = _maxDurability;
    }

    /// <summary>ItemDefinitionSO の値を自身のフィールドに適用する。</summary>
    protected void ApplyDefinition(ItemDefinitionSO def)
    {
        _itemName            = def.ItemName;
        _cost                = def.Cost;
        _weight              = def.Weight;
        _slots               = def.Slots;
        _maxDurability       = def.MaxDurability;
        _impactDmgScale      = def.ImpactDamageScale;
        _impactDmgThreshold  = def.ImpactDamageThreshold;
    }

    // ── インベントリ連携 ─────────────────────────────────────
    public virtual void OnStoredInInventory(PlayerInventory inv)
    {
        _owner            = inv;
        _rb.isKinematic   = true;
        transform.SetParent(inv.transform);
    }

    public virtual void OnRemovedFromInventory()
    {
        _owner          = null;
        _rb.isKinematic = false;
        transform.SetParent(null);
    }

    // ── 使用 ─────────────────────────────────────────────────
    public virtual bool TryUse()
    {
        if (_isBroken) return false;
        ConsumeDurability(GetUseDurabilityDrain());
        return true;
    }

    protected virtual float GetUseDurabilityDrain() => 5f;

    // ── 耐久 ─────────────────────────────────────────────────
    public void ConsumeDurability(float amount)
    {
        if (_isBroken || amount <= 0f) return;
        _currentDurability = Mathf.Max(0f, _currentDurability - amount);

        if (_currentDurability <= 0f)
            Break();
    }

    protected void Break()
    {
        if (_isBroken) return;
        _isBroken = true;
        OnBroken?.Invoke(this);

        // GDD §15.2 — item_break
        PPAudioManager.Instance?.PlaySE(SoundId.ItemBreak, transform.position);

        OnItemBroken();
        Debug.Log($"[Item] {_itemName} が壊れた");
    }

    protected virtual void OnItemBroken()
    {
        // 壊れた演出は派生クラスで実装
        Destroy(gameObject, 2f);
    }

    // ── 衝撃ダメージ ─────────────────────────────────────────
    private void OnCollisionEnter(Collision col)
    {
        float speed = col.relativeVelocity.magnitude;
        if (speed < _impactDmgThreshold) return;

        // GDD §15.2 — item_impact（閾値超え衝突で鳴らす）
        PPAudioManager.Instance?.PlaySE(SoundId.ItemImpact, transform.position);

        float dmg = (speed - _impactDmgThreshold) * _impactDmgScale;
        ConsumeDurability(dmg);
    }

    // ── 磁性タグ（MagneticHelmet に引き寄せられる） ──────────
    /// <summary>金属製アイテムは MagneticTarget コンポーネントを追加すること。</summary>
}
