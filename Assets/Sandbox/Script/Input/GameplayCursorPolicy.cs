using UnityEngine;

/// <summary>
/// プレイ中のマウスカーソル方針を一元管理する。
/// ゲームプレイ: 非表示 + Locked（カメラ操作）
/// メニュー／ポーズ: 表示 + 解放
/// </summary>
public static class GameplayCursorPolicy
{
    private static bool _menuMode;

    public static bool IsMenuMode => _menuMode;

    public static bool AllowsCameraLook => !_menuMode;

    public static void SetGameplayMode()
    {
        _menuMode = false;
        Apply();
    }

    public static void SetMenuMode()
    {
        _menuMode = true;
        Apply();
    }

    public static void ToggleMenuMode()
    {
        if (_menuMode) SetGameplayMode();
        else SetMenuMode();
    }

    /// <summary>他システムが Cursor を触った場合の取りこぼし防止。ローカルプレイヤー毎フレーム呼ぶ。</summary>
    public static void Enforce()
    {
        Apply();
    }

    private static void Apply()
    {
        if (_menuMode)
        {
            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible)
                Cursor.visible = true;
            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
            Cursor.lockState = CursorLockMode.Locked;
        if (Cursor.visible)
            Cursor.visible = false;
    }
}
