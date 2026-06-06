using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ショップ（独立シーン）で購入したアイテムを、次のインゲームシーンへ持ち越すための
/// ラン横断バッファ。シーンをまたいで生存させたいので static に保持する。
///
/// 流れ:
///   1. ショップで「出発」 → <see cref="SetFromCounts"/> で購入内容を記録。
///   2. インゲーム到着 → 遠征開始時に <see cref="RunLoadoutApplier"/> が <see cref="ConsumeAndSpawn"/> を呼び、
///      購入アイテムをプレイヤーのインベントリ / 足元へ実体化してからバッファをクリア。
///   3. タイトルへ戻る場合は <see cref="GameFlow.ResetRun"/> 経由で <see cref="Clear"/> される。
/// </summary>
public static class RunLoadout
{
    // 購入アイテム ID → 個数
    private static readonly Dictionary<string, int> _pending = new();

    /// <summary>持ち越し予定のアイテム種類数（個数の合計ではない）。</summary>
    public static int PendingTypeCount => _pending.Count;

    /// <summary>1 つでも持ち越し予定があるか。</summary>
    public static bool HasPending
    {
        get
        {
            foreach (var kv in _pending)
                if (kv.Value > 0) return true;
            return false;
        }
    }

    /// <summary>購入結果（itemId→個数）でバッファを置き換える。</summary>
    public static void SetFromCounts(IReadOnlyDictionary<string, int> counts)
    {
        _pending.Clear();
        if (counts == null) return;
        foreach (var kv in counts)
        {
            if (kv.Value > 0)
                _pending[kv.Key] = kv.Value;
        }
    }

    public static void Add(string itemId, int count = 1)
    {
        if (string.IsNullOrEmpty(itemId) || count <= 0) return;
        _pending.TryGetValue(itemId, out int cur);
        _pending[itemId] = cur + count;
    }

    public static void Clear() => _pending.Clear();

    /// <summary>
    /// バッファ内のアイテムを実体化してプレイヤーへ渡し、バッファを空にする。
    /// 失敗してもバッファはクリアする（二重付与を防ぐため）。
    /// </summary>
    public static void ConsumeAndSpawn()
    {
        if (_pending.Count == 0) return;

        int spawned = 0;
        foreach (var kv in _pending)
        {
            for (int i = 0; i < kv.Value; i++)
            {
                if (BasecampLoadoutSpawner.SpawnToPlayer(kv.Key))
                    spawned++;
            }
        }

        Debug.Log($"[RunLoadout] 持ち越しアイテムを {spawned} 個プレイヤーへ付与しました。");
        _pending.Clear();
    }
}
