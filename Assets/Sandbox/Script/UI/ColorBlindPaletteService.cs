using System;
using UnityEngine;

/// <summary>
/// GDD §14.7 — 色覚サポートのランタイム仲介。
/// 各 UI 要素が `ColorBlindPaletteService.Instance.GetColor(ColorSlot.PinDanger)` で色を引き、
/// OnPaletteChanged を購読して設定変更時に自動更新する。
///
/// SettingsManager がユーザー変更を拾って `SetMode(...)` を呼ぶ。
/// ScriptableObject が未アサインの場合は GDD §14.7 表の Hex 値を使ったハードコードパレットで動作する。
/// </summary>
[DisallowMultipleComponent]
public class ColorBlindPaletteService : MonoBehaviour
{
    public static ColorBlindPaletteService Instance { get; private set; }

    // ── GDD §14.7: 通常色（Off モード）──────────────────────────
    public static readonly Color DefaultPinDanger     = Hex(0xFF4444);
    public static readonly Color DefaultPinRelic      = Hex(0x4488FF);
    public static readonly Color DefaultPinRoute      = Hex(0xFFCC00);
    public static readonly Color DefaultHpSafe        = Hex(0x44FF44);
    public static readonly Color DefaultHpMid         = Hex(0xFFCC00);
    public static readonly Color DefaultHpDanger      = Hex(0xFF4444);
    public static readonly Color DefaultBalanceSafe   = Hex(0x44FF44);
    public static readonly Color DefaultBalanceDanger = Hex(0xFF4444);

    [SerializeField] private ColorBlindPaletteSO _palette;

    /// <summary>現在のモード。切替時に OnPaletteChanged が発火する。</summary>
    public ColorBlindMode CurrentMode { get; private set; } = ColorBlindMode.Off;

    /// <summary>パレット変更通知。UI 側が Subscribe して再着色する。</summary>
    public event Action OnPaletteChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// モードを切り替える（設定画面から呼ばれる想定）。
    /// 値が変化した場合のみ OnPaletteChanged を発火する。
    /// </summary>
    public void SetMode(ColorBlindMode mode)
    {
        if (CurrentMode == mode) return;
        CurrentMode = mode;
        OnPaletteChanged?.Invoke();
    }

    /// <summary>数値 (0..3) で指定する場合のショートハンド。</summary>
    public void SetModeInt(int mode)
    {
        ColorBlindMode m = mode switch
        {
            1 => ColorBlindMode.Protan,
            2 => ColorBlindMode.Deutan,
            3 => ColorBlindMode.Tritan,
            _ => ColorBlindMode.Off
        };
        SetMode(m);
    }

    /// <summary>指定スロットの現在モード色を返す。</summary>
    public Color GetColor(ColorSlot slot)
    {
        // SO があれば SO 値、なければ GDD §14.7 表のハードコードフォールバック。
        if (_palette != null)
        {
            var set = _palette.GetByMode(CurrentMode);
            return set switch
            {
                _ => slot switch
                {
                    ColorSlot.PinDanger     => set.PinDanger,
                    ColorSlot.PinRelic      => set.PinRelic,
                    ColorSlot.PinRoute      => set.PinRoute,
                    ColorSlot.HpSafe        => set.HpSafe,
                    ColorSlot.HpMid         => set.HpMid,
                    ColorSlot.HpDanger      => set.HpDanger,
                    ColorSlot.BalanceSafe   => set.BalanceSafe,
                    ColorSlot.BalanceDanger => set.BalanceDanger,
                    _                       => Color.white
                }
            };
        }

        return GetDefault(slot, CurrentMode);
    }

    /// <summary>
    /// GDD §14.7 の「色覚サポートの色変換マッピング」表に基づくハードコード値。
    /// SO が未アサインでも正しい色が返るように保証する。
    /// </summary>
    public static Color GetDefault(ColorSlot slot, ColorBlindMode mode)
    {
        // Tritan 以外（Protan/Deutan）は Pin(危険)・HP(危険)・Balance(危険) がオレンジに、
        // HP(安全)・Balance(安全) がシアンにシフトする。
        bool isProtanOrDeutan = mode == ColorBlindMode.Protan || mode == ColorBlindMode.Deutan;
        bool isTritan         = mode == ColorBlindMode.Tritan;

        return slot switch
        {
            ColorSlot.PinDanger     => isProtanOrDeutan ? Hex(0xFF8800)
                                     : isTritan        ? Hex(0xFF00FF)
                                                       : DefaultPinDanger,
            ColorSlot.PinRelic      => isTritan        ? Hex(0x00CCCC) : DefaultPinRelic,
            ColorSlot.PinRoute      => DefaultPinRoute,
            ColorSlot.HpSafe        => isProtanOrDeutan ? Hex(0x00DDDD) : DefaultHpSafe,
            ColorSlot.HpMid         => DefaultHpMid,
            ColorSlot.HpDanger      => isProtanOrDeutan ? Hex(0xFF8800)
                                     : isTritan        ? Hex(0xFF00FF)
                                                       : DefaultHpDanger,
            ColorSlot.BalanceSafe   => isProtanOrDeutan ? Hex(0x00DDDD) : DefaultBalanceSafe,
            ColorSlot.BalanceDanger => isProtanOrDeutan ? Hex(0xFF8800)
                                     : isTritan        ? Hex(0xFF00FF)
                                                       : DefaultBalanceDanger,
            _                       => Color.white
        };
    }

    // ── ヘルパー ─────────────────────────────────────────────
    private static Color Hex(uint rgb)
    {
        float r = ((rgb >> 16) & 0xFF) / 255f;
        float g = ((rgb >>  8) & 0xFF) / 255f;
        float b = ( rgb        & 0xFF) / 255f;
        return new Color(r, g, b, 1f);
    }
}
