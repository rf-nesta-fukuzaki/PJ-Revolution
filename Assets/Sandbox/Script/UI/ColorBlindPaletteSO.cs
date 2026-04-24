using UnityEngine;

/// <summary>
/// GDD §14.7 — 色覚サポート用カラーパレット。
/// モード別 (OFF / Protan / Deutan / Tritan) に同名スロットを保持する。
/// ColorBlindPaletteService がモード切替時に該当スロットから色を引く。
///
/// Inspector 上で生成する場合は Assets → Create → Peak Plunder → ColorBlind Palette。
/// 未配置でも ColorBlindPaletteService が GDD §14.7 表に基づくハードコードフォールバックを返す。
/// </summary>
[CreateAssetMenu(
    fileName = "ColorBlindPalette",
    menuName = "Peak Plunder/ColorBlind Palette",
    order = 100)]
public class ColorBlindPaletteSO : ScriptableObject
{
    [System.Serializable]
    public sealed class PaletteSet
    {
        // ピン
        public Color PinDanger   = ColorBlindPaletteService.DefaultPinDanger;
        public Color PinRelic    = ColorBlindPaletteService.DefaultPinRelic;
        public Color PinRoute    = ColorBlindPaletteService.DefaultPinRoute;

        // HP バー
        public Color HpSafe      = ColorBlindPaletteService.DefaultHpSafe;
        public Color HpMid       = ColorBlindPaletteService.DefaultHpMid;
        public Color HpDanger    = ColorBlindPaletteService.DefaultHpDanger;

        // バランス
        public Color BalanceSafe   = ColorBlindPaletteService.DefaultBalanceSafe;
        public Color BalanceDanger = ColorBlindPaletteService.DefaultBalanceDanger;
    }

    [Header("モード別パレット")]
    public PaletteSet Off    = new();
    public PaletteSet Protan = new();
    public PaletteSet Deutan = new();
    public PaletteSet Tritan = new();

    public PaletteSet GetByMode(ColorBlindMode mode) => mode switch
    {
        ColorBlindMode.Protan => Protan,
        ColorBlindMode.Deutan => Deutan,
        ColorBlindMode.Tritan => Tritan,
        _                     => Off
    };
}

/// <summary>GDD §14.7 の色覚モード。</summary>
public enum ColorBlindMode
{
    Off    = 0,
    Protan = 1,
    Deutan = 2,
    Tritan = 3
}

/// <summary>GDD §14.7 の色スロット識別子。UI コードから `Service.GetColor(ColorSlot.PinDanger)` で参照。</summary>
public enum ColorSlot
{
    PinDanger,
    PinRelic,
    PinRoute,
    HpSafe,
    HpMid,
    HpDanger,
    BalanceSafe,
    BalanceDanger
}
