using System;

/// <summary>
/// 遠征 HUD 用の読み取り専用スナップショット（プレゼンテーション層向け DTO）。
/// MonoBehaviour に依存せず、テスト可能な純粋 C# モデル。
/// </summary>
public readonly struct ExpeditionHudSnapshot
{
    public ExpeditionHudSnapshot(
        string formattedTime,
        int checkpointIndex,
        int totalCheckpoints,
        float altitudeMeters,
        string transientMessage,
        float transientMessageRemainingSeconds)
    {
        FormattedTime = formattedTime ?? string.Empty;
        CheckpointIndex = checkpointIndex;
        TotalCheckpoints = totalCheckpoints;
        AltitudeMeters = altitudeMeters;
        TransientMessage = transientMessage ?? string.Empty;
        TransientMessageRemainingSeconds = transientMessageRemainingSeconds;
    }

    public string FormattedTime { get; }
    public int CheckpointIndex { get; }
    public int TotalCheckpoints { get; }
    public float AltitudeMeters { get; }
    public string TransientMessage { get; }
    public float TransientMessageRemainingSeconds { get; }

    public string CheckpointLabel =>
        CheckpointIndex >= 0 && TotalCheckpoints > 0
            ? $"Checkpoint {CheckpointIndex + 1}/{TotalCheckpoints}"
            : string.Empty;

    public string AltitudeLabel => $"Alt: {AltitudeMeters:0}m";
}

/// <summary>
/// 遠征 HUD のドメインロジック。入力値から表示用スナップショットを構築する。
/// </summary>
public sealed class ExpeditionHudReadModel : IExpeditionHudReadService
{
    private string _transientMessage = string.Empty;
    private float _transientMessageTimer;

    public event Action<ExpeditionHudSnapshot> OnSnapshotChanged;

    public void SetTransientMessage(string message, float durationSeconds)
    {
        _transientMessage = message ?? string.Empty;
        _transientMessageTimer = durationSeconds > 0f ? durationSeconds : 0f;
    }

    public bool TickTransientMessage(float deltaTime)
    {
        if (_transientMessageTimer <= 0f) return false;

        _transientMessageTimer -= deltaTime;
        if (_transientMessageTimer > 0f) return false;

        _transientMessage = string.Empty;
        return true;
    }

    public ExpeditionHudSnapshot BuildSnapshot(
        string formattedTime,
        int checkpointIndex,
        int totalCheckpoints,
        float altitudeMeters)
    {
        return new ExpeditionHudSnapshot(
            formattedTime,
            checkpointIndex,
            totalCheckpoints,
            altitudeMeters,
            _transientMessage,
            _transientMessageTimer);
    }

    public void Publish(
        string formattedTime,
        int checkpointIndex,
        int totalCheckpoints,
        float altitudeMeters)
    {
        var snapshot = BuildSnapshot(formattedTime, checkpointIndex, totalCheckpoints, altitudeMeters);
        OnSnapshotChanged?.Invoke(snapshot);
    }
}
