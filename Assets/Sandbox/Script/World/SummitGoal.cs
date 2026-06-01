using UnityEngine;

namespace Sandbox.World
{
/// <summary>
/// 山頂ゴール。プレイヤーが到達したらゲームクリア。
/// </summary>
public class SummitGoal : MonoBehaviour
{
    private bool _reached;

    private void OnTriggerEnter(Collider other)
    {
        if (_reached) return;
        if (!other.CompareTag("Player")) return;

        _reached = true;
        var timer = GameServices.Timer;
        float elapsed = timer?.ElapsedSeconds ?? 0f;
        timer?.Stop();

        // ベストタイムを保存
        float best = PlayerPrefs.GetFloat("BestTime", float.MaxValue);
        if (elapsed < best)
        {
            PlayerPrefs.SetFloat("BestTime", elapsed);
            PlayerPrefs.Save();
        }

        ExpeditionEvents.RaiseSummitReached(elapsed);
        // L AudioManager 退役済み。山頂 SE は P 側 PeakPlunder.Audio.AudioManager の
        // SoundId 拡張＋シーン配線後に再有効化する。

        Debug.Log($"[SummitGoal] SUMMIT REACHED! Time: {FormatTime(elapsed)}");
    }

    private string FormatTime(float t)
    {
        int min = Mathf.FloorToInt(t / 60f);
        float sec = t % 60f;
        return $"{min:00}:{sec:00.00}";
    }
}
}
