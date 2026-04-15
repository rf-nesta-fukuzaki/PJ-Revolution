using UnityEngine;

/// <summary>
/// GDD §5.2 — アイテム「サーマルケース」
/// 温度変化に弱い遺物を保護する保温ケース。
/// コスト 4pt / 重量 1 / スロット 1 / 耐久 90
/// </summary>
public class ThermalCaseItem : ItemBase
{
    [Header("保護設定")]
    [SerializeField] private float _damageReductionRate = 0.5f;  // ダメージ50%軽減
#pragma warning disable CS0414
    [SerializeField] private bool  _protectsTemperature = true;  // 将来の天候システム連携用
#pragma warning restore CS0414

    private RelicBase _protectedRelic;
    private bool      _isProtecting;

    public bool IsProtecting => _isProtecting;

    protected override void Awake()
    {
        base.Awake();
        _itemName          = "サーマルケース";
        _cost              = 4;
        _weight            = 1f;
        _slots             = 1;
        _maxDurability     = 90f;
        _currentDurability = _maxDurability;
        _impactDmgScale    = 0.3f;
    }

    /// <summary>遺物をサーマルケースで保護する。</summary>
    public bool TryProtectRelic(RelicBase relic)
    {
        if (_isBroken || _isProtecting || relic == null) return false;

        _protectedRelic = relic;
        _isProtecting   = true;

        // 遺物のダメージを軽減するためにイベントに介入
        relic.OnDamaged += OnRelicDamaged;
        Debug.Log($"[ThermalCase] {relic.RelicName} を保護開始");
        return true;
    }

    /// <summary>保護を解除する。</summary>
    public void StopProtecting()
    {
        if (_protectedRelic != null)
            _protectedRelic.OnDamaged -= OnRelicDamaged;

        _protectedRelic = null;
        _isProtecting   = false;
        Debug.Log("[ThermalCase] 保護解除");
    }

    private void OnRelicDamaged(float damage, float currentHp)
    {
        // ダメージ軽減をイベント後に補正（既適用分を回復で近似）
        if (_protectedRelic == null || _isBroken) return;

        float reduced = damage * _damageReductionRate;
        if (reduced > 0f)
            _protectedRelic.Repair(reduced);   // 適用済みダメージの一部を回復で相殺

        ConsumeDurability(1f);
        Debug.Log($"[ThermalCase] ダメージ軽減: {reduced:F1}");
    }

    protected override void OnItemBroken()
    {
        StopProtecting();
        base.OnItemBroken();
    }
}
