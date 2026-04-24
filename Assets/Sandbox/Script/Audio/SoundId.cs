namespace PeakPlunder.Audio
{
    /// <summary>
    /// GDD §15.2 — SE 識別子。AudioManager.PlaySE(SoundId) で参照する。
    /// 本 enum の順番は SoundLibrary の配列インデックスと一致する必要はない（dictionary lookup）。
    /// </summary>
    public enum SoundId
    {
        None = 0,

        // ── プレイヤーアクション SE (GDD §15.2) ─────────────────────
        FootstepWalk,
        FootstepRun,
        FootstepCrouch,
        Jump,
        LandSoft,
        LandHard,
        ClimbGrab,
        ClimbRelease,
        SlideFall,
        RagdollImpact,
        StaminaWarning,
        StaminaEmpty,

        // ── アイテム SE (GDD §15.2) ──────────────────────────────
        ItemPickup,
        ItemDrop,
        ItemThrow,
        ItemImpact,
        ItemBreak,
        RopeConnect,
        RopeTension,
        RopeCut,
        RopeSnap,
        IceAxeStrike,
        AnchorBoltSet,
        GrapplingFire,
        GrapplingHit,
        WinchStart,
        WinchLoop,
        WinchCableSnap,
        FoodEat,
        FlareFire,
        RadioActivate,
        TentSetup,
        PackingWrap,
        BeltAttach,

        // ── 遺物固有 SE (GDD §15.2) ──────────────────────────────
        RelicDiscover,
        RelicGrab,
        RelicDamageLight,
        RelicDamageHeavy,
        RelicDestroyed,
        RelicDuckRoll,
        RelicPotSing,
        RelicSphereHum,
        RelicFishSlip,
        RelicMagnetPull,
        RelicTwinsChain,

        // ── 環境 SE (GDD §15.2) ──────────────────────────────────
        WindAmbient,
        WindGust,
        RainAmbient,
        BlizzardAmbient,
        RockfallWarning,
        RockfallImpact,
        Avalanche,
        IceCrack,
        FloorCrumbleWarn,
        FloorCrumble,
        TrapArrow,
        TrapPendulum,
        ShrineActivate,
        ShrineRevive,

        // ── UI SE (GDD §15.2) ────────────────────────────────────
        UiHover,
        UiClick,
        UiCancel,
        UiPurchase,
        UiPurchaseFail,
        UiVoteStart,
        UiVoteApprove,
        UiVoteDeny,
        UiPing,
        ResultCount,
        ResultTitle,
        HeliApproach,
        HeliHover,
        HeliDepart,

        // ── BGM ジングル (GDD §15.1) ───────────────────────────
        WipeoutJingle,
    }
}
