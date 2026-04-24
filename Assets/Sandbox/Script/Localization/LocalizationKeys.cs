namespace PeakPlunder.Localization
{
    /// <summary>
    /// GDD §22.3 — ローカライズキー定数。
    /// StringTable (日本語=基準言語, 英語=EA版必須) のキーを定義する。
    ///
    /// カテゴリ: UI テキスト (100), アイテム名+説明 (30), 遺物名+説明 (16),
    ///          称号+判定テキスト (16), チュートリアルヒント (12),
    ///          TIPS (10), 天候ボード (10), 大石板 (5) — 合計約200キー
    ///
    /// 命名規則: "category.subcategory.name"
    /// </summary>
    public static class LocalizationKeys
    {
        public const string TableUI     = "UI";
        public const string TableItem   = "Item";
        public const string TableRelic  = "Relic";
        public const string TableTitle  = "Title";
        public const string TableHint   = "Hint";
        public const string TableTip    = "Tip";
        public const string TableWeather = "Weather";
        public const string TableStone  = "Stone";

        // ── UI 主要キー (GDD §22.3) ───────────────────────────────
        public const string UIMainMenuPlay     = "ui.mainmenu.play";
        public const string UIMainMenuQuit     = "ui.mainmenu.quit";
        public const string UIMainMenuSettings = "ui.mainmenu.settings";

        public const string UILobbyCreate      = "ui.lobby.create";
        public const string UILobbyJoin        = "ui.lobby.join";
        public const string UILobbyRoomCode    = "ui.lobby.room_code";
        public const string UILobbyStart       = "ui.lobby.start";

        public const string UIBasecampShop     = "ui.basecamp.shop";
        public const string UIBasecampDepart   = "ui.basecamp.depart";
        public const string UIBasecampBudget   = "ui.basecamp.budget";

        public const string UIHudAltitude      = "ui.hud.altitude";
        public const string UIHudStamina       = "ui.hud.stamina";
        public const string UIHudHealth        = "ui.hud.health";

        public const string UIResultScore      = "ui.result.score";
        public const string UIResultReturn     = "ui.result.return";
        public const string UIResultBestTime   = "ui.result.best_time";

        public const string UIVoteReturnStart  = "ui.vote.return_start";
        public const string UIVoteApprove      = "ui.vote.approve";
        public const string UIVoteDeny         = "ui.vote.deny";

        public const string UISettingsGraphics = "ui.settings.graphics";
        public const string UISettingsAudio    = "ui.settings.audio";
        public const string UISettingsControls = "ui.settings.controls";
        public const string UISettingsAccess   = "ui.settings.accessibility";

        // ── アイテム名 (15種) ─────────────────────────────────────
        public const string ItemShortRope      = "item.short_rope";
        public const string ItemLongRope       = "item.long_rope";
        public const string ItemAnchorBolt     = "item.anchor_bolt";
        public const string ItemIceAxe         = "item.ice_axe";
        public const string ItemGrapplingHook  = "item.grappling_hook";
        public const string ItemPortableWinch  = "item.portable_winch";
        public const string ItemSecureBelt     = "item.secure_belt";
        public const string ItemStretcher      = "item.stretcher";
        public const string ItemBivouacTent    = "item.bivouac_tent";
        public const string ItemOxygenTank     = "item.oxygen_tank";
        public const string ItemThermalCase    = "item.thermal_case";
        public const string ItemFlareGun       = "item.flare_gun";
        public const string ItemFood           = "item.food";
        public const string ItemPackingKit     = "item.packing_kit";
        public const string ItemEmergencyRadio = "item.emergency_radio";

        // ── 遺物名 (8種) ──────────────────────────────────────────
        public const string RelicGoldenDuck       = "relic.golden_duck";
        public const string RelicCrystalCup       = "relic.crystal_cup";
        public const string RelicGreatStoneSlab   = "relic.great_stone_slab";
        public const string RelicSingingVase      = "relic.singing_vase";
        public const string RelicFloatingSphere   = "relic.floating_sphere";
        public const string RelicTwinStatue       = "relic.twin_statue";
        public const string RelicSlipperyFish     = "relic.slippery_fish_statue";
        public const string RelicMagneticHelmet   = "relic.magnetic_helmet";

        // ── 称号 (8種、GDD §13.2) ─────────────────────────────────
        public const string TitleGravityFriend  = "title.gravity_friend";
        public const string TitleHeroExplorer   = "title.hero_explorer";
        public const string TitleButterFingers  = "title.butter_fingers";
        public const string TitleTeamPlayer     = "title.team_player";
        public const string TitleRelicMagnet    = "title.relic_magnet";
        public const string TitleVictim         = "title.victim";
        public const string TitleSprinter       = "title.sprinter";
        public const string TitleLoyalGhost     = "title.loyal_ghost";

        // ── 天候 ──────────────────────────────────────────────────
        public const string WeatherClear    = "weather.clear";
        public const string WeatherCloudy   = "weather.cloudy";
        public const string WeatherRain     = "weather.rain";
        public const string WeatherBlizzard = "weather.blizzard";
        public const string WeatherFog      = "weather.fog";

        // ── 大石板 (5キー、GDD §9.3 歌う壺/大石板系) ─────────────
        public const string StoneSlabMenu1 = "stone.slab_menu_1";
        public const string StoneSlabMenu2 = "stone.slab_menu_2";
        public const string StoneSlabMenu3 = "stone.slab_menu_3";
        public const string StoneSlabMenu4 = "stone.slab_menu_4";
        public const string StoneSlabMenu5 = "stone.slab_menu_5";
    }
}
