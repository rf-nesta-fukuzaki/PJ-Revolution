using UnityEngine;

/// <summary>
/// GDD §5.2 — アイテム「固定ベルト」
/// 遺物を背中に固定。両手が空く。小型遺物のみ対応。
/// コスト 6pt / 重量 1 / スロット 1 / 耐久 100
/// </summary>
public class SecureBeltItem : ItemBase
{
    [Header("固定設定")]
    [SerializeField] private float _maxRelicWeight = 3f;   // 固定できる遺物の最大重量
    [SerializeField] private Vector3 _backOffset   = new Vector3(0f, 0f, -0.4f);  // 背中オフセット

    private RelicBase _strappedRelic;
    private bool      _isStrapped;

    public bool       IsStrapped     => _isStrapped;
    public RelicBase  StrappedRelic  => _strappedRelic;

    protected override void Awake()
    {
        base.Awake();
        _itemName          = "固定ベルト";
        _cost              = 6;
        _weight            = 1f;
        _slots             = 1;
        _maxDurability     = 100f;
        _currentDurability = _maxDurability;
        _impactDmgScale    = 0.1f;
    }

    /// <summary>小型遺物を背中にストラップ固定する。</summary>
    public bool TryStrap(RelicBase relic, Transform playerBack)
    {
        if (_isBroken || _isStrapped || relic == null) return false;

        // 重量チェック（Rigidbody.mass で判定）
        var relicRb = relic.GetComponent<Rigidbody>();
        if (relicRb != null && relicRb.mass > _maxRelicWeight)
        {
            Debug.Log($"[SecureBelt] {relic.RelicName} は重すぎて固定できません（mass:{relicRb.mass} > {_maxRelicWeight}）");
            return false;
        }

        _strappedRelic = relic;
        _isStrapped    = true;

        // 遺物をプレイヤーの背中に追従させる
        var relicRigidbody = relic.GetComponent<Rigidbody>();
        if (relicRigidbody != null)
            relicRigidbody.isKinematic = true;

        relic.transform.SetParent(playerBack);
        relic.transform.localPosition = _backOffset;
        relic.transform.localRotation = Quaternion.identity;

        Debug.Log($"[SecureBelt] {relic.RelicName} を背中に固定");
        return true;
    }

    /// <summary>固定を解除する。</summary>
    public void Unstrap()
    {
        if (_strappedRelic == null) return;

        _strappedRelic.transform.SetParent(null);

        var relicRb = _strappedRelic.GetComponent<Rigidbody>();
        if (relicRb != null)
            relicRb.isKinematic = false;

        Debug.Log($"[SecureBelt] {_strappedRelic.RelicName} の固定を解除");
        _strappedRelic = null;
        _isStrapped    = false;
    }

    protected override float GetUseDurabilityDrain() => 0.5f;

    protected override void OnItemBroken()
    {
        Unstrap();
        base.OnItemBroken();
    }
}
