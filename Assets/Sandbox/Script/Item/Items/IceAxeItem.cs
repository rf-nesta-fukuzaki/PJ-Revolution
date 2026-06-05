using UnityEngine;
using PeakPlunder.Audio;

/// <summary>
/// GDD §5.2 — アイテム「アイスアックス」
/// 氷壁グリップ。15回使用で破損。RequireIceAxe の GrabPoint で必要。
/// コスト 8pt / 重量 1 / スロット 1 / 耐久 60
/// MagneticTarget（金属製）
/// </summary>
[RequireComponent(typeof(MagneticTarget))]
public class IceAxeItem : ItemBase
{
    // GDD §5.2 — 表示名。インベントリ検索（ClimbingController.ConsumeIceAxeUse 等）の正規キーとしても使う。
    public const string ItemNameKey = "アイスアックス";

    private const int MAX_USES = 15;
    private int _useCount;

    protected override void Awake()
    {
        base.Awake();
        _itemName          = ItemNameKey;
        _cost              = 8;
        _weight            = 1f;
        _slots             = 1;
        _maxDurability     = 60f;
        _currentDurability = _maxDurability;
        _impactDmgScale    = 2f;
    }

    /// <summary>R 打ち込み — 掴みポイント生成のみ（耐久は登攀開始時に消費）。</summary>
    public bool PlaceGripPoint(Vector3 position, Vector3 normal)
    {
        if (_isBroken) return false;

        ClimbingPointFactory.CreateIceAxePoint(position, normal);
        GameServices.Audio?.PlaySE(SoundId.IceAxeStrike, position);
        Debug.Log("[IceAxe] グリップポイントを設置");
        return true;
    }

    /// <summary>登攀開始時の耐久消費（GDD: 4/回 = 15回）。</summary>
    public override bool TryUse()
    {
        if (_isBroken) return false;

        _useCount++;
        float drain = _maxDurability / MAX_USES;
        ConsumeDurability(drain);

        GameServices.Audio?.PlaySE(SoundId.IceAxeStrike, transform.position);
        Debug.Log($"[IceAxe] 登攀グリップ {_useCount}/{MAX_USES}");
        return true;
    }

    protected override void OnItemBroken()
    {
        Debug.Log("[IceAxe] アイスアックスが壊れた！氷壁が登れない！");
        base.OnItemBroken();
    }
}
