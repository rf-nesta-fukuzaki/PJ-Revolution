#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PeakPlunder.Localization;
using UnityEditor;
using UnityEngine;

#if PEAKPLUNDER_USE_UNITY_LOCALIZATION
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
    /// 動作条件: Unity Localization 1.5.4 以降がインストールされ、PEAKPLUNDER_USE_UNITY_LOCALIZATION が定義されていること。
    /// 起動: Peak Plunder → Bootstrap → Bootstrap Localization
    /// </summary>
    public static class PeakPlunderLocalizationBootstrap
    {
        private const string LOCALIZATION_DIR  = "Assets/Sandbox/Localization";
        private const string LOCALES_DIR       = "Assets/Sandbox/Localization/Locales";
        private const string TABLES_DIR        = "Assets/Sandbox/Localization/Tables";

        [MenuItem(PeakPlunderEditorMenus.Bootstrap.BootstrapLocalization)]
        public static void BootstrapLocalization()
        {
#if !PEAKPLUNDER_USE_UNITY_LOCALIZATION
            Debug.LogWarning("[PeakPlunder] PEAKPLUNDER_USE_UNITY_LOCALIZATION は未定義。Localization パッケージ導入後に Player Settings へ追加してください。");
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
            CreateTableAndPopulate(LocalizationKeys.TableHint,   BuildHintPairs(),   jaLocale, enLocale);
            CreateTableAndPopulate(LocalizationKeys.TableTip,    BuildTipPairs(),    jaLocale, enLocale);

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

#if PEAKPLUNDER_USE_UNITY_LOCALIZATION
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

        private static IEnumerable<KeyValuePair<string, (string ja, string en)>> BuildHintPairs() => new[]
        {
            P(LocalizationKeys.HintContextFirstClimb,    "左クリックで掴もう！黄色いポイントに手を伸ばして", "Left-click to grab a yellow climbing point"),
            P(LocalizationKeys.HintContextDashIntro,     "Shiftでダッシュ！ ただしスタミナに注意",           "Hold Shift to dash — watch your stamina"),
            P(LocalizationKeys.HintContextStaminaEmpty,  "スタミナが切れた！壁から手が離れます",             "Stamina depleted — you will release the wall"),
            P(LocalizationKeys.HintContextRelicApproach, "遺物を発見！左クリックで掴んで運ぼう。Gキーで丁寧に置けます", "Relic found! Grab with left-click, place gently with G"),
            P(LocalizationKeys.HintContextRelicClimb,    "遺物を持ったままでは壁を登れません。Gキーで置いてから登りましょう", "You cannot climb while carrying a relic. Drop it with G first"),
            P(LocalizationKeys.HintContextRopeNearby,  "ロープでチームメイトとつながろう！近づいてEキーで接続", "Connect to a teammate with E while holding a rope"),
            P(LocalizationKeys.HintContextPinMarker,     "Fキーでマーカーを設置！チームに危険やルートを知らせよう", "Place a marker with F to warn your team"),
            P(LocalizationKeys.HintContextReturnHeli,    "ベースキャンプに戻るか、フレアガンでヘリを呼ぼう（上空に向けて発射！）", "Return to basecamp or call a helicopter with a flare aimed skyward"),
            P(LocalizationKeys.TutorialShopStep1,        "予算 100pt はチーム共有です。早い者勝ちで誰でも買えます。", "The 100pt budget is shared. First come, first served."),
            P(LocalizationKeys.TutorialShopStep2,        "アイテム行をクリックで購入、再クリックで返品できます。装備数の上限に注意。", "Click a row to buy; click again to refund. Watch slot limits."),
            P(LocalizationKeys.TutorialShopStep3,        "各アイテムの説明を読み、遠征ルートに合わせて組み合わせましょう。", "Read item descriptions and plan for your route."),
            P(LocalizationKeys.TutorialShopStep4,        "準備ができたら「出発」ボタンで遠征開始！ 予算や装備は持ち越せません。", "When ready, press Depart. Budget and gear do not carry over."),
            P(LocalizationKeys.HintDepart,      "全員が出発ゲート前に集まり、ホストが出発を実行しよう", "Gather at the departure gate and have the host start the expedition"),
            P(LocalizationKeys.HintRopeSwing,     "ロープで岩に引っかけてスイングし、高い場所へ進もう",   "Hook a rope onto rocks and swing to reach higher ground"),
            P(LocalizationKeys.HintRelicCarry,    "遺物は壊れやすい。ゆっくり運んでチームで支え合おう",     "Relics are fragile — carry slowly and support each other"),
            P(LocalizationKeys.HintReturnVote,    "F5で帰還投票を開始できる。欲張りすぎると全滅のもと",   "Press F5 to start a return vote. Greed leads to wipeouts"),
            P(LocalizationKeys.HintGhostRevive,   "死亡後は幽霊になれる。祠を見つければ1回だけ復活できる", "After death you become a ghost. Find a shrine to revive once"),
            P(LocalizationKeys.HintShopBudget,    "チーム予算100ptで装備を買おう。役割分担がカギ",       "Spend the 100pt team budget wisely. Divide roles"),
            P(LocalizationKeys.HintIcePatch,      "氷パッチでは滑る。ピッケルや慎重な移動が有効",         "Ice patches are slippery. Use an ice axe or move carefully"),
            P(LocalizationKeys.HintStretcher,     "担架は2人で端を掴んで運べる。負傷者救出に使える",     "Two players can carry a stretcher by each end"),
            P(LocalizationKeys.HintWinch,         "ウインチで遺物を巻き上げられる。張力に注意",           "Use the winch to haul relics. Watch cable tension"),
            P(LocalizationKeys.HintFlareHeli,     "フレアを真上に撃つとヘリが呼べる（60秒後に到着）",     "Fire a flare straight up to call a helicopter (arrives in 60s)"),
            P(LocalizationKeys.HintCheckpoint,    "チェックポイントを通過すると死亡時の復帰地点になる",     "Checkpoints set your respawn point if you fall"),
            P(LocalizationKeys.HintRouteGate,     "ルートゲートは毎回ランダムに開閉する",                 "Route gates open and close randomly each run"),
        };

        private static IEnumerable<KeyValuePair<string, (string ja, string en)>> BuildTipPairs() => new[]
        {
            P(LocalizationKeys.TipCoopRope,       "ロープで仲間を引き上げよう。Co-opの基本テク",           "Use ropes to pull teammates up — core co-op tech"),
            P(LocalizationKeys.TipRelicDamage,    "落下・衝突で遺物はダメージ。梱包キットで保護できる",     "Falls and impacts damage relics. Packing kits help"),
            P(LocalizationKeys.TipWeather,        "天候は時間とともに悪化する。早めの帰還も戦略",         "Weather worsens over time. Early return is valid"),
            P(LocalizationKeys.TipSecureBelt,     "固定ベルトで小型遺物を体に固定できる",                   "Secure belt attaches small relics to your body"),
            P(LocalizationKeys.TipThermalCase,    "保温ケースは凍結ダメージを軽減する",                     "Thermal case reduces freeze damage on relics"),
            P(LocalizationKeys.TipBivouac,      "ビバークテントで動的チェックポイントを作れる",           "Bivouac tent creates a dynamic checkpoint"),
            P(LocalizationKeys.TipMagnetHelmet,     "磁力の兜は近くの金属を引き寄せる。注意して運搬",       "Magnetic helmet pulls nearby metal — carry with care"),
            P(LocalizationKeys.TipSingingVase,    "歌う壺の周りではボイスチャットが乱れる",               "Singing vases jam proximity voice chat"),
            P(LocalizationKeys.TipTwinStatue,       "双子像はペアで運ぶと安定する",                           "Twin statues are stable when carried as a pair"),
            P(LocalizationKeys.TipScoreTitles,    "個人スコアでコメディ称号が付く。配信映え間違いなし",   "Personal scores award comedy titles — clip-worthy chaos"),
        };

        private static KeyValuePair<string, (string ja, string en)> P(string key, string ja, string en)
            => new KeyValuePair<string, (string, string)>(key, (ja, en));
    }
}
#endif
