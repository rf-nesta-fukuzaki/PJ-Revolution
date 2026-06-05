using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// GDD §4.3 / §8 — アイテム使用の集中ディスパッチ（R/F・長押し対応）。
/// </summary>
[RequireComponent(typeof(PlayerInventory))]
public class ItemUseController : MonoBehaviour
{
    private const float PACKING_HOLD_SEC  = 3f;
    private const float TENT_HOLD_SEC     = 5f;
    private const float STRETCHER_HOLD_SEC = 1f;

    [SerializeField] private Transform _cameraTransform;

    private PlayerInventory _inventory;
    private PlayerInteraction _interaction;
    private int _inputSlot;

    private ItemBase _chargingItem;
    private float    _chargeElapsed;

    public bool  IsChargingUse => _chargingItem != null;
    public float ChargePct     => _chargingItem == null ? 0f : Mathf.Clamp01(_chargeElapsed / GetHoldDuration(_chargingItem));

    public static bool ConsumedRopePressThisFrame { get; private set; }

    private void Awake()
    {
        _inventory   = GetComponent<PlayerInventory>();
        _interaction = GetComponent<PlayerInteraction>();
        if (_cameraTransform == null)
            _cameraTransform = GetComponentInChildren<Camera>()?.transform ?? transform;
    }

    private void LateUpdate() => ConsumedRopePressThisFrame = false;

    private void Update()
    {
        _inputSlot = LocalCoopPartyMember.ResolveInputSlot(this);
        if (_inputSlot < 0) return;

        UpdateNearbyWinchOperator();

        bool usePressed = InputStateReader.UsePressedThisFrame(_inputSlot)
                        || InputStateReader.ItemUsePressedThisFrame(_inputSlot);

        if (usePressed)
            TryBeginUse();

        UpdateHoldCharge();
    }

    private void UpdateNearbyWinchOperator()
    {
        var deployed = PortableWinchItem.FindDeployedNear(transform.position, PortableWinchItem.OperateRange);
        deployed?.UpdateOperatorDistance(transform);
    }

    private void UpdateHoldCharge()
    {
        if (_chargingItem == null) return;

        bool hold = InputStateReader.ItemUseHeld(_inputSlot)
                    || InputStateReader.UseHeld(_inputSlot);

        if (!hold)
        {
            CancelCharge();
            return;
        }

        _chargeElapsed += Time.deltaTime;
        if (_chargeElapsed >= GetHoldDuration(_chargingItem))
            CompleteCharge();
    }

    public bool TryBeginUse()
    {
        if (InputStateReader.ItemUsePressedThisFrame(_inputSlot)
            && TryOperateNearbyDeployedWinch())
        {
            ConsumedRopePressThisFrame = true;
            return true;
        }

        var item = _inventory.HandItem ?? _inventory.GetHandOrFirstBackpackItem();
        if (item == null || item.IsBroken) return false;

        if (RequiresHold(item))
        {
            _chargingItem  = item;
            _chargeElapsed = 0f;
            return true;
        }

        bool used = ExecuteUse(item);
        if (used && InputStateReader.ItemUsePressedThisFrame(_inputSlot))
            ConsumedRopePressThisFrame = true;
        return used;
    }

    private void CompleteCharge()
    {
        var item = _chargingItem;
        _chargingItem  = null;
        _chargeElapsed = 0f;
        if (item != null) ExecuteUse(item);
    }

    private void CancelCharge()
    {
        _chargingItem  = null;
        _chargeElapsed = 0f;
    }

    private static bool RequiresHold(ItemBase item) =>
        item is PackingKitItem or BivouacTentItem or StretcherItem;

    private static float GetHoldDuration(ItemBase item) => item switch
    {
        PackingKitItem  => PACKING_HOLD_SEC,
        BivouacTentItem => TENT_HOLD_SEC,
        StretcherItem   => STRETCHER_HOLD_SEC,
        _               => 0f,
    };

    public bool ExecuteUse(ItemBase item)
    {
        if (item == null || item.IsBroken) return false;

        int playerId = PlayerScoreId.FromMember(this);
        var relic    = _interaction != null && _interaction.IsCarryingRelic
            ? _interaction.CarriedRelicComponent
            : null;

        bool used = item switch
        {
            ShortRopeItem     _       => false,
            LongRopeItem      _       => false,
            IceAxeItem        axe     => TryUseIceAxe(axe),
            AnchorBoltItem    anchor  => anchor.TryPlaceAnchor(_cameraTransform, playerId),
            GrapplingHookItem hook    => hook.Fire(_cameraTransform.position, _cameraTransform.forward),
            StretcherItem     stretch => stretch.TryToggleExpand(),
            PackingKitItem    pack    => relic != null && pack.ApplyToRelic(relic),
            ThermalCaseItem   thermal => TryUseThermalCase(thermal, relic),
            SecureBeltItem    belt    => TryUseSecureBelt(belt, relic),
            FoodItem          food    => food.TryUse(),
            FlareGunItem      flare   => flare.TryFire(_cameraTransform),
            EmergencyRadioItem radio => radio.TryUse(),
            PortableWinchItem winch   => TryUseWinch(winch),
            BivouacTentItem   tent    => tent.TryPlace(transform.position, transform.rotation),
            OxygenTankItem    _       => false,
            _                         => item.TryUse(),
        };

        if (used)
            Debug.Log($"[ItemUse] {item.ItemName} を使用");
        return used;
    }

    private bool TryUseIceAxe(IceAxeItem axe)
    {
        if (_cameraTransform == null || axe.IsBroken) return false;
        if (!Physics.Raycast(_cameraTransform.position, _cameraTransform.forward,
                out var hit, 2.5f)) return false;

        // GDD: 耐久消費は登攀開始時のみ（ClimbingController.ConsumeIceAxeUse）
        return axe.PlaceGripPoint(hit.point, hit.normal);
    }

    private bool TryUseThermalCase(ThermalCaseItem thermal, RelicBase relic)
    {
        if (thermal.IsProtecting)
        {
            thermal.StopProtecting();
            return true;
        }

        return relic != null && thermal.TryProtectRelic(relic);
    }

    private bool TryUseSecureBelt(SecureBeltItem belt, RelicBase relic)
    {
        if (belt.IsStrapped)
        {
            belt.Unstrap();
            return true;
        }

        if (relic == null || !relic.CanSecureBeltStrap) return false;
        if (!belt.TryStrap(relic, transform)) return false;

        _interaction?.NotifyRelicExternallyAttached();
        return true;
    }

    private bool TryUseWinch(PortableWinchItem winch)
    {
        if (winch.IsDeployedInWorld)
            return winch.TryToggleReel(transform);

        if (!winch.TryDeployFromHand(_inventory, transform))
            return false;

        return true;
    }

    public bool TryOperateNearbyDeployedWinch()
    {
        var winch = PortableWinchItem.FindDeployedNear(transform.position, PortableWinchItem.OperateRange);
        if (winch == null) return false;
        return winch.TryToggleReel(transform);
    }

    private T RaycastFor<T>(float range) where T : Component
    {
        if (_cameraTransform == null) return null;
        var ray = new Ray(_cameraTransform.position, _cameraTransform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, range)) return null;
        return hit.collider.GetComponentInParent<T>();
    }
}
