using System;
using UnityEngine;

namespace Sandbox.UI
{
/// <summary>
/// ゲーム開始から山頂到達までの時間を計測する。
/// mm:ss.ff 形式で表示し、ベストタイムを PlayerPrefs に保存する。
/// 表示層 (HudManager) へはイベントで通知し、ポーリング依存を排除する。
/// </summary>
public class TimerDisplay : MonoBehaviour, IExpeditionTimerService
{
    private static TimerDisplay _instance;

    [System.Obsolete("GameServices.Timer を使用してください")]
    public static TimerDisplay Instance => _instance;

    private float _elapsed;
    private bool _running = true;
    private string _lastFormattedTime = "00:00.00";

    /// <summary>タイマー表示文字列が変化した際に発火。</summary>
    public event Action<string> OnFormattedTimeChanged;

    /// <summary>経過秒数が更新された際に発火（山頂到達などのドメイン処理向け）。</summary>
    public event Action<float> OnElapsedChanged;

    float IExpeditionTimerService.ElapsedSeconds => _elapsed;
    bool IExpeditionTimerService.IsRunning => _running;
    event Action<string> IExpeditionTimerService.FormattedTimeChanged
    {
        add => OnFormattedTimeChanged += value;
        remove => OnFormattedTimeChanged -= value;
    }
    event Action<float> IExpeditionTimerService.ElapsedChanged
    {
        add => OnElapsedChanged += value;
        remove => OnElapsedChanged -= value;
    }

    string IExpeditionTimerService.GetFormattedTime() => GetFormattedTime();
    void IExpeditionTimerService.Stop() => Stop();
    void IExpeditionTimerService.Restart() => Restart();

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        GameServices.Register((IExpeditionTimerService)this);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        if (!_running) return;

        _elapsed += Time.deltaTime;
        OnElapsedChanged?.Invoke(_elapsed);

        string formatted = FormatTime(_elapsed);
        if (formatted == _lastFormattedTime) return;

        _lastFormattedTime = formatted;
        OnFormattedTimeChanged?.Invoke(formatted);
    }

    public void Stop()
    {
        _running = false;
    }

    public void Restart()
    {
        _elapsed = 0f;
        _running = true;
        _lastFormattedTime = FormatTime(_elapsed);
        OnFormattedTimeChanged?.Invoke(_lastFormattedTime);
        OnElapsedChanged?.Invoke(_elapsed);
    }

    public float GetElapsedTime() => _elapsed;

    public string GetFormattedTime() => FormatTime(_elapsed);

    private static string FormatTime(float elapsed)
    {
        int min = Mathf.FloorToInt(elapsed / 60f);
        float sec = elapsed % 60f;
        return $"{min:00}:{sec:00.00}";
    }
}
}
