using UnityEngine;

/// <summary>
/// GDD §5.2 — アイテム「酸素タンク」
/// 高山病防止（2000m以上で必要）。
/// コスト 12pt / 重量 2 / スロット 1 / 耐久 60
/// </summary>
public class OxygenTankItem : ItemBase
{
    [Header("酸素設定")]
    // GDD §8.3 — 1/10秒 = 0.1/秒。耐久60 → 約600秒(=約10分)で空。
    [SerializeField] private float _oxygenPerSecond = 0.1f;   // 毎秒消費量

    private StaminaSystem           _stamina;
    private AltitudeSicknessEffect  _altitudeSickness;

    protected override void Awake()
    {
        base.Awake();
        _itemName          = "酸素タンク";
        _cost              = 12;
        _weight            = 2f;
        _slots             = 1;
        _maxDurability     = 60f;
        _currentDurability = _maxDurability;
    }

    public override void OnEquippedInHand(PlayerInventory inv, Transform anchor, Vector3 localOffset)
    {
        base.OnEquippedInHand(inv, anchor, localOffset);
        ActivateOxygen(inv);
    }

    public override void OnStoredInInventory(PlayerInventory inv)
    {
        base.OnStoredInInventory(inv);
        ActivateOxygen(inv);
    }

    public override void OnRemovedFromInventory()
    {
        DeactivateOxygen();
        base.OnRemovedFromInventory();
    }

    public override void OnRemovedFromHand()
    {
        DeactivateOxygen();
        base.OnRemovedFromHand();
    }

    private void ActivateOxygen(PlayerInventory inv)
    {
        _stamina = inv.GetComponent<StaminaSystem>();
        if (_stamina != null)
            _stamina.HasOxygenTank = true;
        _altitudeSickness = inv.GetComponent<AltitudeSicknessEffect>();
        _altitudeSickness?.SetOxygenTankActive(true);
    }

    private void DeactivateOxygen()
    {
        if (_stamina != null)
            _stamina.HasOxygenTank = false;
        _stamina = null;
        _altitudeSickness?.SetOxygenTankActive(false);
        _altitudeSickness = null;
    }

    private void Update()
    {
        if (_isBroken || _owner == null) return;

        // GDD §8.3 — 標高2000m以上(=高山病が発生する高度)でのみ自動消費する。
        // 手続き山の実山高に追従させるため、高山病システムと同じ閾値(MountainProfile 連動)を参照。
        bool highAltitude = _altitudeSickness != null
            ? _altitudeSickness.IsAboveSicknessAltitude
            : transform.position.y >= 2000f;
        if (!highAltitude) return;

        ConsumeDurability(_oxygenPerSecond * Time.deltaTime);
    }

    protected override void OnItemBroken()
    {
        DeactivateOxygen();
        Debug.Log("[OxygenTank] 酸素タンクが空になった！高山病リスク！");
        Destroy(gameObject, 1f);
    }
}
