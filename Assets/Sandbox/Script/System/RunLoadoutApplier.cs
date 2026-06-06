using System.Collections;
using UnityEngine;

/// <summary>
/// 遠征開始時に <see cref="RunLoadout"/> の持ち越しアイテムをプレイヤーへ付与する常駐リスナー。
///
/// シーンへの手動配置は不要。<see cref="Bootstrap"/> がゲーム起動時に DontDestroyOnLoad で 1 つ生成し、
/// <see cref="ExpeditionEvents.OnExpeditionStarted"/> を購読する。
/// プレイヤー（PlayerInventory）の登録が遅れる場合に備え、最大数秒だけ待ってから付与する。
/// </summary>
public sealed class RunLoadoutApplier : MonoBehaviour
{
    private const float MaxWaitSeconds = 5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // 多重生成防止。
        if (FindFirstObjectByType<RunLoadoutApplier>() != null) return;

        var go = new GameObject("[RunLoadoutApplier]");
        go.AddComponent<RunLoadoutApplier>();
        DontDestroyOnLoad(go);
    }

    private void OnEnable()  => ExpeditionEvents.OnExpeditionStarted += HandleExpeditionStarted;
    private void OnDisable() => ExpeditionEvents.OnExpeditionStarted -= HandleExpeditionStarted;

    private void HandleExpeditionStarted()
    {
        if (!RunLoadout.HasPending) return;
        StartCoroutine(ApplyWhenReady());
    }

    private IEnumerator ApplyWhenReady()
    {
        float elapsed = 0f;
        while (elapsed < MaxWaitSeconds)
        {
            var inv = PlayerInventory.RegisteredInventories;
            if (inv != null && inv.Count > 0) break;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        RunLoadout.ConsumeAndSpawn();
    }
}
