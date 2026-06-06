using UnityEngine;

/// <summary>
/// デバッグメニュー — ワールド（天候 / 時間スケール / 敵）系コマンド。
/// </summary>
public static partial class OfflineDebugCommands
{
    // ── 天候 ──────────────────────────────────────────────────
    public static string SetWeather(WeatherType type)
    {
        var weather = GameServices.Weather as WeatherSystem;
        if (weather == null) return "WeatherSystem なし";
        weather.SetWeather(type);
        return $"天候 → {type}";
    }

    // ── 時間スケール ──────────────────────────────────────────
    public static string SetTimeScale(float scale)
    {
        // timeScale のみ変更し fixedDeltaTime は既定のまま（物理解像度を維持しロープ等を安定させる）。
        Time.timeScale = Mathf.Clamp(scale, 0f, 8f);
        return $"時間スケール → x{Time.timeScale:F2}";
    }

    // ── 敵 ────────────────────────────────────────────────────
    public static string SpawnEnemyWave()
    {
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        if (spawner == null)
            spawner = new GameObject("EnemySpawner").AddComponent<EnemySpawner>();
        spawner.DebugSpawnWave();
        return $"敵ウェーブをスポーン（現在 {spawner.ActiveEnemyCount} 体）";
    }

    public static string KillAllEnemies()
    {
        var enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        int n = 0;
        foreach (var e in enemies)
        {
            if (e == null) continue;
            Object.Destroy(e.gameObject);
            n++;
        }

        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        spawner?.DebugDespawnAll();
        return n > 0 ? $"敵 {n} 体を撃破" : "敵はいません";
    }

    public static string StunAllEnemies()
    {
        var enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        int n = 0;
        foreach (var e in enemies)
        {
            if (e == null) continue;
            e.Stun(5f);
            n++;
        }
        return n > 0 ? $"敵 {n} 体をスタン(5s)" : "敵はいません";
    }
}
