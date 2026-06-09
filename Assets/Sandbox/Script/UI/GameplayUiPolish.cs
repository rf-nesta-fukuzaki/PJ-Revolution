using UnityEngine;

namespace Sandbox.UI
{
    /// <summary>
    /// SandboxOfflineCombined 等の遠征 HUD をプレイ向けに整える実行時ポリッシュ（非破壊）。
    /// デバッグ用ヒントの非表示、リザルト中の HUD 抑制など。
    /// </summary>
    public static class GameplayUiPolish
    {
        private static readonly System.Collections.Generic.List<GameObject> s_suppressed = new();

        /// <summary>遠征 HUD 起動時に一度だけ呼ぶ。デバッグヒント等を隠す。</summary>
        public static void ApplyExpeditionHudCleanup(Transform hudRoot)
        {
            if (hudRoot == null) return;

            var canvasRoot = hudRoot.parent;
            if (canvasRoot != null)
            {
                var hint = canvasRoot.Find("DebugHint");
                if (hint != null) hint.gameObject.SetActive(false);
            }
        }

        /// <summary>リザルト表示中はゲームプレイ HUD を全面非表示にする。</summary>
        public static void SuppressGameplayHud(bool suppress)
        {
            // 編集モード（非 Play）ではシーン上の HUD オブジェクトを直接 SetActive すると、
            // その非アクティブ状態が「シーン保存」で永続化し、次回 Play 時に ExpeditionHUD /
            // MiniCompass / AltitudeMeter が丸ごと消える事故になる（Debug キャプチャメニューが
            // EditorCaptureAtStage 経由で本メソッドを編集モードで呼ぶ）。HUD 抑制は実行時の
            // リザルト演出専用なので Play 中のみ作用させ、編集モードのシーンは一切書き換えない。
            if (!Application.isPlaying) return;

            if (!suppress)
            {
                for (int i = 0; i < s_suppressed.Count; i++)
                {
                    if (s_suppressed[i] != null)
                        s_suppressed[i].SetActive(true);
                }
                s_suppressed.Clear();
                return;
            }

            s_suppressed.Clear();
            SuppressByPath("UIRoot/ExpeditionHUD");
            SuppressByPath("UIRoot/MiniCompassHUD");
            SuppressByPath("UIRoot/AltitudeMeterHUD");
            SuppressByPath("UIRoot/DebugHint");

            foreach (var q in Object.FindObjectsByType<QuotaUpgradeHud>(FindObjectsSortMode.None))
                SuppressObject(q.gameObject);

            foreach (var g in Object.FindObjectsByType<GhostHud>(FindObjectsSortMode.None))
                SuppressObject(g.gameObject);
        }

        private static void SuppressByPath(string path)
        {
            var go = GameObject.Find(path);
            SuppressObject(go);
        }

        private static void SuppressObject(GameObject go)
        {
            if (go == null || !go.activeSelf) return;
            go.SetActive(false);
            s_suppressed.Add(go);
        }
    }
}
