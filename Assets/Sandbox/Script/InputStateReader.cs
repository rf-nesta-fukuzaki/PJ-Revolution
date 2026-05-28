using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

/// <summary>
/// Sandbox gameplay 用の軽量入力読み取りヘルパー。
/// InputActionAsset が未配線の開発シーンでも、キーボード/マウス/ゲームパッドから直接読めるようにする。
/// </summary>
public static class InputStateReader
{
    public static Vector2 ReadMoveVectorRaw()
    {
        Vector2 input = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)  input.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)  input.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)    input.y += 1f;
        }

        var gamepad = Gamepad.current;
        if (gamepad != null)
            input += gamepad.leftStick.ReadValue();
#elif ENABLE_LEGACY_INPUT_MANAGER
        input.x = Input.GetAxisRaw("Horizontal");
        input.y = Input.GetAxisRaw("Vertical");
#endif

        return Vector2.ClampMagnitude(input, 1f);
    }

    public static float ReadVerticalAxisRaw()
    {
        float value = 0f;

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) value -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)   value += 1f;
        }

        var gamepad = Gamepad.current;
        if (gamepad != null)
            value += gamepad.leftStick.y.ReadValue();
#elif ENABLE_LEGACY_INPUT_MANAGER
        value = Input.GetAxisRaw("Vertical");
#endif

        return Mathf.Clamp(value, -1f, 1f);
    }

    public static Vector2 ReadLookDelta()
    {
#if ENABLE_INPUT_SYSTEM
        Vector2 delta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;

        var gamepad = Gamepad.current;
        if (gamepad != null)
            delta += gamepad.rightStick.ReadValue() * 8f;

        return delta;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#else
        return Vector2.zero;
#endif
    }

    public static bool IsSprintPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return IsPressed(Keyboard.current?.leftShiftKey)
               || IsPressed(Keyboard.current?.rightShiftKey)
               || IsPressed(Gamepad.current?.leftStickButton);
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#else
        return false;
#endif
    }

    public static bool JumpPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return WasPressedThisFrame(Keyboard.current?.spaceKey)
               || WasPressedThisFrame(Gamepad.current?.buttonSouth);
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Space);
#else
        return false;
#endif
    }

    public static bool PrimaryPointerPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return WasPressedThisFrame(Mouse.current?.leftButton)
               || WasPressedThisFrame(Gamepad.current?.rightTrigger);
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(0);
#else
        return false;
#endif
    }

    public static bool IsSecondaryPointerHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return IsPressed(Mouse.current?.rightButton)
               || IsPressed(Gamepad.current?.leftTrigger);
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButton(1);
#else
        return false;
#endif
    }

    public static bool InteractPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return WasPressedThisFrame(Keyboard.current?.eKey)
               || WasPressedThisFrame(Gamepad.current?.buttonWest);
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.E);
#else
        return false;
#endif
    }

    public static bool UsePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return WasPressedThisFrame(Keyboard.current?.fKey)
               || WasPressedThisFrame(Gamepad.current?.buttonNorth);
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.F);
#else
        return false;
#endif
    }

    public static bool DropPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return WasPressedThisFrame(Keyboard.current?.qKey)
               || WasPressedThisFrame(Gamepad.current?.buttonEast);
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Q);
#else
        return false;
#endif
    }

    public static bool IsAscendPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return IsPressed(Keyboard.current?.spaceKey)
               || IsPressed(Gamepad.current?.rightShoulder);
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.Space);
#else
        return false;
#endif
    }

    public static bool IsDescendPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return IsPressed(Keyboard.current?.leftCtrlKey)
               || IsPressed(Keyboard.current?.cKey)
               || IsPressed(Gamepad.current?.leftShoulder);
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
#else
        return false;
#endif
    }

    public static bool EscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return WasPressedThisFrame(Keyboard.current?.escapeKey)
               || WasPressedThisFrame(Gamepad.current?.startButton);
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static bool IsPressed(ButtonControl button)
    {
        return button != null && button.isPressed;
    }

    private static bool WasPressedThisFrame(ButtonControl button)
    {
        return button != null && button.wasPressedThisFrame;
    }
#endif
}
