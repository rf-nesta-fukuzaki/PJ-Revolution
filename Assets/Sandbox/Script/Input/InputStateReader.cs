using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// New Input System 用の入力ポーリング補助。
/// 旧 Input Manager の主要操作（移動・視点・ジャンプ・クリック）を置き換える。
/// </summary>
public static class InputStateReader
{
    // マウス/トラックパッドのピクセルデルタ → 度 への基準スケール。
    // Mac トラックパッドは 1 フレームのデルタが小さいため、一般的なマウス向けより
    // 大きめに設定する。最終的な感度は ExplorerCameraLook の _sensitivityX/Y で調整する。
    private const float MouseLookScale = 0.1f;
    private const float GamepadLookScale = 2f;
    private const float GamepadLookDeadzone = 0.08f;

    private static Gamepad GetGamepadForSlot(int slot)
    {
        if (!LocalCoopSettings.IsActive)
            return slot == 0 ? Gamepad.current : null;

        if (slot <= 0) return null;

        var rosterMember = LocalCoopRoster.Instance?.GetSlot(slot);
        if (rosterMember != null && rosterMember.AssignedGamepad != null)
            return rosterMember.AssignedGamepad;

        int index = slot - 1;
        return index < Gamepad.all.Count ? Gamepad.all[index] : null;
    }

    private static bool UsesKeyboardForSlot(int slot)
    {
        if (!LocalCoopSettings.IsActive) return true;
        if (slot > 0) return false;

        var host = LocalCoopRoster.Instance?.GetSlot(0);
        return host == null || host.AssignedGamepad == null;
    }

    private static bool UsesMouseForSlot(int slot)
    {
        if (!LocalCoopSettings.IsActive) return true;
        if (slot > 0) return false;

        var host = LocalCoopRoster.Instance?.GetSlot(0);
        return host == null || host.AssignedGamepad == null;
    }

    public static Vector2 ReadMoveVectorRaw(int slot)
    {
        float horizontal = 0f;
        float vertical = 0f;

        if (UsesKeyboardForSlot(slot))
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) vertical -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) vertical += 1f;
            }
        }

        var gamepad = GetGamepadForSlot(slot);
        if (gamepad != null)
        {
            Vector2 stick = gamepad.leftStick.ReadValue();
            if (!LocalCoopSettings.IsActive)
            {
                if (Mathf.Abs(stick.x) > Mathf.Abs(horizontal)) horizontal = stick.x;
                if (Mathf.Abs(stick.y) > Mathf.Abs(vertical)) vertical = stick.y;
            }
            else
            {
                horizontal = stick.x;
                vertical = stick.y;
            }
        }

        return new Vector2(horizontal, vertical);
    }

    public static Vector2 ReadMoveVectorRaw() => ReadMoveVectorRaw(0);

    public static Vector2 ReadLookDelta(int slot)
    {
        Vector2 mouseDelta = Vector2.zero;
        if (UsesMouseForSlot(slot))
        {
            var mouse = Mouse.current;
            if (mouse != null)
                mouseDelta = mouse.delta.ReadValue() * MouseLookScale;
        }

        Vector2 stickDelta = Vector2.zero;
        var gamepad = GetGamepadForSlot(slot);
        if (gamepad != null)
        {
            Vector2 stick = gamepad.rightStick.ReadValue();
            if (stick.sqrMagnitude >= GamepadLookDeadzone * GamepadLookDeadzone)
            {
                float frameCompensation = Time.unscaledDeltaTime * 60f;
                stickDelta = stick * (GamepadLookScale * frameCompensation);
            }
        }

        return mouseDelta + stickDelta;
    }

    public static Vector2 ReadLookDelta() => ReadLookDelta(0);

    /// <summary>視点感度を上げる（] キー）。プレイ中のライブ調整用。</summary>
    public static bool LookSensitivityUpPressedThisFrame()
    {
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard.rightBracketKey.wasPressedThisFrame;
    }

    /// <summary>視点感度を下げる（[ キー）。プレイ中のライブ調整用。</summary>
    public static bool LookSensitivityDownPressedThisFrame()
    {
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard.leftBracketKey.wasPressedThisFrame;
    }

    public static bool IsSprintPressed(int slot)
    {
        if (UsesKeyboardForSlot(slot))
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
                return true;
        }

        var gamepad = GetGamepadForSlot(slot);
        return gamepad != null && gamepad.leftStickButton.isPressed;
    }

    public static bool IsSprintPressed() => IsSprintPressed(0);

    public static bool JumpPressedThisFrame(int slot)
    {
        if (UsesKeyboardForSlot(slot))
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                return true;
        }

        var gamepad = GetGamepadForSlot(slot);
        return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
    }

    public static bool JumpPressedThisFrame() => JumpPressedThisFrame(0);

    public static bool JumpReleasedThisFrame()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasReleasedThisFrame)
            return true;

        var gamepad = Gamepad.current;
        return gamepad != null && gamepad.buttonSouth.wasReleasedThisFrame;
    }

    public static bool IsAscendPressed()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.isPressed)
            return true;

        var gamepad = Gamepad.current;
        return gamepad != null && gamepad.buttonSouth.isPressed;
    }

    public static bool IsDescendPressed()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed || keyboard.cKey.isPressed))
            return true;

        var gamepad = Gamepad.current;
        return gamepad != null && gamepad.buttonEast.isPressed;
    }

    public static bool PrimaryPointerPressedThisFrame()
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            return true;

        var gamepad = Gamepad.current;
        return gamepad != null && gamepad.rightTrigger.wasPressedThisFrame;
    }

    public static bool SecondaryPointerPressedThisFrame()
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            return true;

        var gamepad = Gamepad.current;
        return gamepad != null && gamepad.leftTrigger.wasPressedThisFrame;
    }

    public static bool IsSecondaryPointerHeld()
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed)
            return true;

        var gamepad = Gamepad.current;
        return gamepad != null && gamepad.leftTrigger.ReadValue() > 0.5f;
    }

    public static bool EscapePressedThisFrame()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            return true;

        var gamepad = Gamepad.current;
        return gamepad != null && gamepad.startButton.wasPressedThisFrame;
    }

    /// <summary>ワイヤーロープ操作: R キー / ゲームパッド北ボタンを押している間。</summary>
    public static bool IsWireRopeHeld(int slot)
    {
        if (UsesKeyboardForSlot(slot))
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.isPressed)
                return true;
        }

        var gamepad = GetGamepadForSlot(slot);
        return gamepad != null && gamepad.buttonNorth.isPressed;
    }

    public static bool IsWireRopeHeld() => IsWireRopeHeld(0);

    /// <summary>ワイヤーロープ操作: R キー / ゲームパッド北ボタンを離したフレーム。</summary>
    public static bool WireRopeReleasedThisFrame(int slot)
    {
        if (UsesKeyboardForSlot(slot))
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasReleasedThisFrame)
                return true;
        }

        var gamepad = GetGamepadForSlot(slot);
        return gamepad != null && gamepad.buttonNorth.wasReleasedThisFrame;
    }

    public static bool WireRopeReleasedThisFrame() => WireRopeReleasedThisFrame(0);

    /// <summary>ワイヤーロープ操作: R キー / ゲームパッド北ボタンを押したフレーム（回収開始など）。</summary>
    public static bool WireRopePressedThisFrame(int slot)
    {
        if (UsesKeyboardForSlot(slot))
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                return true;
        }

        var gamepad = GetGamepadForSlot(slot);
        return gamepad != null && gamepad.buttonNorth.wasPressedThisFrame;
    }

    public static bool WireRopePressedThisFrame() => WireRopePressedThisFrame(0);

    [System.Obsolete("Use WireRopePressedThisFrame() instead.")]
    public static bool ReleaseRopePressedThisFrame() => WireRopePressedThisFrame();

    public static bool InteractPressedThisFrame(int slot)
    {
        if (UsesKeyboardForSlot(slot))
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
                return true;
        }

        var gamepad = GetGamepadForSlot(slot);
        return gamepad != null && gamepad.buttonWest.wasPressedThisFrame;
    }

    public static bool InteractPressedThisFrame() => InteractPressedThisFrame(0);

    public static bool UsePressedThisFrame(int slot)
    {
        if (UsesKeyboardForSlot(slot))
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
                return true;
        }

        var gamepad = GetGamepadForSlot(slot);
        return gamepad != null && gamepad.leftShoulder.wasPressedThisFrame;
    }

    public static bool UsePressedThisFrame() => UsePressedThisFrame(0);

    public static bool DropPressedThisFrame(int slot)
    {
        if (UsesKeyboardForSlot(slot))
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.gKey.wasPressedThisFrame)
                return true;
        }

        var gamepad = GetGamepadForSlot(slot);
        return gamepad != null && gamepad.buttonEast.wasPressedThisFrame;
    }

    public static bool DropPressedThisFrame() => DropPressedThisFrame(0);

    /// <summary>GDD §8.3 — X キー / 西ボタン（ウインチケーブル切断）。</summary>
    public static bool CableCutPressedThisFrame(int slot)
    {
        if (UsesKeyboardForSlot(slot))
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.xKey.wasPressedThisFrame)
                return true;
        }

        var gamepad = GetGamepadForSlot(slot);
        return gamepad != null && gamepad.buttonWest.wasPressedThisFrame;
    }

    public static bool CableCutPressedThisFrame() => CableCutPressedThisFrame(0);

    /// <summary>GDD §8 — R キー / 北ボタン（アイテム使用）。ワイヤーロープと共有。</summary>
    public static bool ItemUsePressedThisFrame(int slot)
    {
        if (UsesKeyboardForSlot(slot))
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                return true;
        }

        var gamepad = GetGamepadForSlot(slot);
        return gamepad != null && gamepad.buttonNorth.wasPressedThisFrame;
    }

    public static bool ItemUsePressedThisFrame() => ItemUsePressedThisFrame(0);

    public static bool ItemUseHeld(int slot)
    {
        if (UsesKeyboardForSlot(slot))
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.isPressed)
                return true;
        }

        var gamepad = GetGamepadForSlot(slot);
        return gamepad != null && gamepad.buttonNorth.isPressed;
    }

    public static bool ItemUseHeld() => ItemUseHeld(0);

    public static bool UseHeld(int slot)
    {
        if (UsesKeyboardForSlot(slot))
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.isPressed)
                return true;
        }

        var gamepad = GetGamepadForSlot(slot);
        return gamepad != null && gamepad.leftShoulder.isPressed;
    }

    public static bool UseHeld() => UseHeld(0);

    public static bool InventoryTogglePressedThisFrame(int slot)
    {
        if (UsesKeyboardForSlot(slot))
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
                return true;
        }
        return false;
    }

    public static bool InventoryTogglePressedThisFrame() => InventoryTogglePressedThisFrame(0);

    public static bool QuickSlotPressedThisFrame(int index, int slot)
    {
        if (!UsesKeyboardForSlot(slot)) return false;
        var keyboard = Keyboard.current;
        if (keyboard == null) return false;
        return index switch
        {
            0 => keyboard.digit1Key.wasPressedThisFrame,
            1 => keyboard.digit2Key.wasPressedThisFrame,
            2 => keyboard.digit3Key.wasPressedThisFrame,
            3 => keyboard.digit4Key.wasPressedThisFrame,
            _ => false,
        };
    }

    public static bool ReelPressed()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.eKey.isPressed)
            return true;

        var gamepad = Gamepad.current;
        return gamepad != null && gamepad.rightShoulder.isPressed;
    }

    public static float ReadVerticalAxisRaw()
    {
        return ReadMoveVectorRaw().y;
    }

    // ── 汎用キー / ポインター入力 ─────────────────────────────
    // 旧 Input.GetKeyDown / GetKey / GetKeyUp / GetMouseButton / mousePosition の置換。
    // 任意のキーをポーリングしたいスクリプトはここを経由して新 Input System を使う。

    public static bool KeyPressedThisFrame(Key key)
    {
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard[key].wasPressedThisFrame;
    }

    public static bool KeyHeld(Key key)
    {
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard[key].isPressed;
    }

    public static bool KeyReleasedThisFrame(Key key)
    {
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard[key].wasReleasedThisFrame;
    }

    /// <summary>マウスボタン押下（0=左, 1=右, 2=中）。旧 Input.GetMouseButtonDown 相当。</summary>
    public static bool MouseButtonPressedThisFrame(int button)
    {
        var mouse = Mouse.current;
        if (mouse == null) return false;
        return button switch
        {
            0 => mouse.leftButton.wasPressedThisFrame,
            1 => mouse.rightButton.wasPressedThisFrame,
            2 => mouse.middleButton.wasPressedThisFrame,
            _ => false,
        };
    }

    /// <summary>ポインター（マウス）のスクリーン座標。旧 Input.mousePosition 相当。</summary>
    public static Vector2 PointerPosition()
    {
        var mouse = Mouse.current;
        return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
    }

    /// <summary>ゲームパッド南ボタン（A / ✕）の押下。旧 JoystickButton0 相当。</summary>
    public static bool GamepadSouthPressedThisFrame()
    {
        var gamepad = Gamepad.current;
        return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
    }
}
