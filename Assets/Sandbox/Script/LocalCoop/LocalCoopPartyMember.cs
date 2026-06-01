using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ローカル Co-op パーティの1スロット（0〜3）を表すマーカー。
/// </summary>
public sealed class LocalCoopPartyMember : MonoBehaviour
{
    [SerializeField] private int _slotIndex;
    [SerializeField] private bool _isHumanControlled = true;
    [SerializeField] private string _displayName = "Player";

    public int SlotIndex => _slotIndex;
    public bool IsHumanControlled => _isHumanControlled;
    public string DisplayName => _displayName;
    public Gamepad AssignedGamepad { get; private set; }

    /// <summary>NGO クライアント ID。NPC は <see cref="ulong.MaxValue"/>。</summary>
    public ulong NetworkOwnerClientId { get; private set; } = ulong.MaxValue;

    public bool IsNetworkNpc => NetworkOwnerClientId == ulong.MaxValue && !_isHumanControlled;

    public void Configure(int slotIndex, bool isHuman, string displayName, Gamepad assignedGamepad = null)
    {
        _slotIndex = Mathf.Clamp(slotIndex, 0, LocalCoopSettings.MaxPartySize - 1);
        _isHumanControlled = isHuman;
        _displayName = string.IsNullOrWhiteSpace(displayName)
            ? (isHuman ? $"Player {_slotIndex + 1}" : $"Companion {_slotIndex + 1}")
            : displayName;
        AssignedGamepad = isHuman && !LocalCoopSettings.IsOnline ? assignedGamepad : null;
        gameObject.name = isHuman ? $"Player_{_displayName}" : $"NPC_{_displayName}";
        RegisterWithRoster();
    }

    public void SetAssignedGamepad(Gamepad gamepad) => AssignedGamepad = gamepad;

    public void SetNetworkOwner(ulong clientId) => NetworkOwnerClientId = clientId;

    private void OnEnable() => RegisterWithRoster();

    private void OnDisable()
    {
        if (LocalCoopRoster.Instance != null)
            LocalCoopRoster.Instance.UnregisterSlot(this);
    }

    private void RegisterWithRoster()
    {
        if (!LocalCoopSettings.IsActive) return;
        LocalCoopRoster.Instance?.RegisterSlot(_slotIndex, this);
    }

    /// <summary>入力スロット（人間のみ。NPC は -1）。</summary>
    public int InputSlot => _isHumanControlled ? _slotIndex : -1;

    public static LocalCoopPartyMember Get(MonoBehaviour behaviour)
    {
        return behaviour != null ? behaviour.GetComponent<LocalCoopPartyMember>() : null;
    }

    public static int ResolveInputSlot(MonoBehaviour behaviour)
    {
        var member = Get(behaviour);
        if (member != null && member.IsHumanControlled)
        {
            if (LocalCoopSettings.IsOnline)
            {
                var netObj = behaviour.GetComponent<NetworkObject>();
                if (netObj != null && !netObj.IsOwner)
                    return -1;
            }

            return member.InputSlot;
        }

        return LocalCoopSettings.IsActive ? -1 : 0;
    }
}
