using UnityEngine;

/// <summary>
/// デバッグメニュー — 進行（所持金 / アップグレード / アイテム付与 / 遺物修復）系コマンド。
/// </summary>
public static partial class OfflineDebugCommands
{
    // ── 所持金 ────────────────────────────────────────────────
    public static string AddMoney(int amount)
    {
        CurrencyWallet.Add(amount);
        return $"所持金 +{amount} → {CurrencyWallet.Balance}";
    }

    public static string ResetMoney()
    {
        CurrencyWallet.Reset();
        return "所持金をリセット (0)";
    }

    // ── 恒久アップグレード ────────────────────────────────────
    public static string MaxAllUpgrades()
    {
        UpgradeStore.DebugMaxAll();
        ReapplyUpgradesToLocalPlayer();
        return $"全アップグレードを Lv{UpgradeStore.MaxLevel} に";
    }

    public static string ResetUpgrades()
    {
        UpgradeStore.ResetAll();
        ReapplyUpgradesToLocalPlayer();
        return "アップグレードをリセット";
    }

    /// <summary>恒久アップグレードの効果を全プレイヤーへ即時再適用する。</summary>
    private static void ReapplyUpgradesToLocalPlayer()
    {
        foreach (var p in PlayerHealthSystem.RegisteredPlayers)
            (p != null ? p.GetComponent<PlayerUpgradeApplier>() : null)?.Apply();
    }

    // ── アイテム付与 ──────────────────────────────────────────
    public static string GiveItem(ShopItemType type)
    {
        var inventory = DebugLocalPlayer.Inventory();
        if (inventory == null) return "PlayerInventory なし";

        var go = ItemRuntimeFactory.CreateBaseObject(type);
        if (go == null) return $"{type} の生成に失敗";

        var item = go.GetComponent<ItemBase>();
        if (item == null) { Object.Destroy(go); return $"{type} に ItemBase なし"; }

        if (!inventory.TryAdd(item))
        {
            // 満杯ならプレイヤー足元へドロップして確認できるようにする。
            go.transform.position = inventory.transform.position + Vector3.up * 1f;
            return $"{type} をドロップ（インベントリ満杯）";
        }
        return $"{type} を付与";
    }

    // ── 遺物 ──────────────────────────────────────────────────
    public static string RepairAllRelics()
    {
        var relics = Object.FindObjectsByType<RelicBase>(FindObjectsSortMode.None);
        int n = 0;
        foreach (var relic in relics)
        {
            if (relic == null || relic.IsDestroyed) continue;
            relic.Repair(relic.MaxHp);
            n++;
        }
        return n > 0 ? $"遺物 {n} 個を完全修復" : "修復可能な遺物なし";
    }
}
