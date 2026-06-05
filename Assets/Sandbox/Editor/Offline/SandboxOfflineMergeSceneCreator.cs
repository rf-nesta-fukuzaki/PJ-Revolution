using PeakPlunder.EditorTools;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sandbox.World;
using Sandbox.World.Integration;

/// <summary>
/// OfflineTestScene.unity（完成オフラインゲームループ）と Sandbox.unity（手続き的 AAA 地形＋大気）
/// を統合した新シーン SandboxOfflineCombined.unity を自動生成するエディタスクリプト。
///
/// 統合方針（= "ループ on 地形"）:
///   - 土台 ……… OfflineTestScene を丸ごと採用（NGO オフライン Host・UI・Shop・プレイヤー・各 GameSystems・
///                RenderSettings/ライティングも継承）。改変は最小限。
///   - 環境 ……… Sandbox から TerrainGenerator GameObject だけを移植し、手続き地形＋大気ビジュアルを載せる。
///   - 重複排除 … Sandbox 側のカメラ(CameraRig/Camera)・Explorer・CheckpointSystem・Ground・テスト用 GO・
///                Directional Light は破棄（OfflineTest 側を正とする）。
///   - 並行マネージャ抑制 … TerrainGenerator 同居の SandboxBootstrap を
///                autoAttachSpawners=false（ScoreTracker 等の並行ゲームプレイ層を注入しない）/
///                autoAttachAtmosphere=true（大気/空/雲/時刻サイクル/標高別カラーは載せる）に設定。
///                大気の AtmosphericProfileController は sun==null 時に FindMainDirectional() で
///                OfflineTest 側 Directional Light を自動取得するため、ライトは 1 個で足りる。
///   - TerrainGenerator は autoUseMainCamera=true で OfflineTest の MainCamera を追従しチャンク生成。
///   - OfflineTest のフラット Ground（y=0 の平面）と境界 Wall_* は無効化し、Sandbox 地形を唯一の地面にする。
///   - CombinedTerrainConformer を付与し、地形ベイク後にゲームプレイ層を地形面へスナップ＆プレイヤーを地表へ配置。
///
/// 重要:
///   元の OfflineTestScene.unity / Sandbox.unity はディスク上では一切変更しない
///   （メモリ上でのみマージし、保存先は COMBINED_PATH のみ）。
///
/// 使い方:
///   Unity メニュー → Peak Plunder → Offline → Create Combined Scene
/// </summary>
public static class SandboxOfflineMergeSceneCreator
{
    private const string OFFLINE_PATH  = "Assets/Sandbox/Scenes/OfflineTestScene.unity";
    private const string SANDBOX_PATH  = "Assets/Sandbox/Scenes/Sandbox.unity";
    private const string COMBINED_PATH = "Assets/Sandbox/Scenes/SandboxOfflineCombined.unity";

    [MenuItem(PeakPlunderEditorMenus.Offline.CreateCombinedScene)]
    public static void CreateCombinedScene()
    {
        // 現在の未保存シーンの扱いを確認
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (!File.Exists(OFFLINE_PATH))
        {
            Debug.LogError($"[Combine] ソースが見つかりません: {OFFLINE_PATH}");
            return;
        }
        if (!File.Exists(SANDBOX_PATH))
        {
            Debug.LogError($"[Combine] ソースが見つかりません: {SANDBOX_PATH}");
            return;
        }
        if (File.Exists(COMBINED_PATH))
        {
            bool ok = EditorUtility.DisplayDialog(
                "SandboxOfflineCombined",
                $"{COMBINED_PATH} は既に存在します。上書き再生成しますか？",
                "上書き", "キャンセル");
            if (!ok) return;
        }

        // 1. OfflineTestScene を Single で開く（土台。RenderSettings/Lighting も継承）。
        var target = EditorSceneManager.OpenScene(OFFLINE_PATH, OpenSceneMode.Single);

        // 2. Sandbox を加算で開き、マージ前のルート集合を記録（Sandbox 由来ルートの特定用）。
        var sandbox = EditorSceneManager.OpenScene(SANDBOX_PATH, OpenSceneMode.Additive);
        var beforeRoots = new HashSet<GameObject>(target.GetRootGameObjects());

        // 3. Sandbox を target へマージ（root を移動し、sandbox シーンはメモリ上で破棄される。ディスクは無改変）。
        SceneManager.MergeScenes(sandbox, target);

        var sandboxRoots = target.GetRootGameObjects()
                                 .Where(go => !beforeRoots.Contains(go))
                                 .ToList();

        // 4. Sandbox 由来ルートを選別：TerrainGenerator を持つものだけ残し、残りは破棄。
        GameObject terrainGo = null;
        var destroyed = new List<string>();
        foreach (var go in sandboxRoots)
        {
            if (terrainGo == null && go.GetComponent<TerrainGenerator>() != null)
            {
                terrainGo = go;
                continue;
            }
            destroyed.Add(go.name);
            Object.DestroyImmediate(go);
        }

        if (terrainGo == null)
        {
            Debug.LogError("[Combine] Sandbox 側に TerrainGenerator を持つ GameObject が見つかりません。中断します。");
            return;
        }

        // 5. TerrainGenerator / SandboxBootstrap を「地形＋大気のみ」に再設定し、地形整合コンフォーマを付与。
        //    CombinedTerrainConformer は実行時に HudManager（ワイヤーロープ力ゲージ）も生成する。
        ConfigureTerrain(terrainGo);
        if (terrainGo.GetComponent<CombinedTerrainConformer>() == null)
            terrainGo.AddComponent<CombinedTerrainConformer>();

        // 6. OfflineTest のフラット Ground と境界 Wall_* を無効化（Sandbox 地形を唯一の地面にする）。
        int disabled = DisableFlatGroundAndWalls(target);

        // 7. 新パスへ保存（in-memory の path は COMBINED_PATH に変わるが、ディスクの OfflineTestScene/Sandbox は未保存=無改変）。
        EditorSceneManager.SetActiveScene(target);
        bool saved = EditorSceneManager.SaveScene(target, COMBINED_PATH);
        if (!saved)
        {
            Debug.LogError($"[Combine] 保存に失敗しました: {COMBINED_PATH}");
            return;
        }
        AssetDatabase.Refresh();

        // 8. Build Settings に未登録なら追加（任意・可逆）。
        AddToBuildSettings(COMBINED_PATH);

        Debug.Log(
            $"[Combine] 生成完了: {COMBINED_PATH}\n" +
            $"  土台 = OfflineTestScene（ループ層そのまま）\n" +
            $"  地形 = Sandbox '{terrainGo.name}'（autoAttachSpawners=false / autoAttachAtmosphere=true / generateRoute=false / +CombinedTerrainConformer）\n" +
            $"  破棄した Sandbox 由来ルート = [{string.Join(", ", destroyed)}]\n" +
            $"  無効化した Ground/Wall = {disabled} 個（Sandbox 地形を唯一の地面に）");
    }

    /// <summary>TerrainGenerator 同居コンポーネントを「地形＋大気のみ」に設定する。</summary>
    private static void ConfigureTerrain(GameObject terrainGo)
    {
        var bootstrap = terrainGo.GetComponent<SandboxBootstrap>();
        if (bootstrap != null)
        {
            var so = new SerializedObject(bootstrap);
            SetBool(so, "autoAttachSpawners", false);   // 並行ゲームプレイ層（ScoreTracker 等）を注入しない
            SetBool(so, "autoAttachAtmosphere", true);  // 大気/空/雲/時刻サイクル/標高別カラーは載せる
            SetBool(so, "generateRoute", false);        // ルートは OfflineTest 側に任せる
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("[Combine] TerrainGenerator GO に SandboxBootstrap がありません。並行マネージャ抑制をスキップ。");
        }

        var terrain = terrainGo.GetComponent<TerrainGenerator>();
        if (terrain != null)
        {
            var ts = new SerializedObject(terrain);
            var viewer = ts.FindProperty("viewer");
            if (viewer != null) viewer.objectReferenceValue = null; // 破棄した Sandbox カメラへの参照を切る
            SetBool(ts, "autoUseMainCamera", true);                 // OfflineTest の MainCamera を追従
            ts.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>Environment 配下のフラット Ground と境界 Wall_* を無効化。無効化した数を返す。</summary>
    private static int DisableFlatGroundAndWalls(Scene target)
    {
        var env = target.GetRootGameObjects().FirstOrDefault(g => g.name == "Environment");
        if (env == null) return 0;
        int count = 0;
        foreach (Transform child in env.transform)
        {
            bool isFlatGround = child.name == "Ground";
            bool isWall = child.name.StartsWith("Wall_");
            if ((isFlatGround || isWall) && child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(false);
                count++;
            }
        }
        return count;
    }

    private static void AddToBuildSettings(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == scenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void SetBool(SerializedObject so, string prop, bool value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.boolValue = value;
        else Debug.LogWarning($"[Combine] SerializedProperty '{prop}' が見つかりません（{so.targetObject.GetType().Name}）。");
    }
}
