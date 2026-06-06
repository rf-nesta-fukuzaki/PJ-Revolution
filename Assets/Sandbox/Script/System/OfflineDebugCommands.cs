using UnityEngine;

/// <summary>
/// OfflineTestScene 用デバッグコマンド（F1〜F9 + 拡張デバッグメニュー）。
/// MonoBehaviour から分離した純粋 C# ハンドラ。カテゴリごとに partial 分割している
/// （Player / World / Progression）。
/// </summary>
public static partial class OfflineDebugCommands
{
    public static string KillLocalPlayer()
    {
        var health = Object.FindFirstObjectByType<PlayerHealthSystem>();
        if (health == null) return "PlayerHealthSystem なし";
        health.TakeDamage(health.MaxHp);
        return "F1: プレイヤー即死";
    }

    public static string CallHelicopter()
    {
        var heli = Object.FindFirstObjectByType<HelicopterController>();
        if (heli == null) return "HelicopterController なし";

        var player = Object.FindFirstObjectByType<PlayerHealthSystem>();
        Vector3 origin = player != null ? player.transform.position : Vector3.up * 30f;
        heli.CallHelicopter(origin);
        return "F2: ヘリ呼び出し";
    }

    public static string ForceReturn()
    {
        var expedition = GameServices.Expedition;
        if (expedition == null) return "ExpeditionManager なし";
        if (expedition.Phase != ExpeditionPhase.Climbing)
            return $"F3: 帰還不可（現在フェーズ: {expedition.Phase}）";

        expedition.ReturnToBase(true);
        return "F3: 強制帰還";
    }

    public static string DamageFirstRelic()
    {
        var relic = Object.FindFirstObjectByType<RelicBase>();
        if (relic == null) return "RelicBase なし";
        relic.ApplyDamage(25f);
        return $"F4: {relic.RelicName} に 25 ダメージ";
    }

    public static string ForceStartExpedition()
    {
        var expedition = GameServices.Expedition;
        if (expedition == null) return "ExpeditionManager なし";
        if (expedition.Phase != ExpeditionPhase.Basecamp)
            return $"F5: 開始不可（現在フェーズ: {expedition.Phase}）";

        expedition.StartExpedition();
        return "F5: 遠征強制開始";
    }

    public static string MoveRelicsToReturnZone()
    {
        var returnZone = Object.FindFirstObjectByType<ReturnZone>();
        if (returnZone == null) return "ReturnZone なし";

        var relics = Object.FindObjectsByType<RelicBase>(FindObjectsSortMode.None);
        if (relics.Length == 0) return "遺物なし";

        Vector3 zonePos = returnZone.transform.position + Vector3.up * 1f;
        for (int i = 0; i < relics.Length; i++)
        {
            if (relics[i] == null) continue;
            relics[i].transform.position = zonePos + Vector3.right * (i * 1.5f);
        }

        return $"F6: 遺物 {relics.Length} 個を ReturnZone 前へ移動";
    }

    public static string ReviveGhost()
    {
        var shrines = Object.FindObjectsByType<ReviveShrine>(FindObjectsSortMode.None);
        if (shrines.Length == 0) return "ReviveShrine なし";

        foreach (var shrine in shrines)
        {
            if (!shrine.IsAvailable) continue;
            shrine.Use();

            var ghost = Object.FindFirstObjectByType<GhostSystem>();
            if (ghost != null && ghost.IsGhost)
            {
                ghost.GetComponent<PlayerStateMachine>()?.Transition(PlayerState.Alive);
                ghost.GetComponent<PlayerHealthSystem>()?.Revive(50f);
            }

            return $"F7: {shrine.gameObject.name} で復活";
        }

        return "F7: 使用可能な祠がありません";
    }

    public static string CycleWeather()
    {
        var weather = GameServices.Weather as WeatherSystem;
        if (weather == null) return "WeatherSystem なし";
        weather.CycleToNextWeather();
        return $"F8: 天候切り替え → {weather.CurrentWeather}";
    }

    public static string DrainLocalStamina()
    {
        var stamina = Object.FindFirstObjectByType<StaminaSystem>();
        if (stamina == null) return "StaminaSystem なし";
        stamina.ConsumeAll();
        return "F9: スタミナゼロ";
    }

    // ── ジップライン（拠点⇄チェックポイント） ─────────────────
    /// <summary>指定番号(0始まり)のチェックポイントを強制解放し、拠点へのジップラインを開通させる。</summary>
    public static string OpenZipline(int index)
    {
        var cps = Object.FindObjectsByType<Sandbox.World.Zipline.ZiplineCheckpoint>(FindObjectsSortMode.None);
        if (cps.Length == 0) return "ジップラインCP未配置（地形生成待ち？）";

        foreach (var cp in cps)
        {
            if (cp == null || cp.Index != index) continue;
            if (cp.IsReached) return $"CP{index + 1}: 既に開通済み";
            cp.ForceReach();
            return $"CP{index + 1} 解放 → 拠点ジップライン開通";
        }
        return $"CP{index + 1} が見つかりません（配置数 {cps.Length}）";
    }

    /// <summary>全チェックポイントを強制解放し、全ジップラインを開通させる。</summary>
    public static string OpenAllZiplines()
    {
        var cps = Object.FindObjectsByType<Sandbox.World.Zipline.ZiplineCheckpoint>(FindObjectsSortMode.None);
        if (cps.Length == 0) return "ジップラインCP未配置（地形生成待ち？）";

        int opened = 0;
        foreach (var cp in cps)
        {
            if (cp == null || cp.IsReached) continue;
            cp.ForceReach();
            opened++;
        }
        return opened > 0 ? $"ジップライン {opened} 本 開通" : "全ジップライン開通済み";
    }
}
