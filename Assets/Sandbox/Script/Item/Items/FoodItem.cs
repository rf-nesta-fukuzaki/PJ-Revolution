using UnityEngine;

/// <summary>
/// GDD §5.2 — アイテム「食料（×3）」
/// スタミナ回復。投げて渡せる。
/// コスト 3pt / 重量 1 / スロット 1 / 耐久 100（消耗品）
/// </summary>
public class FoodItem : ItemBase
{
    [SerializeField] private float _staminaRecover = 40f;
    [SerializeField] private int   _uses           = 3;

    private int _usesLeft;

    protected override void Awake()
    {
        base.Awake();
        _itemName          = "食料";
        _cost              = 3;
        _weight            = 1f;
        _slots             = 1;
        _maxDurability     = 100f;
        _currentDurability = _maxDurability;
        _usesLeft          = _uses;
    }

    public override bool TryUse()
    {
        if (_isBroken || _usesLeft <= 0) return false;

        var stamina = GetComponentInParent<StaminaSystem>();
        if (stamina == null)
        {
            // 近くのプレイヤーに渡したとき
            var nearby = FindNearestPlayer();
            stamina    = nearby?.GetComponent<StaminaSystem>();
        }

        if (stamina == null) return false;

        stamina.Recover(_staminaRecover);
        _usesLeft--;

        Debug.Log($"[Food] スタミナ +{_staminaRecover}。残り {_usesLeft} 個。");

        if (_usesLeft <= 0)
            Break();

        return true;
    }

    private GameObject FindNearestPlayer()
    {
        // 簡易実装：最近のプレイヤーを返す
        float minD  = float.MaxValue;
        StaminaSystem nearest = null;
        foreach (var p in StaminaSystem.RegisteredPlayers)
        {
            float d = Vector3.Distance(transform.position, p.transform.position);
            if (d < minD) { minD = d; nearest = p; }
        }
        return nearest?.gameObject;
    }

    protected override float GetUseDurabilityDrain() => _maxDurability / _uses;
}
