using UnityEngine;

/// <summary>
/// デバッグメニュー — プレイヤー（HP / 気力 / 状態 / テレポート）系コマンド。
/// すべて <see cref="DebugLocalPlayer"/> 経由で操作対象を解決する。
/// </summary>
public static partial class OfflineDebugCommands
{
    private static bool _godMode;
    private static bool _oxygenForced;

    // ── 体力 ──────────────────────────────────────────────────
    public static string HealLocalPlayerFull()
    {
        var health = DebugLocalPlayer.Health();
        if (health == null) return "PlayerHealthSystem なし";
        if (health.IsDowned) health.ReviveFromDowned(health.MaxHp);
        health.Heal(health.MaxHp);
        return $"HP 全回復 ({health.MaxHp:F0})";
    }

    public static string DamageLocalPlayer(float amount)
    {
        var health = DebugLocalPlayer.Health();
        if (health == null) return "PlayerHealthSystem なし";
        health.TakeDamage(amount);
        return $"HP -{amount:F0} → {health.CurrentHp:F0}/{health.MaxHp:F0}";
    }

    public static string ToggleGodMode()
    {
        var health = DebugLocalPlayer.Health();
        if (health == null) return "PlayerHealthSystem なし";
        _godMode = !_godMode;
        // 被ダメージ軽減を 100%/0% に切り替える（役割の軽減値は上書きされる点に注意）。
        health.SetDamageResistance(_godMode ? 1f : 0f);
        if (_godMode) health.Heal(health.MaxHp);
        return _godMode ? "無敵モード ON" : "無敵モード OFF";
    }

    // ── 死亡 / 蘇生 ───────────────────────────────────────────
    public static string ForceDownLocalPlayer()
    {
        var health = DebugLocalPlayer.Health();
        if (health == null) return "PlayerHealthSystem なし";
        if (_godMode) health.SetDamageResistance(0f);   // 無敵中でもダウンを通す
        health.TakeDamage(health.MaxHp);
        if (_godMode) health.SetDamageResistance(1f);
        return health.IsDowned ? "ダウン状態へ（蘇生可能）" : "ダウン/死亡へ遷移";
    }

    public static string ReviveLocalFromDowned()
    {
        var health = DebugLocalPlayer.Health();
        if (health == null) return "PlayerHealthSystem なし";
        if (!health.IsDowned) return "ダウン状態ではありません";
        health.ReviveFromDowned(health.MaxHp * 0.5f);
        return "ダウンから蘇生 (HP50%)";
    }

    public static string FinalizeLocalDeath()
    {
        var health = DebugLocalPlayer.Health();
        if (health == null) return "PlayerHealthSystem なし";
        health.FinalizeDeath();
        return "完全死亡 → 偵察幽霊へ";
    }

    public static string ReviveLocalPlayer()
    {
        var health = DebugLocalPlayer.Health();
        if (health == null) return "PlayerHealthSystem なし";
        health.Revive(health.MaxHp);
        DebugLocalPlayer.Component<PlayerStateMachine>()?.Transition(PlayerState.Alive);
        return "幽霊/死亡から復活 (HP満タン)";
    }

    // ── 気力 ──────────────────────────────────────────────────
    public static string RefillLocalStamina()
    {
        var stamina = DebugLocalPlayer.Stamina();
        if (stamina == null) return "StaminaSystem なし";
        stamina.Recover(stamina.MaxStamina);
        return $"気力 全回復 ({stamina.MaxStamina:F0})";
    }

    // ── 酸素 / 高山病 ─────────────────────────────────────────
    public static string ToggleOxygenTank()
    {
        var stamina  = DebugLocalPlayer.Stamina();
        var sickness = DebugLocalPlayer.Component<AltitudeSicknessEffect>();
        if (stamina == null && sickness == null) return "対象プレイヤーなし";

        _oxygenForced = !_oxygenForced;
        if (stamina != null)  stamina.HasOxygenTank = _oxygenForced;
        sickness?.SetOxygenTankActive(_oxygenForced);
        return _oxygenForced ? "酸素タンク ON（高山病防止）" : "酸素タンク OFF";
    }

    // ── テレポート ────────────────────────────────────────────
    public static string TeleportToBasecamp()
    {
        Vector3 pos = ResolveBasecampPosition();
        return DebugLocalPlayer.TeleportTo(pos)
            ? $"拠点へテレポート ({pos.y:F0}m)"
            : "対象プレイヤーなし";
    }

    public static string TeleportToSummit()
    {
        if (!DebugLocalPlayer.TeleportTo(Vector3.zero)) return "対象プレイヤーなし";

        var root = DebugLocalPlayer.Root();
        Vector3 xz = root.transform.position;
        float summitY = MountainProfile.IsReady ? MountainProfile.SummitY : 460f;

        // 山頂 XZ 付近を真上からレイキャストして接地点を拾う。失敗時は SummitY+5m。
        Vector3 origin = new Vector3(xz.x, summitY + 200f, xz.z);
        Vector3 target = new Vector3(xz.x, summitY + 5f, xz.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 400f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            target = hit.point + Vector3.up * 2f;

        DebugLocalPlayer.TeleportTo(target);
        return $"山頂付近へテレポート ({target.y:F0}m)";
    }

    public static string TeleportToCheckpoint(int index)
    {
        var cps = Object.FindObjectsByType<Sandbox.World.Zipline.ZiplineCheckpoint>(FindObjectsSortMode.None);
        foreach (var cp in cps)
        {
            if (cp == null || cp.Index != index) continue;
            Vector3 pos = cp.transform.position + Vector3.up * 2f;
            DebugLocalPlayer.TeleportTo(pos);
            return $"CP{index + 1} へテレポート ({pos.y:F0}m)";
        }
        return $"CP{index + 1} が見つかりません";
    }

    public static string TeleportUp(float meters)
    {
        var root = DebugLocalPlayer.Root();
        if (root == null) return "対象プレイヤーなし";
        DebugLocalPlayer.TeleportTo(root.transform.position + Vector3.up * meters);
        return $"上方へ {meters:F0}m テレポート";
    }

    private static Vector3 ResolveBasecampPosition()
    {
        var points = Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        foreach (var sp in points)
        {
            if (sp == null) continue;
            if (sp.Layer == SpawnLayer.Route ||
                sp.name.Contains("Basecamp", System.StringComparison.OrdinalIgnoreCase))
                return sp.transform.position + Vector3.up * 1.5f;
        }

        var zone = Object.FindFirstObjectByType<ReturnZone>();
        if (zone != null) return zone.transform.position + Vector3.up * 1.5f;

        return new Vector3(0f, 2f, -130f);
    }
}
