using UnityEngine;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

/// <summary>
/// GDD §5.2 — アイテム「梱包キット」
/// 壊れやすい遺物を保護。ダメージ50%軽減。3回使用。
/// コスト 8pt / 重量 2 / スロット 1 / 耐久 100
/// </summary>
public class PackingKitItem : ItemBase
{
    private const int   MAX_USES          = 3;
    private const float DAMAGE_REDUCTION  = 0.5f;

    private int      _usesLeft;
    private RelicBase _protectedRelic;

    protected override void Awake()
    {
        base.Awake();
        _itemName          = "梱包キット";
        _cost              = 8;
        _weight            = 2f;
        _slots             = 1;
        _maxDurability     = 100f;
        _currentDurability = _maxDurability;
        _usesLeft          = MAX_USES;
    }

    /// <summary>指定した遺物に梱包キットを適用する。</summary>
    public bool ApplyToRelic(RelicBase relic)
    {
        if (_isBroken || _usesLeft <= 0 || relic == null) return false;

        _protectedRelic = relic;

        // RelicBase にダメージ軽減を適用（BuffedRelicWrapper でラップ）
        var wrapper = relic.GetComponent<RelicPackingBuffer>();
        if (wrapper == null)
            wrapper = relic.gameObject.AddComponent<RelicPackingBuffer>();

        wrapper.SetReductionFactor(DAMAGE_REDUCTION);

        _usesLeft--;
        float drain = _maxDurability / MAX_USES;
        ConsumeDurability(drain);

        // GDD §15.2 — packing_wrap（梱包シートを巻き付ける音）
        PPAudioManager.Instance?.PlaySE(SoundId.PackingWrap, relic.transform.position);

        Debug.Log($"[PackingKit] {relic.RelicName} を梱包。残り使用回数: {_usesLeft}");
        return true;
    }

    protected override void OnItemBroken()
    {
        Debug.Log("[PackingKit] 梱包キットを使い切った");
        Destroy(gameObject);
    }
}
