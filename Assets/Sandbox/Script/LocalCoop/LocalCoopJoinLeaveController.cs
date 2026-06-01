using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ローカル Co-op の後入り（NPC→人間）・後抜け（人間→NPC）を監視する。
/// ゲームパッド接続/切断と Start / Back+Start 入力で切り替える。
/// </summary>
[DefaultExecutionOrder(60)]
public sealed class LocalCoopJoinLeaveController : MonoBehaviour
{
    [Header("手動操作")]
    [Tooltip("未参加パッドの Start で後入り")]
    [SerializeField] private bool _allowManualJoin = true;
    [Tooltip("参加中パッドの Back+Start 長押しで後抜け")]
    [SerializeField] private bool _allowManualLeave = true;
    [SerializeField] private float _leaveHoldSeconds = 1.2f;

    [Header("自動（デバイス抜き差し）")]
    [SerializeField] private bool _autoJoinOnConnect = true;
    [SerializeField] private bool _autoLeaveOnDisconnect = true;

    private SandboxLocalCoopBootstrap _bootstrap;
    private readonly System.Collections.Generic.Dictionary<Gamepad, float> _leaveHoldTimers = new();

    private void OnEnable()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
        var roster = LocalCoopRoster.Instance;
        if (roster != null)
            roster.SlotControlChanged += OnSlotControlChanged;
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        var roster = LocalCoopRoster.Instance;
        if (roster != null)
            roster.SlotControlChanged -= OnSlotControlChanged;
        _leaveHoldTimers.Clear();
    }

    private void Start()
    {
        _bootstrap = SandboxLocalCoopBootstrap.Instance;
        if (LocalCoopSettings.IsOnline)
            enabled = false;
    }

    private void Update()
    {
        if (!LocalCoopSettings.IsActive || _bootstrap == null || !_bootstrap.IsPartyReady) return;

        PollGamepadButtons();
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (!LocalCoopSettings.IsActive || _bootstrap == null || !_bootstrap.IsPartyReady) return;
        if (device is not Gamepad pad) return;

        switch (change)
        {
            case InputDeviceChange.Added:
                if (_autoJoinOnConnect)
                    TryJoinWithGamepad(pad, manual: false);
                break;
            case InputDeviceChange.Removed:
                if (_autoLeaveOnDisconnect)
                    TryLeaveWithGamepad(pad);
                _leaveHoldTimers.Remove(pad);
                break;
        }
    }

    private void PollGamepadButtons()
    {
        PollKeyboardHostLeave();

        foreach (var pad in Gamepad.all)
        {
            if (pad == null) continue;

            var roster = LocalCoopRoster.Instance;
            if (roster == null) continue;

            int occupiedSlot = roster.FindSlotForGamepad(pad);

            if (occupiedSlot < 0)
            {
                _leaveHoldTimers.Remove(pad);
                if (_allowManualJoin && pad.startButton.wasPressedThisFrame)
                    TryJoinWithGamepad(pad, manual: true);
                continue;
            }

            if (!_allowManualLeave || occupiedSlot <= 0) continue;

            bool leaveRequested = pad.selectButton.isPressed && pad.startButton.isPressed;
            if (!leaveRequested)
            {
                _leaveHoldTimers.Remove(pad);
                continue;
            }

            if (!_leaveHoldTimers.ContainsKey(pad))
                _leaveHoldTimers[pad] = Time.unscaledTime;

            if (Time.unscaledTime - _leaveHoldTimers[pad] >= _leaveHoldSeconds)
            {
                _leaveHoldTimers.Remove(pad);
                _bootstrap.TryDemoteSlot(occupiedSlot);
            }
        }
    }

    private void PollKeyboardHostLeave()
    {
        if (_bootstrap == null) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        var roster = LocalCoopRoster.Instance;
        var host = roster?.GetSlot(0);
        if (host == null) return;

        if (!host.IsHumanControlled && keyboard.enterKey.wasPressedThisFrame)
        {
            _bootstrap.TryPromoteSlot(0, gamepad: null);
            return;
        }

        if (!_allowManualLeave || !keyboard.endKey.wasPressedThisFrame) return;

        if (host.IsHumanControlled)
            _bootstrap.TryDemoteSlot(0);
    }

    private void TryJoinWithGamepad(Gamepad pad, bool manual)
    {
        if (pad == null || _bootstrap == null) return;

        var roster = LocalCoopRoster.Instance;
        if (roster == null) return;

        if (roster.FindSlotForGamepad(pad) >= 0) return;

        var hostSlot = roster.GetSlot(0);
        if (hostSlot != null && !hostSlot.IsHumanControlled)
        {
            _bootstrap.TryPromoteSlot(0, pad);
            return;
        }

        int gamepadIndex = IndexOfGamepad(pad);
        if (gamepadIndex < 0) return;

        int preferredSlot = gamepadIndex + 1;
        if (preferredSlot >= LocalCoopSettings.MaxPartySize) return;

        var preferred = roster.GetSlot(preferredSlot);
        if (preferred != null && !preferred.IsHumanControlled)
        {
            _bootstrap.TryPromoteSlot(preferredSlot, pad);
            return;
        }

        int npcSlot = roster.FindFirstNpcSlot();
        if (npcSlot < 0) return;

        if (manual || npcSlot == preferredSlot || gamepadIndex == 0)
            _bootstrap.TryPromoteSlot(npcSlot, pad);
    }

    private void TryLeaveWithGamepad(Gamepad pad)
    {
        if (pad == null || _bootstrap == null) return;

        int slot = LocalCoopRoster.Instance?.FindSlotForGamepad(pad) ?? -1;
        if (slot <= 0) return;

        _bootstrap.TryDemoteSlot(slot);
    }

    private static int IndexOfGamepad(Gamepad pad)
    {
        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            if (Gamepad.all[i] == pad)
                return i;
        }
        return -1;
    }

    private void OnSlotControlChanged(int slot, bool isHuman)
    {
        _bootstrap?.RefreshPresentation();
    }
}
