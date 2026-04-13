using System;
using System.Reflection;
using UnityEditor;

/// <summary>
/// Unity AI Assistant の既知ノイズログを抑制する。
/// MCP リレー機能は維持したまま、接続失敗時の過剰ログのみ静かにする。
/// </summary>
[InitializeOnLoad]
internal static class UnityAiAssistantNoiseGuard
{
    static UnityAiAssistantNoiseGuard()
    {
        Apply();
        EditorApplication.delayCall += Apply;
    }

    static void Apply()
    {
        SuppressRelayConnectionTraceInConsole();
        SuppressAccountApiTimeoutWarning();
    }

    static void SuppressRelayConnectionTraceInConsole()
    {
        try
        {
            var traceType = FindType("Unity.AI.Tracing.Trace");
            var setCategoryEnabled = traceType?.GetMethod(
                "SetCategoryEnabled",
                BindingFlags.Public | BindingFlags.Static);
            if (setCategoryEnabled == null)
                return;

            setCategoryEnabled.Invoke(null, new object[] { "connection", false });
            setCategoryEnabled.Invoke(null, new object[] { "gateway.connection", false });
        }
        catch
        {
            // Suppression failure should never block editor startup.
        }
    }

    static void SuppressAccountApiTimeoutWarning()
    {
        try
        {
            var apiAccessibleStateType = FindType("Unity.AI.Toolkit.Accounts.Services.States.ApiAccessibleState");
            var hasLoggedWarning = apiAccessibleStateType?.GetField(
                "s_HasLoggedWarning",
                BindingFlags.NonPublic | BindingFlags.Static);
            hasLoggedWarning?.SetValue(null, true);
        }
        catch
        {
            // Suppression failure should never block editor startup.
        }
    }

    static Type FindType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(fullName, throwOnError: false);
            if (type != null)
                return type;
        }

        return null;
    }
}
