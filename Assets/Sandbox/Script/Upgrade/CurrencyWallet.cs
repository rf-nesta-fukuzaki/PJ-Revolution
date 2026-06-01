using UnityEngine;

/// <summary>
/// ラン間で持ち越される永続マネー（R.E.P.O. の所持金）。
/// 抽出成功で加算され、恒久アップグレード購入で消費する。PlayerPrefs に永続化。
/// </summary>
public static class CurrencyWallet
{
    private const string KEY = "pp_wallet_balance";

    public static int Balance => PlayerPrefs.GetInt(KEY, 0);

    public static void Add(int amount)
    {
        if (amount <= 0) return;
        PlayerPrefs.SetInt(KEY, Balance + amount);
        PlayerPrefs.Save();
    }

    public static bool TrySpend(int amount)
    {
        if (amount < 0) return false;
        if (Balance < amount) return false;
        PlayerPrefs.SetInt(KEY, Balance - amount);
        PlayerPrefs.Save();
        return true;
    }

    /// <summary>テスト/新規ゲーム用にリセット。</summary>
    public static void Reset()
    {
        PlayerPrefs.SetInt(KEY, 0);
        PlayerPrefs.Save();
    }
}
