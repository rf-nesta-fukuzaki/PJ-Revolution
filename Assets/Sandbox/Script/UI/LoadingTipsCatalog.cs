using System.Collections.Generic;
using PeakPlunder.Localization;
using UnityEngine;

namespace Sandbox.UI
{
    /// <summary>
    /// GDD §22.3 — ローディング中に表示する TIPS カタログ。
    /// LocalizationKeys の tip.* をランダムに返す。
    /// </summary>
    public static class LoadingTipsCatalog
    {
        private static readonly string[] TipKeys =
        {
            LocalizationKeys.TipCoopRope,
            LocalizationKeys.TipRelicDamage,
            LocalizationKeys.TipWeather,
            LocalizationKeys.TipSecureBelt,
            LocalizationKeys.TipThermalCase,
            LocalizationKeys.TipBivouac,
            LocalizationKeys.TipMagnetHelmet,
            LocalizationKeys.TipSingingVase,
            LocalizationKeys.TipTwinStatue,
            LocalizationKeys.TipScoreTitles,
        };

        private static readonly string[] FallbackJa =
        {
            "ロープで仲間を引き上げよう。Co-opの基本テク",
            "落下・衝突で遺物はダメージ。梱包キットで保護できる",
            "天候は時間とともに悪化する。早めの帰還も戦略",
            "固定ベルトで小型遺物を体に固定できる",
            "保温ケースは凍結ダメージを軽減する",
            "ビバークテントで動的チェックポイントを作れる",
            "磁力の兜は近くの金属を引き寄せる。注意して運搬",
            "歌う壺の周りではボイスチャットが乱れる",
            "双子像はペアで運ぶと安定する",
            "個人スコアでコメディ称号が付く。配信映え間違いなし",
        };

        public static string PickRandom()
        {
            int index = Random.Range(0, TipKeys.Length);
            string key = TipKeys[index];
            string localized = LocalizedText.Get(key, LocalizationKeys.TableHint);
            if (!string.IsNullOrEmpty(localized) && localized != key)
                return localized;
            return FallbackJa[index];
        }

        public static IReadOnlyList<string> AllTipKeys => TipKeys;
    }
}
