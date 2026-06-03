using UnityEngine;

/// <summary>
/// 山の「プレイ可能な高度帯（基地 → 山頂）」を実行時に公開する共有プロファイル。
///
/// 手続き地形のため山頂の絶対標高はランごとに変わりうる（現状 ~460m / 基地 ~50m）。
/// 旧来このプロジェクトはハザード閾値を絶対メートル(1600m/2000m)で持っていたが、
/// 実山高(~460m)に永遠に届かず、凍傷・高山病・天候クライマックスが一切発火しなかった。
///
/// このクラスは CombinedTerrainConformer が地形ベイク後に BaseY/SummitY を Publish し、
/// 各システム（天候悪化・凍傷・高山病・高度計）が「山に対する割合(0=基地,1=山頂)」で
/// 高度を解釈できるようにする。これによりコアループの
///   低地(操作習熟) → 中腹(天候悪化) → 高地(クライマックス)
/// が実山高に追従して必ず成立する。
/// </summary>
public static class MountainProfile
{
    /// <summary>プレイ可能高度帯の下端（基地 pad 天面の World Y）。</summary>
    public static float BaseY { get; private set; } = 50f;

    /// <summary>プレイ可能高度帯の上端（観測された山頂の World Y）。</summary>
    public static float SummitY { get; private set; } = 460f;

    /// <summary>地形から実測値を取得済みか。未準備時は各システムが絶対値フォールバックを使う。</summary>
    public static bool IsReady { get; private set; }

    // 基地と山頂が「意味のある差」になるまで Ready にしない（原点近傍の低ピーク誤認を避ける）。
    private const float MinMeaningfulSpread = 80f;

    /// <summary>
    /// 地形ベイクの進行に合わせて毎フレーム呼ぶ。GlobalMaxY は単調増加で真の山頂へ収束するため、
    /// SummitY は観測最大値を採用する（遠い高チャンクが後から焼けても追従する）。
    /// </summary>
    public static void Publish(float baseY, float observedSummitY)
    {
        BaseY = baseY;
        if (!IsReady)
            SummitY = Mathf.Max(observedSummitY, baseY + MinMeaningfulSpread);
        else if (observedSummitY > SummitY)
            SummitY = observedSummitY;

        if (observedSummitY - baseY >= MinMeaningfulSpread)
            IsReady = true;
    }

    /// <summary>World Y を山に対する割合 [0,1] へ。0=基地, 1=山頂。</summary>
    public static float Fraction(float worldY)
        => SummitY > BaseY ? Mathf.Clamp01((worldY - BaseY) / (SummitY - BaseY)) : 0f;

    /// <summary>割合 [0,1] を World Y へ。閾値を「山頂からの割合」で指定するのに使う。</summary>
    public static float WorldYAtFraction(float fraction01)
        => Mathf.Lerp(BaseY, SummitY, Mathf.Clamp01(fraction01));

    // Play 開始時に必ず初期状態へ戻す（Enter Play Mode で domain reload を切っていても安全に）。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        BaseY = 50f;
        SummitY = 460f;
        IsReady = false;
    }
}
