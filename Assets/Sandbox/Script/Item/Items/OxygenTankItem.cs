using UnityEngine;

/// <summary>
/// GDD §5.2 — アイテム「酸素タンク」
/// 高山病防止（2000m以上で必要）。
/// コスト 12pt / 重量 2 / スロット 1 / 耐久 60
/// </summary>
public class OxygenTankItem : ItemBase
{
    [Header("酸素設定")]
    [SerializeField] private float _oxygenPerSecond = 1f;   // 毎秒消費量

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

    public override void OnStoredInInventory(PlayerInventory inv)
    {
        base.OnStoredInInventory(inv);
        _stamina = inv.GetComponent<StaminaSystem>();
        if (_stamina != null)
            _stamina.HasOxygenTank = true;

        // 高山病エフェクトを抑制（GDD §3.4）
        _altitudeSickness = inv.GetComponent<AltitudeSicknessEffect>();
        _altitudeSickness?.SetOxygenTankActive(true);
    }

    public override void OnRemovedFromInventory()
    {
        if (_stamina != null)
            _stamina.HasOxygenTank = false;
        _stamina = null;

        _altitudeSickness?.SetOxygenTankActive(false);
        _altitudeSickness = null;

        base.OnRemovedFromInventory();
    }

    private void Update()
    {
        if (_isBroken || _owner == null) return;

        float altitude = transform.position.y;
        if (altitude < 2000f) return;

        // 高山帯にいる間は酸素を消費
        ConsumeDurability(_oxygenPerSecond * Time.deltaTime);
    }

    protected override void OnItemBroken()
    {
        if (_stamina != null)
            _stamina.HasOxygenTank = false;

        _altitudeSickness?.SetOxygenTankActive(false);
        _altitudeSickness = null;

        Debug.Log("[OxygenTank] 酸素タンクが空になった！高山病リスク！");
        Destroy(gameObject, 1f);
    }
}
