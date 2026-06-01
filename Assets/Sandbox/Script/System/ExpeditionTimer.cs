using System;

/// <summary>
/// 遠征タイマーの純粋 C# ドメインロジック。
/// MonoBehaviour から分離し、EditMode テスト可能にする。
/// </summary>
public sealed class ExpeditionTimer
{
    private float _elapsed;
    private bool _isRunning;
    private string _lastFormatted = "00:00.00";

    public float ElapsedSeconds => _elapsed;
    public bool IsRunning => _isRunning;

    public event Action<float> OnElapsedChanged;
    public event Action<string> OnFormattedTimeChanged;

    public void Start()
    {
        _elapsed = 0f;
        _isRunning = true;
        PublishFormattedTime(force: true);
        OnElapsedChanged?.Invoke(_elapsed);
    }

    public void Stop()
    {
        _isRunning = false;
    }

    public void Reset()
    {
        _elapsed = 0f;
        _isRunning = false;
        PublishFormattedTime(force: true);
    }

    public void Tick(float deltaTime)
    {
        Contract.Requires(deltaTime >= 0f, "ExpeditionTimer.Tick: deltaTime は 0 以上でなければならない");

        if (!_isRunning) return;

        _elapsed += deltaTime;
        OnElapsedChanged?.Invoke(_elapsed);
        PublishFormattedTime(force: false);
    }

    public string GetFormattedTime()
    {
        int min = (int)(_elapsed / 60f);
        float sec = _elapsed % 60f;
        return $"{min:00}:{sec:00.00}";
    }

    private void PublishFormattedTime(bool force)
    {
        string formatted = GetFormattedTime();
        if (!force && formatted == _lastFormatted) return;

        _lastFormatted = formatted;
        OnFormattedTimeChanged?.Invoke(formatted);
    }
}
