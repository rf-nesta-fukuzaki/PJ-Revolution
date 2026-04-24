using UnityEngine;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

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

    public override bool TryUse()
    {
        if (_isBroken) return false;

        _useCount++;
        float drain = _maxDurability / MAX_USES;
        ConsumeDurability(drain);

        // GDD §15.2 — ice_axe_strike
        PPAudioManager.Instance?.PlaySE(SoundId.IceAxeStrike, transform.position);

        Debug.Log($"[IceAxe] 使用 {_useCount}/{MAX_USES}");
        return true;
    }

    protected override void OnItemBroken()
    {
        Debug.Log("[IceAxe] アイスアックスが壊れた！氷壁が登れない！");
        base.OnItemBroken();
    }
}
