using System.Linq;
using UnityEngine;
using TMPro;

namespace Sandbox.UI
{
    /// <summary>
    /// シーン内の TMP テキストのフォントを 1 つ（プロジェクト標準＝NotoSansJP）に揃える。
    /// SandboxOfflineCombined のベイク済み UI には LiberationSans(TMP 既定) と
    /// NotoSansJP_Rebuilt が混在しており、タイポグラフィの統一感を損なう。
    ///
    /// シーンアセット(.unity)を書き換えない実行時パス（非破壊・可逆）。
    /// 値の判定（フォント参照の一致）で決定的に検証できる。
    /// </summary>
    public static class UiFontUnifier
    {
        private const string ProjectFontKeyword = "NotoSansJP";

        /// <summary>
        /// 読み込み済みフォントからプロジェクト標準フォント(NotoSansJP系)を解決する。
        /// ① TMP 既定フォント → ② ロード済み TMP_FontAsset の名前検索。
        /// AssetDatabase を使わないのでビルド時も動作する（QuotaUpgradeHud と同方針）。
        /// </summary>
        public static TMP_FontAsset ResolveProjectFont()
        {
            var def = TMP_Settings.defaultFontAsset;
            if (IsProjectFont(def)) return def;

            return Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(IsProjectFont);
        }

        private static bool IsProjectFont(TMP_FontAsset f)
        {
            return f != null
                   && f.name.IndexOf(ProjectFontKeyword, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// シーン内の全 TMP_Text を target フォントへ統一する。変更した件数を返す。
        /// target が null（標準フォント未発見）なら何もしない。
        /// </summary>
        public static int UnifyScene(TMP_FontAsset target)
        {
            if (target == null) return 0;

            int changed = 0;
            var labels = Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
            foreach (var label in labels)
            {
                if (label == null || label.font == target) continue;
                label.font = target;
                changed++;
            }
            return changed;
        }

        /// <summary>標準フォントを解決し、シーン全体へ適用するワンショット。変更件数を返す。</summary>
        public static int UnifySceneToProjectFont()
        {
            return UnifyScene(ResolveProjectFont());
        }
    }
}
