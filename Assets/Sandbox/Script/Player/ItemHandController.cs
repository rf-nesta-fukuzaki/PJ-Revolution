using UnityEngine;

/// <summary>
/// GDD §4.3 — Tab インベントリ・1〜4 クイックスロット・手持ち管理。
/// </summary>
[RequireComponent(typeof(PlayerInventory))]
public class ItemHandController : MonoBehaviour
{
    private PlayerInventory _inventory;
    private PlayerHealthSystem _health;
    private int _inputSlot;

    private void Awake()
    {
        _inventory = GetComponent<PlayerInventory>();
        _health    = GetComponent<PlayerHealthSystem>();
    }

    private void Update()
    {
        _inputSlot = LocalCoopPartyMember.ResolveInputSlot(this);
        if (_inputSlot < 0 || (_health != null && _health.IsDead)) return;

        if (InputStateReader.InventoryTogglePressedThisFrame(_inputSlot))
            InventoryHud.Instance?.Toggle();

        if (InputStateReader.QuickSlotPressedThisFrame(0, _inputSlot)) _inventory.TryQuickEquip(0);
        if (InputStateReader.QuickSlotPressedThisFrame(1, _inputSlot)) _inventory.TryQuickEquip(1);
        if (InputStateReader.QuickSlotPressedThisFrame(2, _inputSlot)) _inventory.TryQuickEquip(2);
        if (InputStateReader.QuickSlotPressedThisFrame(3, _inputSlot)) _inventory.TryQuickEquip(3);
    }
}
