using UnityEngine;
using PeakPlunder.Audio;

/// <summary>
/// GDD §5.2 / §5.1 — アイテム「ショートロープ（10m）」
/// </summary>
public class ShortRopeItem : ItemBase, IShopRopeItem
{
    public const float RelicAttachRange = 2f;

    [Header("ロープ設定")]
    [SerializeField] private float _ropeLength = 10f;

    private int       _connectedPlayerIdA = -1;
    private int       _connectedPlayerIdB = -1;
    private RelicBase _connectedRelic;
    private bool      _isConnected;
    private bool      _isRelicMode;

    public bool  IsConnected    => _isConnected;
    public bool  IsRelicMode    => _isRelicMode;
    public float RopeLength     => _ropeLength;
    public float BreakForce     => ShopRopeConstants.ShortRopeBreakForce;
    public RelicBase ConnectedRelic => _connectedRelic;

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

    public bool TryAttachToRelic(RelicBase relic, int playerId, Vector3 fromPosition)
    {
        if (_isBroken || _isConnected || relic == null) return false;

        var bridge = FindOwnerBridge();
        if (bridge != null)
            return bridge.RequestAttachToRelic(this, relic, _ropeLength, fromPosition, BreakForce);

        return TryAttachToRelicLocal(relic, playerId, fromPosition, _ropeLength, BreakForce);
    }

    public bool TryConnectToAnchor(Transform anchor, int playerId, Vector3 fromPosition)
    {
        if (_isBroken || _isConnected || anchor == null) return false;
        if (Vector3.Distance(fromPosition, anchor.position) > ShopRopeConstants.AnchorConnectRange)
            return false;

        var bridge = FindOwnerBridge();
        if (bridge != null)
            return bridge.RequestConnectToAnchor(this, anchor, playerId, fromPosition, _ropeLength, BreakForce);

        return ApplyAnchorConnectLocal(anchor, playerId);
    }

    private bool ApplyAnchorConnectLocal(Transform anchor, int playerId)
    {
        bool ok = GameServices.Ropes != null &&
                  GameServices.Ropes.ConnectPlayerToAnchor(playerId, anchor, _ropeLength, BreakForce, this);

        if (!ok) return false;

        ApplyAnchorConnectState(anchor, playerId);
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

    public bool TryAttachToRelicLocal(
        RelicBase relic,
        int playerId,
        Vector3 fromPosition,
        float length,
        float breakForce)
    {
        if (_isBroken || _isConnected || relic == null) return false;

        var grab = relic.GetComponent<RelicGrabPoint>();
        if (grab != null && !grab.IsWithinAttachRange(fromPosition))
            return false;
        if (grab == null && Vector3.Distance(fromPosition, relic.transform.position) > RelicAttachRange)
            return false;

        var carrier = relic.GetComponent<RelicCarrier>();
        if (carrier != null && carrier.IsBeingCarried)
            return false;

        var relicRb = relic.GetComponent<Rigidbody>();
        if (relicRb == null) return false;

        if (relicRb.isKinematic)
            relicRb.isKinematic = false;

        bool ok = GameServices.Ropes != null &&
                  GameServices.Ropes.ConnectPlayerToRelic(playerId, relicRb, length, breakForce, this);

        if (!ok) return false;

        ApplyRelicAttachState(relic, playerId);
        GameServices.Audio?.PlaySE(SoundId.RopeConnect, relic.transform.position);
        Debug.Log($"[ShortRope] 遺物 {relic.RelicName} に括り付け");
        return true;
    }

    public void ApplyPlayerConnectState(int playerIdA, int playerIdB)
    {
        _isConnected        = true;
        _isRelicMode        = false;
        _connectedPlayerIdA = playerIdA;
        _connectedPlayerIdB = playerIdB;
        _connectedRelic     = null;
        Debug.Log($"[ShortRope] プレイヤー {playerIdA} と {playerIdB} を接続");
    }

    public void ApplyRelicAttachState(RelicBase relic, int playerId)
    {
        _isConnected        = true;
        _isRelicMode        = true;
        _connectedPlayerIdA = playerId;
        _connectedPlayerIdB = -1;
        _connectedRelic     = relic;
    }

    public void ApplyAnchorConnectState(Transform anchor, int playerId)
    {
        _isConnected        = true;
        _isRelicMode        = false;
        _connectedPlayerIdA = playerId;
        _connectedPlayerIdB = -1;
        _connectedRelic     = null;
    }

    public void CutRope()
    {
        if (!_isConnected) return;

        var bridge = FindOwnerBridge();
        if (bridge != null)
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
        _connectedRelic     = null;
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
