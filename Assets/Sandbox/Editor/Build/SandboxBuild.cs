using System.Collections.Generic;
using System.IO;
using System.Linq;
using PeakPlunder.EditorTools;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Sandbox.EditorTools
{
    /// <summary>
    /// ワンクリックビルド。Build Settings で有効なシーンをそのまま使う。
    /// メニュー: Peak Plunder/Build/...
    /// 出力先: &lt;repo&gt;/Builds/PeakIdiots_v0.1/(Windows|macOS)/
    /// Windows ビルドは macOS 上では active target 切替（フル再インポート）を伴うので時間がかかる点に注意。
    /// </summary>
    public static class SandboxBuild
    {
        private const string OUT_ROOT = "Builds/PeakIdiots_v0.1";
        private const string APP_NAME = "PeakIdiots";

        private static string[] EnabledScenes()
        {
            return EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
        }

        [MenuItem(PeakPlunderEditorMenus.Build.BuildMacOS)]
        public static void BuildMac()
        {
            string dir = Path.Combine(OUT_ROOT, "macOS");
            Directory.CreateDirectory(dir);
            Run(BuildTarget.StandaloneOSX, Path.Combine(dir, APP_NAME + ".app"));
        }

        [MenuItem(PeakPlunderEditorMenus.Build.BuildWindows)]
        public static void BuildWindows()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
                if (!ok) { Debug.LogError("[SandboxBuild] Windows ターゲットへ切替できませんでした（モジュール未導入の可能性）。"); return; }
            }
            string dir = Path.Combine(OUT_ROOT, "Windows");
            Directory.CreateDirectory(dir);
            Run(BuildTarget.StandaloneWindows64, Path.Combine(dir, APP_NAME + ".exe"));
        }

        /// <summary>development build の軽量 dry run（現在の active target でビルドして成立性のみ確認）。</summary>
        public static BuildReport DryRun(string locationPathName)
        {
            return Run(EditorUserBuildSettings.activeBuildTarget, locationPathName, BuildOptions.Development);
        }

        private static BuildReport Run(BuildTarget target, string location, BuildOptions options = BuildOptions.None)
        {
            var scenes = EnabledScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[SandboxBuild] Build Settings に有効なシーンがありません。");
                return null;
            }
            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = location,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                options = options,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[SandboxBuild] {target} → {s.result}  size={s.totalSize}bytes  time={s.totalTime}  out={location}");
            return report;
        }
    }
}
