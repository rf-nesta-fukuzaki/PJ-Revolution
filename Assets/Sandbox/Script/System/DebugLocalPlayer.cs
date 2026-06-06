using Unity.Netcode;
using UnityEngine;

/// <summary>
/// デバッグメニューが操作対象とする「ローカルプレイヤー」を解決する共有ヘルパー。
/// NetworkObject.IsOwner を最優先し、無ければ最初に登録されたプレイヤーへフォールバックする。
/// 各 OfflineDebugCommands から重複なく参照される（DRY）。
/// </summary>
public static class DebugLocalPlayer
{
    /// <summary>操作対象のプレイヤー体力システム（=プレイヤー root の代表）。</summary>
    public static PlayerHealthSystem Health()
    {
        var players = PlayerHealthSystem.RegisteredPlayers;
        PlayerHealthSystem fallback = null;
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null) continue;
            fallback ??= p;
            var no = p.GetComponent<NetworkObject>();
            if (no != null && no.IsOwner) return p;
        }
        return fallback;
    }

    /// <summary>操作対象プレイヤーのインベントリ（owner 優先・別 root の可能性に備え独自解決）。</summary>
    public static PlayerInventory Inventory()
    {
        var invs = PlayerInventory.RegisteredInventories;
        PlayerInventory fallback = null;
        for (int i = 0; i < invs.Count; i++)
        {
            var inv = invs[i];
            if (inv == null) continue;
            fallback ??= inv;
            var no = inv.GetComponent<NetworkObject>();
            if (no != null && no.IsOwner) return inv;
        }
        return fallback;
    }

    /// <summary>操作対象プレイヤーの root GameObject（解決できなければ null）。</summary>
    public static GameObject Root()
    {
        var health = Health();
        return health != null ? health.gameObject : null;
    }

    /// <summary>プレイヤー root から任意のコンポーネントを取得する（無ければ null）。</summary>
    public static T Component<T>() where T : Component
    {
        var root = Root();
        return root != null ? root.GetComponent<T>() : null;
    }

    public static StaminaSystem Stamina() => Component<StaminaSystem>();

    /// <summary>プレイヤーを指定座標へ安全に移動する（速度ゼロ化 + 物理同期）。</summary>
    public static bool TeleportTo(Vector3 position)
    {
        var root = Root();
        if (root == null) return false;

        var rb = root.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = position;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            root.transform.position = position;
        }

        Physics.SyncTransforms();
        return true;
    }
}
