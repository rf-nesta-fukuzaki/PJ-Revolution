/// <summary>
/// GDD §5.1 / §8.3 — ショップロープ（ショート/ロング）の共有定数。
/// </summary>
public static class ShopRopeConstants
{
    public const float ShortRopeBreakForce = 3000f;
    public const float LongRopeBreakForce  = 2500f;
    public const float ConnectRange        = 2f;
    public const float AnchorConnectRange = 2f;

    /// <summary>GDD §5.1 — 壁/地面接触時の耐久消費（接触力 N ÷ この値 = 毎秒耐久）。</summary>
    public const float WallScrapeForceDivisor = 500f;
}
