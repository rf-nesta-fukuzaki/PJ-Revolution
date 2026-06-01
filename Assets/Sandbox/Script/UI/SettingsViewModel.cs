using UnityEngine;

/// <summary>
/// 設定画面のドメインロジック（読み込み・保存・適用の orchestration）。
/// </summary>
public sealed class SettingsViewModel
{
    public SettingsData Current { get; private set; }
    public SettingsGameplaySnapshot GameplaySnapshot { get; private set; } = SettingsGameplaySnapshot.Default;
    public bool HasGameplaySnapshot { get; private set; }

    public SettingsData Load(string persistentDataPath)
    {
        if (SettingsJsonStore.TryLoad(persistentDataPath, out var loaded, out var gameplay))
        {
            GameplaySnapshot = gameplay;
            HasGameplaySnapshot = true;
            SettingsStorage.SaveToPlayerPrefs(loaded);
            Current = loaded;
            return loaded;
        }

        GameplaySnapshot = SettingsGameplaySnapshot.Default;
        HasGameplaySnapshot = false;
        Current = SettingsStorage.LoadFromPlayerPrefs();
        return Current;
    }

    public void Save(string persistentDataPath, SettingsData data)
    {
        Contract.Requires(!string.IsNullOrEmpty(persistentDataPath), "persistentDataPath が空です");

        Current = data;
        SettingsStorage.SaveToPlayerPrefs(data);

        string playerDisplayName = GameServices.Save != null
            ? GameServices.Save.PlayerDisplayName
            : string.Empty;
        bool tutorialHintsEnabled = GameServices.Save == null
            || GameServices.Save.IsTutorialHintsEnabled();

        SettingsJsonStore.Save(persistentDataPath, data, playerDisplayName, tutorialHintsEnabled);
    }

    public void ApplyGameplaySnapshotToProfile()
    {
        if (!HasGameplaySnapshot || GameServices.Save == null) return;

        string loadedName = GameplaySnapshot.PlayerDisplayName;
        if (!string.IsNullOrWhiteSpace(loadedName))
            GameServices.Save.PlayerDisplayName = loadedName;

        GameServices.Save.SetTutorialHintsEnabled(GameplaySnapshot.TutorialHintsEnabled);
    }

    public void ApplyCoreSettings(SettingsData data, IColorBlindPaletteService colorBlind)
    {
        SettingsApplier.ApplyGraphics(data);
        SettingsApplier.ApplyUiScale(data.uiScale);
        colorBlind?.SetModeInt(data.colorBlindMode);
    }
}
