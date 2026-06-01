using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// 契約プログラミング (Design by Contract) の共通ユーティリティ。
/// 事前条件・事後条件・不変条件を一箇所で検証し、フェイルファストを徹底する。
/// </summary>
public static class Contract
{
    /// <summary>事前条件: 条件が真でなければ ArgumentException を送出する。</summary>
    [Conditional("UNITY_ASSERTIONS")]
    public static void Requires(bool condition, string message)
    {
        if (condition) return;
        throw new ArgumentException(message);
    }

    /// <summary>事前条件: 参照が null でなければ ArgumentNullException を送出する。</summary>
    [Conditional("UNITY_ASSERTIONS")]
    public static void RequiresNotNull(object value, string paramName)
    {
        if (value != null) return;
        throw new ArgumentNullException(paramName);
    }

    /// <summary>事後条件: 条件が偽なら Debug.Assert で検出する。</summary>
    [Conditional("UNITY_ASSERTIONS")]
    public static void Ensures(bool condition, string message)
    {
        Debug.Assert(condition, $"[Contract] {message}");
    }

    /// <summary>不変条件: 条件が偽なら Debug.Assert で検出する。</summary>
    [Conditional("UNITY_ASSERTIONS")]
    public static void Invariant(bool condition, string message)
    {
        Debug.Assert(condition, $"[Contract] Invariant violated: {message}");
    }

    /// <summary>実行時フェイルファスト: 条件が偽ならエラーログ後に false を返す（例外を投げない）。</summary>
    public static bool TryRequires(bool condition, string message)
    {
        if (condition) return true;
        Debug.LogError($"[Contract] {message}");
        return false;
    }
}
