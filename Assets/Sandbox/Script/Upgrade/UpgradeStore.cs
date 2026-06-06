using UnityEngine;

/// <summary>
/// 恒久アップグレードのレベル・コスト・効果量を管理する永続ストア（PlayerPrefs）。
/// <see cref="CurrencyWallet"/> の資金で <see cref="UpgradeType"/> を1段階ずつ強化する。
/// </summary>
public static class UpgradeStore
{
    public const int MaxLevel = 5;

    // 1レベルあたりの効果量
    private const float HEALTH_PER_LEVEL  = 25f;  // +HP
    private const float STAMINA_PER_LEVEL = 20f;  // +スタミナ
    private const float SPRINT_PER_LEVEL  = 0.8f; // +m/s

    // 次レベル購入の基準コスト（実コスト = base * (現レベル+1)）
    private const int HEALTH_BASE_COST  = 60;
    private const int STAMINA_BASE_COST = 50;
    private const int SPRINT_BASE_COST  = 70;

    private static string Key(UpgradeType t) => "pp_upgrade_" + t;

    public static int GetLevel(UpgradeType t) => Mathf.Clamp(PlayerPrefs.GetInt(Key(t), 0), 0, MaxLevel);

    public static bool CanUpgrade(UpgradeType t) => GetLevel(t) < MaxLevel;

    /// <summary>次レベル購入に必要な金額。最大レベルなら int.MaxValue。</summary>
    public static int GetCost(UpgradeType t)
    {
        if (!CanUpgrade(t)) return int.MaxValue;
        int nextLevel = GetLevel(t) + 1;
        return BaseCost(t) * nextLevel;
    }

    /// <summary>現在レベルでの累積効果量（applier がプレイヤーへ適用する）。</summary>
    public static float GetBonus(UpgradeType t) => GetLevel(t) * PerLevel(t);

    /// <summary>資金が足りれば1段階購入する。</summary>
    public static bool TryPurchase(UpgradeType t)
    {
        if (!CanUpgrade(t)) return false;
        int cost = GetCost(t);
        if (!CurrencyWallet.TrySpend(cost)) return false;

        SetLevel(t, GetLevel(t) + 1);
        Debug.Log($"[Upgrade] {t} → Lv{GetLevel(t)} (コスト {cost}, 残金 {CurrencyWallet.Balance})");
        return true;
    }

    public static void ResetAll()
    {
        foreach (UpgradeType t in System.Enum.GetValues(typeof(UpgradeType)))
            PlayerPrefs.SetInt(Key(t), 0);
        PlayerPrefs.Save();
    }

    /// <summary>デバッグメニュー用: 全アップグレードを最大レベルにする（資金消費なし）。</summary>
    public static void DebugMaxAll()
    {
        foreach (UpgradeType t in System.Enum.GetValues(typeof(UpgradeType)))
            PlayerPrefs.SetInt(Key(t), MaxLevel);
        PlayerPrefs.Save();
    }

    private static void SetLevel(UpgradeType t, int level)
    {
        PlayerPrefs.SetInt(Key(t), Mathf.Clamp(level, 0, MaxLevel));
        PlayerPrefs.Save();
    }

    private static float PerLevel(UpgradeType t) => t switch
    {
        UpgradeType.MaxHealth   => HEALTH_PER_LEVEL,
        UpgradeType.MaxStamina  => STAMINA_PER_LEVEL,
        UpgradeType.SprintSpeed => SPRINT_PER_LEVEL,
        _ => 0f,
    };

    private static int BaseCost(UpgradeType t) => t switch
    {
        UpgradeType.MaxHealth   => HEALTH_BASE_COST,
        UpgradeType.MaxStamina  => STAMINA_BASE_COST,
        UpgradeType.SprintSpeed => SPRINT_BASE_COST,
        _ => int.MaxValue,
    };
}
