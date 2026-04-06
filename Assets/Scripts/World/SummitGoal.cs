using UnityEngine;

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
        float elapsed = TimerDisplay.Instance != null ? TimerDisplay.Instance.GetElapsedTime() : 0f;
        TimerDisplay.Instance?.Stop();

        // ベストタイムを保存
        float best = PlayerPrefs.GetFloat("BestTime", float.MaxValue);
        if (elapsed < best)
        {
            PlayerPrefs.SetFloat("BestTime", elapsed);
            PlayerPrefs.Save();
        }

        HudManager.Instance?.ShowSummitReached(elapsed);
        AudioManager.Instance?.PlaySE("summit");

        Debug.Log($"[SummitGoal] SUMMIT REACHED! Time: {FormatTime(elapsed)}");
    }

    private string FormatTime(float t)
    {
        int min = Mathf.FloorToInt(t / 60f);
        float sec = t % 60f;
        return $"{min:00}:{sec:00.00}";
    }
}
