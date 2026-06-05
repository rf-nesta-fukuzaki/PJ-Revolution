using Unity.Netcode;
using UnityEngine;

/// <summary>
/// GDD §3.2 — プレイヤーのインタラクション入力処理。
///   E: 拾う / 置く / 担架 / ウインチ / ヘリ / 返品
///   R/F: アイテム使用（ItemUseController）
///   G: 遺物ドロップ / 手持ちアイテムを丁寧に置く / ウインチ回収
///   X: ショップロープ切断 / ウインチケーブル切断
/// </summary>
[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(PlayerHealthSystem))]
[RequireComponent(typeof(ItemUseController))]
public class PlayerInteraction : MonoBehaviour
{
    private const float INTERACT_RANGE      = 2.5f;
    private const float DROP_IMPULSE        = 4f;
    private const float STRETCHER_RANGE     = 2.0f;
    private const float HELICOPTER_RANGE    = 8f;
    private const float RELIC_GRAB_CAST_RADIUS = 0.6f;
    private const float RELIC_GRAB_RANGE_MULT  = 1.2f;
    private const float RELIC_GRAB_MIN_DOT      = 0.3f;

    [Header("インタラクション設定")]
    [SerializeField] private float     _interactRange  = INTERACT_RANGE;
    [SerializeField] private Transform _cameraTransform;

    private PlayerInventory    _inventory;
    private PlayerHealthSystem _health;
    private ItemUseController  _itemUse;
    private RelicCarrier       _carriedRelic;
    private StretcherItem      _attachedStretcher;
    private BalanceIndicator   _balanceIndicator;
    private int                _inputSlot;

    private IScoreService ScoreService => GameServices.Score;

    public bool IsCarryingRelic => _carriedRelic != null;
    public RelicBase CarriedRelicComponent => _carriedRelic != null
        ? _carriedRelic.GetComponent<RelicBase>()
        : null;

    private void Awake()
    {
        _inventory        = GetComponent<PlayerInventory>();
        _health           = GetComponent<PlayerHealthSystem>();
        _itemUse          = GetComponent<ItemUseController>();
        _balanceIndicator = GetComponentInChildren<BalanceIndicator>();
        if (_cameraTransform == null)
            _cameraTransform = GetComponentInChildren<Camera>()?.transform ?? transform;
        _inputSlot = LocalCoopPartyMember.ResolveInputSlot(this);
    }

    private void Start()
    {
        InventoryHud.Instance?.Bind(_inventory);
    }

    private void OnEnable()  => _health.OnDied += OnPlayerDied;
    private void OnDisable() => _health.OnDied -= OnPlayerDied;

    private void Update()
    {
        _inputSlot = LocalCoopPartyMember.ResolveInputSlot(this);
        if (_inputSlot < 0 || _health.IsDead) return;

        if (InputStateReader.InteractPressedThisFrame(_inputSlot)) HandleInteract();
        if (InputStateReader.DropPressedThisFrame(_inputSlot))     HandleDrop();
        if (InputStateReader.CableCutPressedThisFrame(_inputSlot)) HandleCableCut();
    }

    private void HandleCableCut()
    {
        if (TryCutShopRope()) return;

        var winch = PortableWinchItem.FindDeployedNear(transform.position, PortableWinchItem.OperateRange);
        if (winch == null) return;

        winch.CutCable();
    }

    private bool TryCutShopRope()
    {
        if (_inventory.HandItem is IShopRopeItem handRope && handRope.IsConnected)
        {
            handRope.CutRope();
            return true;
        }

        int playerId = PlayerScoreId.FromMember(this);
        var ropes = GameServices.Ropes;
        if (ropes == null || !ropes.IsPlayerInAnyShopRope(playerId)) return false;

        if (_inventory.HandItem is IShopRopeItem anyRope)
        {
            anyRope.CutRope();
            return true;
        }

        ropes.DisconnectAllForPlayer(playerId);
        return true;
    }

    private void HandleInteract()
    {
        if (TryRefundAtLocker()) return;
        if (TryRecoverGrapplingHook()) return;
        if (TryInteractDeployedWinch()) return;
        if (TryBoardHelicopter()) return;
        if (TryDetachStretcher()) return;
        if (TryMountRelicOnStretcher()) return;
        if (TryConnectShopRope()) return;
        if (TryAttachLongRopeToRelic()) return;
        if (TryPutDownRelic()) return;
        if (TryAttachStretcher()) return;
        if (TryPickUpRelic()) return;
        TryPickUpItem();
    }

    private bool TryInteractDeployedWinch()
    {
        var winch = RaycastFor<PortableWinchItem>(_interactRange);
        if (winch == null || !winch.IsDeployedInWorld) return false;
        if (Vector3.Distance(transform.position, winch.transform.position) > PortableWinchItem.OperateRange)
            return false;

        if (!winch.HasCableHook && !winch.IsCableAttached)
            return winch.TryDeployCable();

        if (winch.HasCableHook && !winch.IsCableAttached)
        {
            var target = RaycastFor<Rigidbody>(_interactRange * 2f);
            if (target != null)
                return winch.TryAttachCableTo(target);
        }

        return false;
    }

    /// <summary>固定ベルト等へ遺物を移管したあと、手元運搬参照をクリアする。</summary>
    public void NotifyRelicExternallyAttached()
    {
        _carriedRelic = null;
        _balanceIndicator?.Hide();
    }

    private bool TryConnectShopRope()
    {
        if (_inventory.HandItem is not IShopRopeItem rope || rope.IsConnected) return false;

        int playerId = PlayerScoreId.FromMember(this);

        var partner = FindNearbyPlayerForRope(ShopRopeConstants.ConnectRange);
        if (partner != null)
        {
            int partnerId = PlayerScoreId.FromMember(partner);
            return rope.TryConnectToPlayer(playerId, partnerId);
        }

        var anchor = FindNearestAnchor(ShopRopeConstants.AnchorConnectRange);
        if (anchor != null)
            return rope.TryConnectToAnchor(anchor, playerId, transform.position);

        return false;
    }

    private PlayerInventory FindNearbyPlayerForRope(float range)
    {
        PlayerInventory best     = null;
        float           bestDist = float.MaxValue;
        Vector3         myPos    = transform.position;

        foreach (var inv in PlayerInventory.RegisteredInventories)
        {
            if (inv == null || inv == _inventory) continue;
            float d = Vector3.Distance(myPos, inv.transform.position);
            if (d > range || d >= bestDist) continue;
            bestDist = d;
            best     = inv;
        }

        return best;
    }

    private Transform FindNearestAnchor(float range)
    {
        var ropes = GameServices.Ropes;
        if (ropes == null || ropes.AnchorPoints.Count == 0) return null;

        Transform best     = null;
        float       bestDist = float.MaxValue;
        Vector3     myPos    = transform.position;

        foreach (var anchor in ropes.AnchorPoints)
        {
            if (anchor == null) continue;
            float d = Vector3.Distance(myPos, anchor.position);
            if (d > range || d >= bestDist) continue;
            bestDist = d;
            best     = anchor;
        }

        return best;
    }

    private bool TryAttachLongRopeToRelic()
    {
        if (_inventory.HandItem is not LongRopeItem longRope) return false;
        if (longRope.IsConnected) return false;

        var relic = FindNearbyRelicForRope(LongRopeItem.RelicAttachRange);
        if (relic == null) return false;

        int playerId = PlayerScoreId.FromMember(this);
        return longRope.TryAttachToRelic(relic, playerId, transform.position);
    }

    private RelicBase FindNearbyRelicForRope(float range)
    {
        RelicBase best     = null;
        float     bestDist = float.MaxValue;

        var carriers = Object.FindObjectsByType<RelicCarrier>(FindObjectsSortMode.None);
        foreach (var carrier in carriers)
        {
            if (carrier == null || carrier.IsBeingCarried) continue;

            var relic = carrier.GetComponent<RelicBase>();
            if (relic == null || relic.IsDestroyed) continue;

            var grab = relic.GetComponent<RelicGrabPoint>();
            Vector3 anchor = grab != null ? grab.AttachPosition : relic.transform.position;
            float dist = Vector3.Distance(transform.position, anchor);
            if (dist > range || dist >= bestDist) continue;

            bestDist = dist;
            best     = relic;
        }

        return best;
    }

    private bool TryRecoverGrapplingHook()
    {
        var hook = FindActiveGrapplingHook();
        return hook != null && hook.TryRecover(transform.position);
    }

    private GrapplingHookItem FindActiveGrapplingHook()
    {
        if (_inventory.HandItem is GrapplingHookItem handHook && handHook.IsGrappling)
            return handHook;

        foreach (var item in _inventory.BackpackItems)
        {
            if (item is GrapplingHookItem hook && hook.IsGrappling)
                return hook;
        }

        foreach (var item in _inventory.QuickSlotItems)
        {
            if (item is GrapplingHookItem hook && hook.IsGrappling)
                return hook;
        }

        return null;
    }

    private bool TryRefundAtLocker()
    {
        var locker = BasecampItemLocker.Instance;
        return locker != null && locker.TryRefundNearby(_inventory, _cameraTransform);
    }

    private bool TryBoardHelicopter()
    {
        var heli = GameServices.Helicopter;
        if (heli == null || !heli.IsBoarding) return false;
        if (Vector3.Distance(transform.position, heli.HelipadPosition) > HELICOPTER_RANGE) return false;
        heli.BoardPlayer(_health);
        return true;
    }

    private bool TryDetachStretcher()
    {
        if (_attachedStretcher == null) return false;

        var netSync = _attachedStretcher.GetComponent<NetworkStretcherSync>();
        if (netSync != null && netSync.IsSpawned)
            netSync.RequestDetachServerRpc();
        else
            _attachedStretcher.Detach(this);

        _attachedStretcher = null;
        return true;
    }

    private bool TryMountRelicOnStretcher()
    {
        if (_carriedRelic == null) return false;
        var stretcher = RaycastFor<StretcherItem>(STRETCHER_RANGE);
        if (stretcher == null) return false;

        var relic = _carriedRelic.GetComponent<RelicBase>();
        if (relic == null) return false;

        _carriedRelic.PutDown();
        _carriedRelic = null;
        _balanceIndicator?.Hide();

        return stretcher.MountRelic(relic);
    }

    private bool TryPutDownRelic()
    {
        if (_carriedRelic == null) return false;
        _carriedRelic.PutDown();
        _carriedRelic = null;
        _balanceIndicator?.Hide();
        return true;
    }

    private bool TryAttachStretcher()
    {
        var stretcher = RaycastFor<StretcherItem>(STRETCHER_RANGE);
        if (stretcher == null) return false;

        var netSync = stretcher.GetComponent<NetworkStretcherSync>();
        if (netSync != null && netSync.IsSpawned)
        {
            netSync.RequestAttachServerRpc();
            _attachedStretcher = stretcher;
            return true;
        }

        if (stretcher.TryAttach(this, out _))
            _attachedStretcher = stretcher;
        return true;
    }

    private bool TryPickUpRelic()
    {
        var carrier = FindGrabbableRelic();
        if (carrier == null) return false;

        int scoreId = PlayerScoreId.FromMember(this);
        carrier.PickUp(transform, scoreId);
        _carriedRelic = carrier;
        _balanceIndicator?.Show(carrier);
        ScoreService?.RecordRelicFound(scoreId);
        ScoreService?.RegisterCollectedRelic(carrier.GetComponent<RelicBase>());
        return true;
    }

    private void TryPickUpItem()
    {
        var item = RaycastFor<ItemBase>(_interactRange);
        if (item == null) return;

        if (_inventory.TryAdd(item))
            Debug.Log($"[Interaction] {item.ItemName} を拾った");
    }

    private void HandleDrop()
    {
        var deployedWinch = PortableWinchItem.FindDeployedNear(transform.position, PortableWinchItem.OperateRange);
        if (deployedWinch != null && deployedWinch.TryRetrieve(_inventory))
            return;

        if (_carriedRelic != null)
        {
            _carriedRelic.Drop(_cameraTransform.forward * DROP_IMPULSE);
            _carriedRelic = null;
            _balanceIndicator?.Hide();
            return;
        }

        if (_inventory.HasHandItem)
            _inventory.DropHandItemGently();
    }

    private void OnPlayerDied(PlayerHealthSystem _)
    {
        _attachedStretcher?.Detach(this);
        _attachedStretcher = null;

        if (_carriedRelic != null)
        {
            _carriedRelic.Drop(Vector3.zero);
            _carriedRelic = null;
            _balanceIndicator?.Hide();
        }

        _inventory.DropAllOnDeath();
    }

    private T RaycastFor<T>(float range) where T : Component
    {
        if (_cameraTransform == null) return null;
        var ray = new Ray(_cameraTransform.position, _cameraTransform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, range)) return null;
        return hit.collider.GetComponentInParent<T>();
    }

    private RelicCarrier FindGrabbableRelic()
    {
        if (_cameraTransform == null) return null;

        Vector3 origin  = _cameraTransform.position;
        Vector3 forward = _cameraTransform.forward;
        float   range   = _interactRange * RELIC_GRAB_RANGE_MULT;

        if (Physics.SphereCast(origin, RELIC_GRAB_CAST_RADIUS, forward,
                out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
        {
            var carrier = hit.collider.GetComponentInParent<RelicCarrier>();
            if (IsGrabbable(carrier)) return carrier;
        }

        Vector3 center = origin + forward * (range * 0.5f);
        var overlaps = Physics.OverlapSphere(center, range, ~0, QueryTriggerInteraction.Ignore);

        RelicCarrier best     = null;
        float        bestDist = float.MaxValue;
        foreach (var col in overlaps)
        {
            var carrier = col.GetComponentInParent<RelicCarrier>();
            if (!IsGrabbable(carrier)) continue;

            Vector3 toRelic = carrier.transform.position - origin;
            float   dist    = toRelic.magnitude;
            if (dist > range) continue;
            if (dist > 0.01f && Vector3.Dot(forward, toRelic / dist) < RELIC_GRAB_MIN_DOT) continue;

            if (dist < bestDist)
            {
                bestDist = dist;
                best     = carrier;
            }
        }
        return best;
    }

    private static bool IsGrabbable(RelicCarrier carrier)
        => carrier != null && !carrier.IsBeingCarried;
}
