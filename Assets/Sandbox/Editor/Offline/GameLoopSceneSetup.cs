#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PeakPlunder.EditorTools;
using Sandbox.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// タイトル→ロビー→インゲーム→リザルト→ショップ のゲームループを成立させるための
/// シーン整備エディタツール。
///
/// 実行内容（冪等）:
///   1. ショップシーン <c>Shop.unity</c> を生成（無ければ）。<see cref="ShopSceneController"/> を 1 つ置くだけの軽量シーン。
///   2. <c>StartMenu</c> の <see cref="SandboxStartMenu"/>.gameSceneName を空にし、GameFlow 既定（SandboxOfflineCombined）へ委譲。
///   3. <c>SandboxOfflineCombined</c> 内の在シーン <see cref="BasecampShop"/> を無効化（外部ショップシーンへ役割を移譲）。
///   4. Build Settings の先頭を [StartMenu, SandboxOfflineCombined, Shop] の順に整える。
///   5. <see cref="SettingsRuntimeUiBuilder"/> / <see cref="MainMenuRuntimeUiBuilder"/> で
///      Settings・MainMenu のテーマ UI 配線をシーン YAML に永続化する。
///
/// メニュー: Peak Plunder > Game Loop > Setup Game Loop Scenes
/// </summary>
public static class GameLoopSceneSetup
{
    private const string TitleScenePath    = "Assets/Sandbox/Scenes/StartMenu.unity";
    private const string InGameScenePath   = "Assets/Sandbox/Scenes/SandboxOfflineCombined.unity";
    private const string ShopScenePath     = "Assets/Sandbox/Scenes/Shop.unity";
    private const string MainMenuScenePath = "Assets/Sandbox/Scenes/MainMenu.unity";
    private const string OfflineTestPath   = "Assets/Sandbox/Scenes/OfflineTestScene.unity";

    [MenuItem(PeakPlunderEditorMenus.GameLoop.SetupGameLoopScenes)]
    public static void SetupGameLoopScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        CreateOrRefreshShopScene();
        ConfigureTitleScene();
        DisableInSceneShopInCombined();
        RefreshResultScreenInScene(InGameScenePath);
        RefreshResultScreenInScene(OfflineTestPath);
        RefreshSettingsInScene(InGameScenePath);
        RefreshSettingsInScene(OfflineTestPath);
        RefreshMainMenuInScene();
        ConfigureBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[GameLoop] セットアップ完了:\n" +
            $"  Title  = {TitleScenePath}\n" +
            $"  InGame = {InGameScenePath}\n" +
            $"  Shop   = {ShopScenePath}\n" +
            "  Build Settings 先頭 = [StartMenu, SandboxOfflineCombined, Shop]");
    }

    [MenuItem(PeakPlunderEditorMenus.GameLoop.ValidateGameLoop)]
    public static void ValidateGameLoop()
    {
        var problems = new List<string>();

        if (!File.Exists(TitleScenePath))  problems.Add($"タイトルシーンが無い: {TitleScenePath}");
        if (!File.Exists(InGameScenePath)) problems.Add($"インゲームシーンが無い: {InGameScenePath}（Create Combined Scene を先に実行）");
        if (!File.Exists(ShopScenePath))   problems.Add($"ショップシーンが無い: {ShopScenePath}");

        var buildPaths = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();
        foreach (var p in new[] { TitleScenePath, InGameScenePath, ShopScenePath })
        {
            if (!buildPaths.Contains(p))
                problems.Add($"Build Settings 未登録: {p}");
        }

        if (buildPaths.Count > 0 && buildPaths[0] != TitleScenePath)
            problems.Add($"Build Settings 先頭がタイトルシーンでない（現在: {buildPaths[0]}）");

        ValidateSceneHasComponent(TitleScenePath, typeof(SandboxStartMenu), problems);
        ValidateSceneHasComponent(ShopScenePath, typeof(ShopSceneController), problems);

        if (problems.Count == 0)
            Debug.Log("[GameLoop Validate] OK — 3 シーンが揃い、Build Settings・主要コンポーネントも整っています。");
        else
            Debug.LogWarning("[GameLoop Validate] 課題:\n  - " + string.Join("\n  - ", problems));
    }

    // ── 1. ショップシーン生成 ────────────────────────────────
    private static void CreateOrRefreshShopScene()
    {
        var scene = File.Exists(ShopScenePath)
            ? EditorSceneManager.OpenScene(ShopScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var controller = Object.FindFirstObjectByType<ShopSceneController>();
        if (controller == null)
        {
            var go = new GameObject("ShopScene");
            go.AddComponent<ShopSceneController>();
        }

        // 既定シーンの Main Camera は ShopSceneController が再利用するので残す。
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ShopScenePath);
    }

    // ── 2. タイトルシーン設定 ────────────────────────────────
    private static void ConfigureTitleScene()
    {
        if (!File.Exists(TitleScenePath))
        {
            Debug.LogWarning($"[GameLoop] タイトルシーンが見つかりません: {TitleScenePath}");
            return;
        }

        var scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
        var menu = Object.FindFirstObjectByType<SandboxStartMenu>();
        if (menu != null)
        {
            var so = new SerializedObject(menu);
            var prop = so.FindProperty("gameSceneName");
            if (prop != null) prop.stringValue = ""; // GameFlow 既定（SandboxOfflineCombined）へ
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        else
        {
            Debug.LogWarning("[GameLoop] StartMenu に SandboxStartMenu が見つかりません。");
        }
    }

    // ── 3.5 リザルト画面の配線補完 ───────────────────────────
    private static void RefreshResultScreenInScene(string scenePath)
    {
        if (!File.Exists(scenePath)) return;

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var result = Object.FindFirstObjectByType<ResultScreen>(FindObjectsInactive.Include);
        if (result != null)
        {
            ResultScreenRuntimeBuilder.EnsureStructureAndSave(result);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[GameLoop] {scenePath} の ResultScreen 配線を補完・保存しました。");
        }
    }

    // ── 3.6 Settings / MainMenu テーマ UI 永続化 ─────────────
    private static void RefreshSettingsInScene(string scenePath)
    {
        if (!File.Exists(scenePath)) return;

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var settings = Object.FindFirstObjectByType<SettingsManager>(FindObjectsInactive.Include);
        if (settings == null) return;

        SettingsRuntimeUiBuilder.EnsureThemedAndSave(settings);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[GameLoop] {scenePath} の Settings UI を補完・保存しました。");
    }

    private static void RefreshMainMenuInScene()
    {
        if (!File.Exists(MainMenuScenePath)) return;

        var scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        var menu = Object.FindFirstObjectByType<MainMenuManager>(FindObjectsInactive.Include);
        if (menu == null)
        {
            Debug.LogWarning($"[GameLoop] {MainMenuScenePath} に MainMenuManager が見つかりません。");
            return;
        }

        MainMenuRuntimeUiBuilder.EnsureThemedAndSave(menu);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[GameLoop] {MainMenuScenePath} の MainMenu UI を補完・保存しました。");
    }

    private static void ValidateSceneHasComponent(string scenePath, System.Type type, List<string> problems)
    {
        if (!File.Exists(scenePath)) return;
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        bool found = Object.FindFirstObjectByType(type, FindObjectsInactive.Include) != null;
        EditorSceneManager.CloseScene(scene, true);
        if (!found)
            problems.Add($"{scenePath} に {type.Name} がありません。");
    }

    // ── 3. 在シーンショップの無効化 ──────────────────────────
    private static void DisableInSceneShopInCombined()
    {
        if (!File.Exists(InGameScenePath))
        {
            Debug.LogWarning(
                $"[GameLoop] インゲームシーンが見つかりません: {InGameScenePath}\n" +
                "  先に Peak Plunder > Offline > Create Combined Scene を実行してください。");
            return;
        }

        var scene = EditorSceneManager.OpenScene(InGameScenePath, OpenSceneMode.Single);
        var shops = Object.FindObjectsByType<BasecampShop>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int disabled = 0;
        foreach (var shop in shops)
        {
            if (shop.enabled)
            {
                shop.enabled = false; // B キー開閉と在シーン出発を停止（外部ショップへ移譲）
                disabled++;
            }
        }

        if (disabled > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[GameLoop] 在シーン BasecampShop を {disabled} 個無効化しました（外部ショップシーンへ移譲）。");
        }
    }

    // ── 4. Build Settings 整備 ───────────────────────────────
    private static void ConfigureBuildSettings()
    {
        var ordered = new[] { TitleScenePath, InGameScenePath, ShopScenePath };
        var result = new List<EditorBuildSettingsScene>();

        foreach (var path in ordered)
        {
            if (File.Exists(path))
                result.Add(new EditorBuildSettingsScene(path, true));
        }

        // 既存の他シーン（Sandbox など）はループ 3 シーンの後ろへ温存する。
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (!ordered.Contains(s.path))
                result.Add(s);
        }

        EditorBuildSettings.scenes = result.ToArray();
    }
}
#endif
