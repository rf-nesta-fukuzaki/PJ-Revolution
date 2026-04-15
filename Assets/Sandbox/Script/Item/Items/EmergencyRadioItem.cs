using System.Collections;
using UnityEngine;

/// <summary>
/// GDD §5.2 — アイテム「緊急無線機」
/// プロキシミティ距離制限を30秒無効化。超壊れやすい。
/// コスト 7pt / 重量 1 / スロット 1 / 耐久 40
/// </summary>
public class EmergencyRadioItem : ItemBase
{
    [Header("無線設定")]
    [SerializeField] private float _broadcastDuration = 30f;  // 無効化時間（秒）

    private bool      _isBroadcasting;
    private Coroutine _broadcastCoroutine;

    public bool IsBroadcasting => _isBroadcasting;

    protected override void Awake()
    {
        base.Awake();
        _itemName           = "緊急無線機";
        _cost               = 7;
        _weight             = 1f;
        _slots              = 1;
        _maxDurability      = 40f;
        _currentDurability  = _maxDurability;
        _impactDmgScale     = 3f;     // 超壊れやすい（衝撃倍率高め）
        _impactDmgThreshold = 1.5f;   // 低速衝撃でもダメージ
    }

    /// <summary>緊急ブロードキャストを開始する（プロキシミティ制限30秒無効）。</summary>
    public override bool TryUse()
    {
        if (_isBroken || _isBroadcasting) return false;

        _broadcastCoroutine = StartCoroutine(BroadcastRoutine());
        return true;
    }

    private IEnumerator BroadcastRoutine()
    {
        _isBroadcasting = true;
        ConsumeDurability(_maxDurability * 0.8f);  // 大量消耗

        Debug.Log("[EmergencyRadio] 緊急ブロードキャスト開始！プロキシミティ制限解除");

        // ProximityVoiceChat がある場合は距離制限を解除
        ProximityVoiceChat.Instance?.SetRangeOverride(true);

        float elapsed = 0f;
        while (elapsed < _broadcastDuration && !_isBroken)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        ProximityVoiceChat.Instance?.SetRangeOverride(false);
        _isBroadcasting = false;

        ConsumeDurability(_maxDurability);  // 使用後は壊れる
        Debug.Log("[EmergencyRadio] ブロードキャスト終了");
    }

    protected override float GetUseDurabilityDrain() => _maxDurability;

    protected override void OnItemBroken()
    {
        if (_broadcastCoroutine != null)
            StopCoroutine(_broadcastCoroutine);

        ProximityVoiceChat.Instance?.SetRangeOverride(false);
        _isBroadcasting = false;

        Debug.Log("[EmergencyRadio] 壊れました！");
        base.OnItemBroken();
    }
}
