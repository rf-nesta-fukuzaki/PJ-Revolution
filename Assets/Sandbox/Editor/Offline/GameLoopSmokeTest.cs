#if UNITY_EDITOR
using PeakPlunder.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PeakPlunder.EditorTools
{
    /// <summary>
    /// Play モードで全ゲームループの自動スモークテストを起動する。
    ///
    /// 「Peak Plunder > Game Loop > Run Loop Smoke Test」を実行すると、
    ///   1. StartMenu シーンを開く
    ///   2. Play モードに入る
    ///   3. EnteredPlayMode で <see cref="GameLoopAutoPilot"/> を生成する
    /// という流れで、タイトル→インゲーム→リザルト→ショップ→購入→再出発を自動検証する。
    ///
    /// SessionState を使うため、Play 開始時のドメインリロードを跨いでもアーム状態が残る。
    /// </summary>
    [InitializeOnLoad]
    public static class GameLoopSmokeTest
    {
        private const string ArmedKey = "PeakPlunder.GameLoopSmokeTest.Armed";
        private const string LoopsKey = "PeakPlunder.GameLoopSmokeTest.Loops";
        private const int DefaultLoops = 2;

        static GameLoopSmokeTest()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem(PeakPlunderEditorMenus.GameLoop.RunLoopSmokeTest)]
        public static void RunLoopSmokeTest()
        {
            string scenePath = ResolveTitleScenePath();
            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogError("[SmokeTest] StartMenu シーンが見つかりません。");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            SessionState.SetBool(ArmedKey, true);
            SessionState.SetInt(LoopsKey, DefaultLoops);

            Debug.Log("[SmokeTest] アーム完了。Play モードを開始します。");
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;

            int loops = SessionState.GetInt(LoopsKey, DefaultLoops);
            SessionState.EraseBool(ArmedKey);

            GameLoopAutoPilot.Spawn(loops);
            Debug.Log($"[SmokeTest] AutoPilot を生成しました（loops={loops}）。");
        }

        private static string ResolveTitleScenePath()
        {
            string[] guids = AssetDatabase.FindAssets($"t:Scene {GameFlow.TitleScene}");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == GameFlow.TitleScene)
                    return path;
            }
            return null;
        }
    }
}
#endif
