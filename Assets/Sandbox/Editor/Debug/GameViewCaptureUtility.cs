#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Sandbox.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PeakPlunder.EditorTools
{
    /// <summary>
    /// Play 中の Game View 全体（Screen Space - Overlay UI を含む合成結果）を PNG で保存する開発用ユーティリティ。
    /// 実行時生成 UI のビジュアル評価に使う。出力は Assets 外の <c>_Captures/</c> 以下（Unity に import されない）。
    /// </summary>
    public static class GameViewCaptureUtility
    {
        public const string MenuRoot = "Peak Plunder/Debug/";
        public const string MenuCapture = MenuRoot + "Capture Game View";

        private static string CaptureDir
        {
            get
            {
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "_Captures"));
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return dir;
            }
        }

        [MenuItem(MenuCapture)]
        public static void CaptureGameView()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName)) sceneName = "Untitled";
            CaptureNamed(sceneName);
        }

        [MenuItem(MenuRoot + "Capture Pause Menu")]
        public static void CapturePauseMenu()
        {
            var pause = Object.FindFirstObjectByType<PauseMenu>();
            if (pause == null)
            {
                Debug.LogWarning("[Capture] PauseMenu が見つかりません。");
                return;
            }

            pause.Pause();
            EditorApplication.delayCall += () => CaptureNamed("PauseMenu");
        }

        [MenuItem(MenuRoot + "Capture Pause Confirm")]
        public static void CapturePauseConfirm()
        {
            var pause = Object.FindFirstObjectByType<PauseMenu>();
            if (pause == null)
            {
                Debug.LogWarning("[Capture] PauseMenu が見つかりません。");
                return;
            }

            pause.EditorShowConfirmForCapture();
            EditorApplication.delayCall += () => CaptureNamed("PauseConfirm");
        }

        [MenuItem(MenuRoot + "Capture Result (Titles Stage)")]
        public static void CaptureResultTitles()
        {
            var result = Object.FindFirstObjectByType<ResultScreen>();
            if (result == null)
            {
                Debug.LogWarning("[Capture] ResultScreen が見つかりません。");
                return;
            }

            result.EditorCaptureAtStage(4, CreateSampleScore());
            EditorApplication.delayCall += () => CaptureNamed("Result");
        }

        [MenuItem(MenuRoot + "Capture Loading Overlay")]
        public static void CaptureLoadingOverlay()
        {
            var iris = IrisTransition.Instance;
            if (iris == null)
            {
                Debug.LogWarning("[Capture] IrisTransition が見つかりません。");
                return;
            }

            var overlay = LoadingTipsOverlay.Instance;
            overlay?.Show("ロープは岩より強い。…たまに。");
            overlay?.SetProgress(0.42f);
            EditorApplication.delayCall += () => CaptureNamed("Loading");
        }

        public static void CaptureNamed(string fileName)
        {
            string path = Path.Combine(CaptureDir, fileName + ".png");
            ScreenCapture.CaptureScreenshot(path, 3);
            Debug.Log($"[Capture] Game View -> {path}");
        }

        private static ScoreData CreateSampleScore()
        {
            return new ScoreData
            {
                TeamScore = 1240,
                ClearTimeSeconds = 842.35f,
                PlayerScores = new List<PlayerScore>
                {
                    new()
                    {
                        PlayerName = "Explorer",
                        IndividualScore = 620,
                        FallCount = 4,
                        ItemsLost = 2,
                        RelicDamageDealt = 18f,
                        GhostContributions = 0,
                        RopePlacementCount = 7,
                        ShoutCount = 3,
                        RelicsFoundCount = 2,
                        Survived = true,
                        RewardShare = 0.5f,
                        PlayerReward = 620
                    },
                    new()
                    {
                        PlayerName = "NPC_Alpha",
                        IndividualScore = 620,
                        FallCount = 9,
                        ItemsLost = 5,
                        RelicDamageDealt = 42f,
                        GhostContributions = 2,
                        RopePlacementCount = 3,
                        ShoutCount = 11,
                        RelicsFoundCount = 1,
                        Survived = false,
                        RewardShare = 0.5f,
                        PlayerReward = 620
                    }
                }
            };
        }
    }
}
#endif
