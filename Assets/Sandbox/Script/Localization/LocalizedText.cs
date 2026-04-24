using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_LOCALIZATION_AVAILABLE
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
#endif

namespace PeakPlunder.Localization
{
    /// <summary>
    /// GDD §22 — Unity Localization への薄いブリッジ。
    ///
    /// Localization パッケージが取り込まれ、StringTable がセットアップされるまでは
    /// キーをそのまま返すか、このクラスのフォールバック辞書（日本語）を返す。
    ///
    /// 呼び出し例:
    ///   string label = LocalizedText.Get(LocalizationKeys.UIMainMenuPlay);
    ///
    /// 本実装はパッケージ有無で挙動が切り替わる（UNITY_LOCALIZATION_AVAILABLE）。
    /// パッケージ導入後、Project Settings > Player > Scripting Define Symbols に
    /// "UNITY_LOCALIZATION_AVAILABLE" を追加すると実運用モードに切り替わる。
    /// </summary>
    public static class LocalizedText
    {
        private static readonly Dictionary<string, string> JpFallback = BuildJpFallback();
        private static readonly Dictionary<string, string> EnFallback = BuildEnFallback();
        private static string _fallbackLanguageCode = DetectDefaultFallbackLanguage();

        public static string CurrentFallbackLanguageCode => _fallbackLanguageCode;

        /// <summary>
        /// キーからローカライズ済み文字列を取得する。
        /// Unity Localization が未導入/未初期化の場合はフォールバック辞書を返す。
        /// </summary>
        public static string Get(string key, string tableName = "UI")
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

#if UNITY_LOCALIZATION_AVAILABLE
            try
            {
                var table = LocalizationSettings.StringDatabase.GetTable(tableName);
                if (table != null)
                {
                    var entry = table.GetEntry(key);
                    if (entry != null)
                    {
                        var value = entry.GetLocalizedString();
                        if (!string.IsNullOrEmpty(value)) return value;
                    }
                }
            }
            catch
            {
                // LocalizationSettings が未初期化時は例外を飲み込み、フォールバックに進む
            }
#endif
            if (TryGetFallbackValue(key, out var fallback))
                return fallback;

            return key;
        }

        /// <summary>
        /// フォールバック言語（ja/en）を明示指定する。未対応コードは ja 扱い。
        /// </summary>
        public static void SetFallbackLanguage(string languageCode)
        {
            _fallbackLanguageCode = NormalizeLanguageCode(languageCode);
        }

        /// <summary>
        /// フォールバック辞書を拡張する（テスト用 or パッケージ未導入時の手動追加）。
        /// </summary>
        public static void RegisterFallback(string key, string value)
        {
            RegisterFallback(key, value, _fallbackLanguageCode);
        }

        public static void RegisterFallback(string key, string value, string languageCode)
        {
            if (string.IsNullOrEmpty(key)) return;

            if (NormalizeLanguageCode(languageCode) == "en")
                EnFallback[key] = value;
            else
                JpFallback[key] = value;
        }

        // ── 日本語フォールバック辞書（基準言語 — GDD §22.1）──────
        private static Dictionary<string, string> BuildJpFallback()
        {
            return new Dictionary<string, string>
            {
                // UI
                { LocalizationKeys.UIMainMenuPlay,     "プレイ" },
                { LocalizationKeys.UIMainMenuQuit,     "終了" },
                { LocalizationKeys.UIMainMenuSettings, "設定" },
                { LocalizationKeys.UILobbyCreate,      "ルームを作成" },
                { LocalizationKeys.UILobbyJoin,        "ルームに参加" },
                { LocalizationKeys.UILobbyRoomCode,    "ルームコード" },
                { LocalizationKeys.UILobbyStart,       "開始" },
                { LocalizationKeys.UIBasecampShop,     "装備ショップ" },
                { LocalizationKeys.UIBasecampDepart,   "出発" },
                { LocalizationKeys.UIBasecampBudget,   "チーム予算" },
                { LocalizationKeys.UIHudAltitude,      "高度" },
                { LocalizationKeys.UIHudStamina,       "スタミナ" },
                { LocalizationKeys.UIHudHealth,        "体力" },
                { LocalizationKeys.UIResultScore,      "スコア" },
                { LocalizationKeys.UIResultReturn,     "リトライ" },
                { LocalizationKeys.UIResultBestTime,   "ベストタイム" },
                { LocalizationKeys.UIVoteReturnStart,  "帰還投票を開始" },
                { LocalizationKeys.UIVoteApprove,      "承認" },
                { LocalizationKeys.UIVoteDeny,         "拒否" },
                { LocalizationKeys.UISettingsGraphics, "グラフィック" },
                { LocalizationKeys.UISettingsAudio,    "オーディオ" },
                { LocalizationKeys.UISettingsControls, "操作" },
                { LocalizationKeys.UISettingsAccess,   "アクセシビリティ" },

                // アイテム
                { LocalizationKeys.ItemShortRope,      "ショートロープ" },
                { LocalizationKeys.ItemLongRope,       "ロングロープ" },
                { LocalizationKeys.ItemAnchorBolt,     "アンカーボルト" },
                { LocalizationKeys.ItemIceAxe,         "アイスアックス" },
                { LocalizationKeys.ItemGrapplingHook,  "グラップリングフック" },
                { LocalizationKeys.ItemPortableWinch,  "携帯ウインチ" },
                { LocalizationKeys.ItemSecureBelt,     "固定ベルト" },
                { LocalizationKeys.ItemStretcher,      "担架" },
                { LocalizationKeys.ItemBivouacTent,    "ビバークテント" },
                { LocalizationKeys.ItemOxygenTank,     "酸素タンク" },
                { LocalizationKeys.ItemThermalCase,    "保温ケース" },
                { LocalizationKeys.ItemFlareGun,       "フレアガン" },
                { LocalizationKeys.ItemFood,           "食料" },
                { LocalizationKeys.ItemPackingKit,     "梱包キット" },
                { LocalizationKeys.ItemEmergencyRadio, "緊急無線機" },

                // 遺物
                { LocalizationKeys.RelicGoldenDuck,     "黄金のアヒル像" },
                { LocalizationKeys.RelicCrystalCup,     "古代クリスタルの杯" },
                { LocalizationKeys.RelicGreatStoneSlab, "儀式用の大石板" },
                { LocalizationKeys.RelicSingingVase,    "歌う壺" },
                { LocalizationKeys.RelicFloatingSphere, "浮遊する球体" },
                { LocalizationKeys.RelicTwinStatue,     "鎖付き双子像" },
                { LocalizationKeys.RelicSlipperyFish,   "ぬるぬる聖なる魚像" },
                { LocalizationKeys.RelicMagneticHelmet, "磁力の兜" },

                // 称号
                { LocalizationKeys.TitleGravityFriend, "重力の友" },
                { LocalizationKeys.TitleHeroExplorer,  "英雄探検家" },
                { LocalizationKeys.TitleButterFingers, "バターフィンガー" },
                { LocalizationKeys.TitleTeamPlayer,    "チームの要" },
                { LocalizationKeys.TitleRelicMagnet,   "遺物マグネット" },
                { LocalizationKeys.TitleVictim,        "災難の星" },
                { LocalizationKeys.TitleSprinter,      "疾走者" },
                { LocalizationKeys.TitleLoyalGhost,    "忠実なる幽霊" },

                // 天候
                { LocalizationKeys.WeatherClear,    "晴れ" },
                { LocalizationKeys.WeatherCloudy,   "曇り" },
                { LocalizationKeys.WeatherRain,     "雨" },
                { LocalizationKeys.WeatherBlizzard, "吹雪" },
                { LocalizationKeys.WeatherFog,      "霧" },

                // 大石板
                { LocalizationKeys.StoneSlabMenu1, "本日の給食：砂岩定食" },
                { LocalizationKeys.StoneSlabMenu2, "本日の給食：石灰粥" },
                { LocalizationKeys.StoneSlabMenu3, "本日の給食：花崗岩プリン" },
                { LocalizationKeys.StoneSlabMenu4, "本日の給食：玄武岩煮込み" },
                { LocalizationKeys.StoneSlabMenu5, "本日の給食：水晶ジュレ" },
            };
        }

        // ── 英語フォールバック辞書（EA必須言語 — GDD §24.1）────────
        private static Dictionary<string, string> BuildEnFallback()
        {
            return new Dictionary<string, string>
            {
                // UI
                { LocalizationKeys.UIMainMenuPlay,     "Play" },
                { LocalizationKeys.UIMainMenuQuit,     "Quit" },
                { LocalizationKeys.UIMainMenuSettings, "Settings" },
                { LocalizationKeys.UILobbyCreate,      "Create Room" },
                { LocalizationKeys.UILobbyJoin,        "Join Room" },
                { LocalizationKeys.UILobbyRoomCode,    "Room Code" },
                { LocalizationKeys.UILobbyStart,       "Start" },
                { LocalizationKeys.UIBasecampShop,     "Gear Shop" },
                { LocalizationKeys.UIBasecampDepart,   "Depart" },
                { LocalizationKeys.UIBasecampBudget,   "Team Budget" },
                { LocalizationKeys.UIHudAltitude,      "Altitude" },
                { LocalizationKeys.UIHudStamina,       "Stamina" },
                { LocalizationKeys.UIHudHealth,        "Health" },
                { LocalizationKeys.UIResultScore,      "Score" },
                { LocalizationKeys.UIResultReturn,     "Retry" },
                { LocalizationKeys.UIResultBestTime,   "Best Time" },
                { LocalizationKeys.UIVoteReturnStart,  "Start Return Vote" },
                { LocalizationKeys.UIVoteApprove,      "Approve" },
                { LocalizationKeys.UIVoteDeny,         "Deny" },
                { LocalizationKeys.UISettingsGraphics, "Graphics" },
                { LocalizationKeys.UISettingsAudio,    "Audio" },
                { LocalizationKeys.UISettingsControls, "Controls" },
                { LocalizationKeys.UISettingsAccess,   "Accessibility" },

                // アイテム
                { LocalizationKeys.ItemShortRope,      "Short Rope" },
                { LocalizationKeys.ItemLongRope,       "Long Rope" },
                { LocalizationKeys.ItemAnchorBolt,     "Anchor Bolt" },
                { LocalizationKeys.ItemIceAxe,         "Ice Axe" },
                { LocalizationKeys.ItemGrapplingHook,  "Grappling Hook" },
                { LocalizationKeys.ItemPortableWinch,  "Portable Winch" },
                { LocalizationKeys.ItemSecureBelt,     "Secure Belt" },
                { LocalizationKeys.ItemStretcher,      "Stretcher" },
                { LocalizationKeys.ItemBivouacTent,    "Bivouac Tent" },
                { LocalizationKeys.ItemOxygenTank,     "Oxygen Tank" },
                { LocalizationKeys.ItemThermalCase,    "Thermal Case" },
                { LocalizationKeys.ItemFlareGun,       "Flare Gun" },
                { LocalizationKeys.ItemFood,           "Food" },
                { LocalizationKeys.ItemPackingKit,     "Packing Kit" },
                { LocalizationKeys.ItemEmergencyRadio, "Emergency Radio" },

                // 遺物
                { LocalizationKeys.RelicGoldenDuck,     "Golden Duck Idol" },
                { LocalizationKeys.RelicCrystalCup,     "Ancient Crystal Cup" },
                { LocalizationKeys.RelicGreatStoneSlab, "Great Ritual Stone Slab" },
                { LocalizationKeys.RelicSingingVase,    "Singing Vase" },
                { LocalizationKeys.RelicFloatingSphere, "Floating Sphere" },
                { LocalizationKeys.RelicTwinStatue,     "Chained Twin Statues" },
                { LocalizationKeys.RelicSlipperyFish,   "Slippery Sacred Fish" },
                { LocalizationKeys.RelicMagneticHelmet, "Magnetic Helm" },

                // 称号
                { LocalizationKeys.TitleGravityFriend, "Gravity's Friend" },
                { LocalizationKeys.TitleHeroExplorer,  "Hero Explorer" },
                { LocalizationKeys.TitleButterFingers, "Butter Fingers" },
                { LocalizationKeys.TitleTeamPlayer,    "Team Player" },
                { LocalizationKeys.TitleRelicMagnet,   "Relic Magnet" },
                { LocalizationKeys.TitleVictim,        "Victim of Fate" },
                { LocalizationKeys.TitleSprinter,      "Sprinter" },
                { LocalizationKeys.TitleLoyalGhost,    "Loyal Ghost" },

                // 天候
                { LocalizationKeys.WeatherClear,    "Clear" },
                { LocalizationKeys.WeatherCloudy,   "Cloudy" },
                { LocalizationKeys.WeatherRain,     "Rain" },
                { LocalizationKeys.WeatherBlizzard, "Blizzard" },
                { LocalizationKeys.WeatherFog,      "Fog" },

                // 大石板
                { LocalizationKeys.StoneSlabMenu1, "Today's Menu: Sandstone Set" },
                { LocalizationKeys.StoneSlabMenu2, "Today's Menu: Limestone Porridge" },
                { LocalizationKeys.StoneSlabMenu3, "Today's Menu: Granite Pudding" },
                { LocalizationKeys.StoneSlabMenu4, "Today's Menu: Basalt Stew" },
                { LocalizationKeys.StoneSlabMenu5, "Today's Menu: Crystal Jelly" },
            };
        }

        private static bool TryGetFallbackValue(string key, out string value)
        {
            var active = _fallbackLanguageCode == "en" ? EnFallback : JpFallback;
            if (active.TryGetValue(key, out value))
                return true;

            if (JpFallback.TryGetValue(key, out value))
                return true;

            return EnFallback.TryGetValue(key, out value);
        }

        private static string DetectDefaultFallbackLanguage()
        {
            return Application.systemLanguage == SystemLanguage.English ? "en" : "ja";
        }

        private static string NormalizeLanguageCode(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
                return "ja";

            string normalized = languageCode.Trim().ToLowerInvariant();
            return normalized.StartsWith("en", StringComparison.Ordinal) ? "en" : "ja";
        }
    }
}
