#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PeakPlunder.Localization;
using UnityEditor;
using UnityEngine;

#if UNITY_LOCALIZATION_AVAILABLE
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEditor.Localization;
#endif

namespace PeakPlunder.EditorTools
{
    /// <summary>
    /// GDD §22 — Localization StringTable ブートストラッパー。
    /// Locale (ja/en) と StringTableCollections (UI/Item/Relic/Title/Weather/Stone/Hint/Tip) を作成する。
    ///
    /// 動作条件: Unity Localization 1.5.4 以降がインストールされていること。
    /// 起動: Tools > PeakPlunder > Bootstrap Localization
    /// </summary>
    public static class PeakPlunderLocalizationBootstrap
    {
        private const string LOCALIZATION_DIR  = "Assets/Sandbox/Localization";
        private const string LOCALES_DIR       = "Assets/Sandbox/Localization/Locales";
        private const string TABLES_DIR        = "Assets/Sandbox/Localization/Tables";

        [MenuItem("Tools/PeakPlunder/Bootstrap Localization")]
        public static void BootstrapLocalization()
        {
#if !UNITY_LOCALIZATION_AVAILABLE
            Debug.LogWarning("[PeakPlunder] UNITY_LOCALIZATION_AVAILABLE は未定義。Player Settings に追加してください。");
#else
            EnsureDirectory(LOCALIZATION_DIR);
            EnsureDirectory(LOCALES_DIR);
            EnsureDirectory(TABLES_DIR);

            var jaLocale = EnsureLocale("ja", "Japanese");
            var enLocale = EnsureLocale("en", "English");

            // 各テーブルを作成 + 日英の値を投入
            CreateTableAndPopulate(LocalizationKeys.TableUI,     BuildUiPairs(),     jaLocale, enLocale);
            CreateTableAndPopulate(LocalizationKeys.TableItem,   BuildItemPairs(),   jaLocale, enLocale);
            CreateTableAndPopulate(LocalizationKeys.TableRelic,  BuildRelicPairs(),  jaLocale, enLocale);
            CreateTableAndPopulate(LocalizationKeys.TableTitle,  BuildTitlePairs(),  jaLocale, enLocale);
            CreateTableAndPopulate(LocalizationKeys.TableWeather,BuildWeatherPairs(),jaLocale, enLocale);
            CreateTableAndPopulate(LocalizationKeys.TableStone,  BuildStonePairs(),  jaLocale, enLocale);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PeakPlunder] Localization bootstrap complete.");
#endif
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }

#if UNITY_LOCALIZATION_AVAILABLE
        private static Locale EnsureLocale(string code, string label)
        {
            string path = $"{LOCALES_DIR}/{label} ({code}).asset";
            var existing = AssetDatabase.LoadAssetAtPath<Locale>(path);
            if (existing != null) return existing;

            var locale = Locale.CreateLocale(new LocaleIdentifier(code));
            locale.name = $"{label} ({code})";
            AssetDatabase.CreateAsset(locale, path);
            LocalizationEditorSettings.AddLocale(locale);
            Debug.Log($"[PeakPlunder] Locale created: {locale.name}");
            return locale;
        }

        private static void CreateTableAndPopulate(string tableName,
            IEnumerable<KeyValuePair<string, (string ja, string en)>> pairs,
            Locale ja, Locale en)
        {
            var existing = LocalizationEditorSettings.GetStringTableCollection(tableName);
            StringTableCollection collection = existing;
            if (collection == null)
            {
                collection = LocalizationEditorSettings.CreateStringTableCollection(tableName, TABLES_DIR);
                Debug.Log($"[PeakPlunder] Created StringTableCollection: {tableName}");
            }

            var sharedData = collection.SharedData;
            var jaTable = collection.GetTable(ja.Identifier) as StringTable;
            var enTable = collection.GetTable(en.Identifier) as StringTable;

            if (jaTable == null || enTable == null)
            {
                Debug.LogError($"[PeakPlunder] Missing ja/en tables for {tableName} (ja={jaTable != null}, en={enTable != null})");
                return;
            }

            int count = 0;
            foreach (var kv in pairs)
            {
                var keyEntry = sharedData.GetEntry(kv.Key);
                if (keyEntry == null)
                    keyEntry = sharedData.AddKey(kv.Key);
                if (keyEntry == null) continue;

                SetValue(jaTable, kv.Key, kv.Value.ja);
                SetValue(enTable, kv.Key, kv.Value.en);
                count++;
            }

            EditorUtility.SetDirty(sharedData);
            EditorUtility.SetDirty(jaTable);
            EditorUtility.SetDirty(enTable);
            Debug.Log($"[PeakPlunder] Populated {count} entries into {tableName}");
        }

        private static void SetValue(StringTable table, string key, string value)
        {
            var entry = table.GetEntry(key);
            if (entry == null) entry = table.AddEntry(key, value);
            else entry.Value = value;
        }
#endif

        private static IEnumerable<KeyValuePair<string, (string ja, string en)>> BuildUiPairs() => new[]
        {
            P(LocalizationKeys.UIMainMenuPlay,     "プレイ",              "Play"),
            P(LocalizationKeys.UIMainMenuQuit,     "終了",                "Quit"),
            P(LocalizationKeys.UIMainMenuSettings, "設定",                "Settings"),
            P(LocalizationKeys.UILobbyCreate,      "ルームを作成",        "Create Room"),
            P(LocalizationKeys.UILobbyJoin,        "ルームに参加",        "Join Room"),
            P(LocalizationKeys.UILobbyRoomCode,    "ルームコード",        "Room Code"),
            P(LocalizationKeys.UILobbyStart,       "開始",                "Start"),
            P(LocalizationKeys.UIBasecampShop,     "装備ショップ",        "Gear Shop"),
            P(LocalizationKeys.UIBasecampDepart,   "出発",                "Depart"),
            P(LocalizationKeys.UIBasecampBudget,   "チーム予算",          "Team Budget"),
            P(LocalizationKeys.UIHudAltitude,      "高度",                "Altitude"),
            P(LocalizationKeys.UIHudStamina,       "スタミナ",            "Stamina"),
            P(LocalizationKeys.UIHudHealth,        "体力",                "Health"),
            P(LocalizationKeys.UIResultScore,      "スコア",              "Score"),
            P(LocalizationKeys.UIResultReturn,     "リトライ",            "Retry"),
            P(LocalizationKeys.UIResultBestTime,   "ベストタイム",        "Best Time"),
            P(LocalizationKeys.UIVoteReturnStart,  "帰還投票を開始",      "Start Return Vote"),
            P(LocalizationKeys.UIVoteApprove,      "承認",                "Approve"),
            P(LocalizationKeys.UIVoteDeny,         "拒否",                "Deny"),
            P(LocalizationKeys.UISettingsGraphics, "グラフィック",        "Graphics"),
            P(LocalizationKeys.UISettingsAudio,    "オーディオ",          "Audio"),
            P(LocalizationKeys.UISettingsControls, "操作",                "Controls"),
            P(LocalizationKeys.UISettingsAccess,   "アクセシビリティ",    "Accessibility"),
        };

        private static IEnumerable<KeyValuePair<string, (string ja, string en)>> BuildItemPairs() => new[]
        {
            P(LocalizationKeys.ItemShortRope,      "ショートロープ",      "Short Rope"),
            P(LocalizationKeys.ItemLongRope,       "ロングロープ",        "Long Rope"),
            P(LocalizationKeys.ItemAnchorBolt,     "アンカーボルト",      "Anchor Bolt"),
            P(LocalizationKeys.ItemIceAxe,         "アイスアックス",      "Ice Axe"),
            P(LocalizationKeys.ItemGrapplingHook,  "グラップリングフック","Grappling Hook"),
            P(LocalizationKeys.ItemPortableWinch,  "携帯ウインチ",        "Portable Winch"),
            P(LocalizationKeys.ItemSecureBelt,     "固定ベルト",          "Secure Belt"),
            P(LocalizationKeys.ItemStretcher,      "担架",                "Stretcher"),
            P(LocalizationKeys.ItemBivouacTent,    "ビバークテント",      "Bivouac Tent"),
            P(LocalizationKeys.ItemOxygenTank,     "酸素タンク",          "Oxygen Tank"),
            P(LocalizationKeys.ItemThermalCase,    "保温ケース",          "Thermal Case"),
            P(LocalizationKeys.ItemFlareGun,       "フレアガン",          "Flare Gun"),
            P(LocalizationKeys.ItemFood,           "食料",                "Food"),
            P(LocalizationKeys.ItemPackingKit,     "梱包キット",          "Packing Kit"),
            P(LocalizationKeys.ItemEmergencyRadio, "緊急無線機",          "Emergency Radio"),
        };

        private static IEnumerable<KeyValuePair<string, (string ja, string en)>> BuildRelicPairs() => new[]
        {
            P(LocalizationKeys.RelicGoldenDuck,     "黄金のアヒル像",          "Golden Duck Idol"),
            P(LocalizationKeys.RelicCrystalCup,     "古代クリスタルの杯",      "Ancient Crystal Cup"),
            P(LocalizationKeys.RelicGreatStoneSlab, "儀式用の大石板",          "Great Ritual Stone Slab"),
            P(LocalizationKeys.RelicSingingVase,    "歌う壺",                  "Singing Vase"),
            P(LocalizationKeys.RelicFloatingSphere, "浮遊する球体",            "Floating Sphere"),
            P(LocalizationKeys.RelicTwinStatue,     "鎖付き双子像",            "Chained Twin Statues"),
            P(LocalizationKeys.RelicSlipperyFish,   "ぬるぬる聖なる魚像",      "Slippery Sacred Fish"),
            P(LocalizationKeys.RelicMagneticHelmet, "磁力の兜",                "Magnetic Helm"),
        };

        private static IEnumerable<KeyValuePair<string, (string ja, string en)>> BuildTitlePairs() => new[]
        {
            P(LocalizationKeys.TitleGravityFriend, "重力の友",            "Gravity's Friend"),
            P(LocalizationKeys.TitleHeroExplorer,  "英雄探検家",          "Hero Explorer"),
            P(LocalizationKeys.TitleButterFingers, "バターフィンガー",    "Butter Fingers"),
            P(LocalizationKeys.TitleTeamPlayer,    "チームの要",          "Team Player"),
            P(LocalizationKeys.TitleRelicMagnet,   "遺物マグネット",      "Relic Magnet"),
            P(LocalizationKeys.TitleVictim,        "災難の星",            "Victim of Fate"),
            P(LocalizationKeys.TitleSprinter,      "疾走者",              "Sprinter"),
            P(LocalizationKeys.TitleLoyalGhost,    "忠実なる幽霊",        "Loyal Ghost"),
        };

        private static IEnumerable<KeyValuePair<string, (string ja, string en)>> BuildWeatherPairs() => new[]
        {
            P(LocalizationKeys.WeatherClear,    "晴れ",    "Clear"),
            P(LocalizationKeys.WeatherCloudy,   "曇り",    "Cloudy"),
            P(LocalizationKeys.WeatherRain,     "雨",      "Rain"),
            P(LocalizationKeys.WeatherBlizzard, "吹雪",    "Blizzard"),
            P(LocalizationKeys.WeatherFog,      "霧",      "Fog"),
        };

        private static IEnumerable<KeyValuePair<string, (string ja, string en)>> BuildStonePairs() => new[]
        {
            P(LocalizationKeys.StoneSlabMenu1, "本日の給食：砂岩定食",       "Today's Menu: Sandstone Set"),
            P(LocalizationKeys.StoneSlabMenu2, "本日の給食：石灰粥",         "Today's Menu: Limestone Porridge"),
            P(LocalizationKeys.StoneSlabMenu3, "本日の給食：花崗岩プリン",   "Today's Menu: Granite Pudding"),
            P(LocalizationKeys.StoneSlabMenu4, "本日の給食：玄武岩煮込み",   "Today's Menu: Basalt Stew"),
            P(LocalizationKeys.StoneSlabMenu5, "本日の給食：水晶ジュレ",     "Today's Menu: Crystal Jelly"),
        };

        private static KeyValuePair<string, (string ja, string en)> P(string key, string ja, string en)
            => new KeyValuePair<string, (string, string)>(key, (ja, en));
    }
}
#endif
