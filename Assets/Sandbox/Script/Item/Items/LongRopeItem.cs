using UnityEngine;

/// <summary>
/// GDD §5.2 — アイテム「ロングロープ（25m）」
/// 広範囲連結。チームの展開幅が広がる。
/// コスト 10pt / 重量 2 / スロット 2 / 耐久 70
/// </summary>
public class LongRopeItem : ItemBase
{
    [Header("ロープ設定")]
    [SerializeField] private float _ropeLength = 25f;

    private int  _connectedPlayerIdA = -1;
    private int  _connectedPlayerIdB = -1;
    private bool _isConnected;

    protected override void Awake()
    {
        base.Awake();
        _itemName          = "ロングロープ";
        _cost              = 10;
        _weight            = 2f;
        _slots             = 2;
        _maxDurability     = 70f;
        _currentDurability = _maxDurability;
        _impactDmgScale    = 0.5f;
    }

    /// <summary>2人のプレイヤーをロープで繋ぐ。</summary>
    public bool TryConnect(int playerIdA, int playerIdB)
    {
        if (_isBroken || _isConnected) return false;

        bool ok = RopeManager.Instance != null &&
                  RopeManager.Instance.ConnectRope(playerIdA, playerIdB, _ropeLength);

        if (ok)
        {
            _isConnected        = true;
            _connectedPlayerIdA = playerIdA;
            _connectedPlayerIdB = playerIdB;
            Debug.Log($"[LongRope] プレイヤー {playerIdA} と {playerIdB} を {_ropeLength}m で接続");
        }
        return ok;
    }

    /// <summary>ロープを切断する。</summary>
    public void CutRope()
    {
        if (!_isConnected) return;

        RopeManager.Instance?.DisconnectRope(_connectedPlayerIdA, _connectedPlayerIdB);
        _isConnected = false;
        Debug.Log("[LongRope] ロープを切断");
    }

    protected override float GetUseDurabilityDrain() => 2f;

    protected override void OnItemBroken()
    {
        CutRope();
        base.OnItemBroken();
    }
}
