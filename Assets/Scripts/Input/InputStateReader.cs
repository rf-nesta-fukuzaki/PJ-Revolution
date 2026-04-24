using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// New Input System 用の入力ポーリング補助。
/// 旧 Input Manager の主要操作（移動・視点・ジャンプ・クリック）を置き換える。
/// </summary>
public static class InputStateReader
{
    private const float MouseLookScale = 0.02f;
    private const float GamepadLookScale = 2f;

    public static Vector2 ReadMoveVectorRaw()
    {
        float horizontal = 0f;
        float vertical = 0f;

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) vertical -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) vertical += 1f;
        }

        var gamepad = Gamepad.current;
        if (gamepad != null)
        {
            Vector2 stick = gamepad.leftStick.ReadValue();
            if (Mathf.Abs(stick.x) > Mathf.Abs(horizontal)) horizontal = stick.x;
            if (Mathf.Abs(stick.y) > Mathf.Abs(vertical)) vertical = stick.y;
        }

        return new Vector2(horizontal, vertical);
    }

    public static Vector2 ReadLookDelta()
    {
        Vector2 mouseDelta = Vector2.zero;
        var mouse = Mouse.current;
        if (mouse != null)
            mouseDelta = mouse.delta.ReadValue() * MouseLookScale;

        Vector2 stickDelta = Vector2.zero;
        var gamepad = Gamepad.current;
        if (gamepad != null)
            stickDelta = gamepad.rightStick.ReadValue() * GamepadLookScale;

        return mouseDelta + stickDelta;
    }

    public static bool IsSprintPressed()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
            return true;

        var gamepad = Gamepad.current;
        return gamepad != null && gamepad.leftStickButton.isPressed;
    }

    public static bool JumpPressedThisFrame()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            return true;

        var gamepad = Gamepad.current;
        return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
    }

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

    public static bool ReleaseRopePressedThisFrame()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            return true;

        var gamepad = Gamepad.current;
        return gamepad != null && gamepad.buttonNorth.wasPressedThisFrame;
    }

    public static bool InteractPressedThisFrame()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            return true;

        var gamepad = Gamepad.current;
        return gamepad != null && gamepad.buttonWest.wasPressedThisFrame;
    }

    public static bool UsePressedThisFrame()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
            return true;

        var gamepad = Gamepad.current;
        return gamepad != null && gamepad.leftShoulder.wasPressedThisFrame;
    }

    public static bool DropPressedThisFrame()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.gKey.wasPressedThisFrame)
            return true;

        var gamepad = Gamepad.current;
        return gamepad != null && gamepad.buttonEast.wasPressedThisFrame;
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
}
