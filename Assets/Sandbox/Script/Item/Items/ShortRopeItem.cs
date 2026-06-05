using UnityEngine;

/// <summary>
/// GDD §5.2 / §5.1 — アイテム「ショートロープ（10m）」
/// </summary>
public class ShortRopeItem : ItemBase, IShopRopeItem
{
    [Header("ロープ設定")]
    [SerializeField] private float _ropeLength = 10f;

    private int  _connectedPlayerIdA = -1;
    private int  _connectedPlayerIdB = -1;
    private bool _isConnected;
    private bool _isRelicMode;

    public bool  IsConnected => _isConnected;
    public bool  IsRelicMode  => _isRelicMode;
    public float RopeLength   => _ropeLength;
    public float BreakForce   => ShopRopeConstants.ShortRopeBreakForce;

    protected override void Awake()
    {
        base.Awake();
        _itemName          = "ショートロープ";
        _cost              = 5;
        _weight            = 1f;
        _slots             = 1;
        _maxDurability     = 80f;
        _currentDurability = _maxDurability;
        _impactDmgScale    = 0.5f;
    }

    public bool TryConnectToPlayer(int playerIdA, int playerIdB)
    {
        if (_isBroken || _isConnected) return false;

        var bridge = FindOwnerBridge();
        if (bridge != null)
            return bridge.RequestConnectToPlayer(this, playerIdA, playerIdB, _ropeLength, BreakForce);

        return ApplyConnectLocal(playerIdA, playerIdB);
    }

    public bool TryAttachToRelic(RelicBase relic, int playerId, Vector3 fromPosition) => false;

    public bool TryConnectToAnchor(Transform anchor, int playerId, Vector3 fromPosition)
    {
        if (_isBroken || _isConnected || anchor == null) return false;
        if (Vector3.Distance(fromPosition, anchor.position) > ShopRopeConstants.AnchorConnectRange)
            return false;

        bool ok = GameServices.Ropes != null &&
                  GameServices.Ropes.ConnectPlayerToAnchor(playerId, anchor, _ropeLength, BreakForce, this);

        if (!ok) return false;

        _isConnected        = true;
        _isRelicMode        = false;
        _connectedPlayerIdA = playerId;
        _connectedPlayerIdB = -1;
        Debug.Log($"[ShortRope] アンカー {anchor.name} に固定");
        return true;
    }

    private bool ApplyConnectLocal(int playerIdA, int playerIdB)
    {
        bool ok = GameServices.Ropes != null &&
                  GameServices.Ropes.ConnectRope(playerIdA, playerIdB, _ropeLength, BreakForce, this);

        if (!ok) return false;

        ApplyPlayerConnectState(playerIdA, playerIdB);
        return true;
    }

    public void ApplyPlayerConnectState(int playerIdA, int playerIdB)
    {
        _isConnected        = true;
        _isRelicMode        = false;
        _connectedPlayerIdA = playerIdA;
        _connectedPlayerIdB = playerIdB;
        Debug.Log($"[ShortRope] プレイヤー {playerIdA} と {playerIdB} を接続");
    }

    public void CutRope()
    {
        if (!_isConnected) return;

        var bridge = FindOwnerBridge();
        if (bridge != null && !_isRelicMode)
        {
            bridge.RequestDisconnectShopRope(this);
            return;
        }

        DisconnectLocal();
    }

    public void CutRopeLocalOnly()
    {
        if (!_isConnected) return;
        DisconnectLocal();
    }

    private void DisconnectLocal()
    {
        if (_isRelicMode)
            GameServices.Ropes?.DisconnectPlayerRelic(_connectedPlayerIdA);
        else if (_connectedPlayerIdB >= 0)
            GameServices.Ropes?.DisconnectRope(_connectedPlayerIdA, _connectedPlayerIdB);
        else
            GameServices.Ropes?.DisconnectPlayerAnchor(_connectedPlayerIdA);

        ResetConnectionState();
        Debug.Log("[ShortRope] ロープを切断");
    }

    private void ResetConnectionState()
    {
        _isConnected        = false;
        _isRelicMode        = false;
        _connectedPlayerIdA = -1;
        _connectedPlayerIdB = -1;
    }

    private PlayerShopRopeNetworkBridge FindOwnerBridge()
    {
        var inv = GetComponentInParent<PlayerInventory>();
        if (inv != null)
            return inv.GetComponent<PlayerShopRopeNetworkBridge>();

        foreach (var registered in PlayerInventory.RegisteredInventories)
        {
            if (registered != null && registered.HandItem == this)
                return registered.GetComponent<PlayerShopRopeNetworkBridge>();
        }

        return null;
    }

    protected override float GetUseDurabilityDrain() => 0f;

    protected override void OnItemBroken()
    {
        CutRope();
        base.OnItemBroken();
    }
}
