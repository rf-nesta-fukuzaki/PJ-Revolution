using System;
using UnityEngine;
using PeakPlunder.Audio;

/// <summary>チェックポイント進捗の読み取り専用サービス（UI 層向け）。</summary>
public interface ICheckpointProgressService
{
    int CurrentCheckpointIndex { get; }
    int TotalCheckpoints { get; }

    event Action<int, string> CheckpointReached;
    event Action<int, int> CheckpointProgressChanged;
    event Action RespawnStarted;
    event Action RespawnCompleted;

    void RegisterCheckpoint(Transform checkpoint);
    void RecordCheckpoint(int index);
}

/// <summary>遠征タイマーの読み取り専用サービス（UI 層向け）。</summary>
public interface IExpeditionTimerService
{
    float ElapsedSeconds { get; }
    bool IsRunning { get; }
    string GetFormattedTime();
    void Stop();
    void Restart();

    event Action<string> FormattedTimeChanged;
    event Action<float> ElapsedChanged;
}

/// <summary>画面フェード / シーン遷移サービス（UI 層向け）。</summary>
public interface ISceneFadeService
{
    void IrisOut();
    void IrisIn();
    void ReloadScene();
}

/// <summary>コンテキストヒント表示サービス（GDD §21.2）。</summary>
public interface IHintService
{
    void TriggerHint(int hintId);
}

/// <summary>色覚サポートパレット（GDD §14.7）。</summary>
public interface IColorBlindPaletteService
{
    ColorBlindMode CurrentMode { get; }
    event Action OnPaletteChanged;
    void SetModeInt(int mode);
    Color GetColor(ColorSlot slot);
}

/// <summary>設定画面サービス（GDD §14.7）。</summary>
public interface ISettingsService
{
    SettingsData Settings { get; }
    float MouseSensitivity { get; }
    bool InvertY { get; }
    bool IsOpen { get; }
    void Open();
    void Close();
    void ApplyAll(SettingsData data);
}

/// <summary>遺物発見通知 UI（GDD §14.9）。</summary>
public interface IRelicDiscoveryNotifier
{
    void NotifyDiscovered(int playerInstanceId, string relicName);
}

/// <summary>BGM / SE 再生サービス（GDD §15）。</summary>
public interface IAudioService
{
    float BgmVolumeScale { get; }
    void PlaySE(SoundId id, Vector3 worldPosition, float volumeScale = 1f);
    void PlaySE2D(SoundId id, float volumeScale = 1f);
    void StopLoop(SoundId id);
    void PlayBGM(AudioClip clip, float volume = 0.5f);
    void StopBGM();
    void SetBGMVolumeScale(float scale);
}

/// <summary>遠征 HUD 表示の共通契約（ExpeditionHUD / HudManager 統合向け）。</summary>
public interface IExpeditionHudReadService
{
    ExpeditionHudSnapshot BuildSnapshot(
        string formattedTime,
        int checkpointIndex,
        int totalCheckpoints,
        float altitudeMeters);
}
