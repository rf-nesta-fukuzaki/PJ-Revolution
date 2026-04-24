using UnityEngine;

/// <summary>
/// GDD §5.2 — アイテム「ショートロープ（10m）」
/// 基本のプレイヤー連結。極端な負荷で切れる。
/// コスト 5pt / 重量 1 / スロット 1 / 耐久 80
/// </summary>
public class ShortRopeItem : ItemBase
{
    [Header("ロープ設定")]
    [SerializeField] private float _ropeLength = 10f;

    private int _connectedPlayerIdA = -1;
    private int _connectedPlayerIdB = -1;
    private bool _isConnected;

    protected override void Awake()
    {
        base.Awake();
        _itemName         = "ショートロープ";
        _cost             = 5;
        _weight           = 1f;
        _slots            = 1;
        _maxDurability    = 80f;
        _currentDurability = _maxDurability;
        _impactDmgScale   = 0.5f;
    }

    /// <summary>2人のプレイヤーをロープで繋ぐ。</summary>
    public bool TryConnect(int playerIdA, int playerIdB)
    {
        if (_isBroken || _isConnected) return false;

        bool ok = GameServices.Ropes != null &&
                  GameServices.Ropes.ConnectRope(playerIdA, playerIdB, _ropeLength);

        if (ok)
        {
            _isConnected       = true;
            _connectedPlayerIdA = playerIdA;
            _connectedPlayerIdB = playerIdB;
            Debug.Log($"[ShortRope] プレイヤー {playerIdA} と {playerIdB} を接続");
        }
        return ok;
    }

    /// <summary>ロープを切断する。</summary>
    public void CutRope()
    {
        if (!_isConnected) return;

        GameServices.Ropes?.DisconnectRope(_connectedPlayerIdA, _connectedPlayerIdB);
        _isConnected = false;
        Debug.Log("[ShortRope] ロープを切断");
    }

    protected override float GetUseDurabilityDrain() => 2f;

    protected override void OnItemBroken()
    {
        CutRope();
        base.OnItemBroken();
    }
}
