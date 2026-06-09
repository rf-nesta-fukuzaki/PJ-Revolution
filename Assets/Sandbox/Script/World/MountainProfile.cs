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

    /// <summary>登攀ルートの起点 XZ（拠点中心）。HasRoute=true のとき有効。</summary>
    public static Vector2 BaseXZ { get; private set; }

    /// <summary>登攀ルートの終点 XZ（観測山頂）。HasRoute=true のとき有効。</summary>
    public static Vector2 SummitXZ { get; private set; }

    /// <summary>基地→山頂のルート XZ が確定済みか（敵/オブジェクトのゾーン配置に使う）。</summary>
    public static bool HasRoute { get; private set; }

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

    // 観測山頂 XZ は地形ベイク進行に伴い「近傍の低ピーク → 真の山頂」へ飛ぶ。XZ が一定時間動かなく
    // なって初めて HasRoute=true にする（DistributeClimbCourse の climbSummitStableSeconds と同じ考え方）。
    // これをしないと、収束途中の短いルート上に敵が低地へ固まってしまう。
    private static Vector2 _routeStableSummit;
    private static float   _routeStableTimer;
    private const float    RouteStableSeconds = 1.5f;
    private const float    RouteStableMove    = 5f;  // m。これ以上動いたら未確定へ戻す

    /// <summary>
    /// 登攀ルートの XZ アンカー（基地→観測山頂）を公開する。CombinedTerrainConformer が
    /// 毎フレーム最新値で呼ぶ（DistributeClimbCourse と同じ起点・終点）。これにより EnemySpawner 等が
    /// 「ゾーン(標高ステージ)に対応した位置」を実山に沿って解決できる。山頂 XZ が安定するまで
    /// HasRoute は false のまま（早すぎる確定で敵が低地に固まるのを防ぐ）。
    /// </summary>
    public static void PublishRoute(Vector2 baseXZ, Vector2 summitXZ)
    {
        BaseXZ   = baseXZ;
        SummitXZ = summitXZ;

        if ((summitXZ - _routeStableSummit).sqrMagnitude > RouteStableMove * RouteStableMove)
        {
            _routeStableSummit = summitXZ;
            _routeStableTimer  = 0f;
            HasRoute           = false;
            return;
        }

        _routeStableTimer += Time.deltaTime;
        if (_routeStableTimer >= RouteStableSeconds && (summitXZ - baseXZ).sqrMagnitude > 2500f) // 山頂が基地から ≥50m
            HasRoute = true;
    }

    /// <summary>ルート上の XZ アンカー。0=基地, 1=山頂。HasRoute=false 時は BaseXZ を返す。</summary>
    public static Vector2 RoutePointXZ(float fraction01)
        => Vector2.Lerp(BaseXZ, SummitXZ, Mathf.Clamp01(fraction01));

    // Play 開始時に必ず初期状態へ戻す（Enter Play Mode で domain reload を切っていても安全に）。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        BaseY = 50f;
        SummitY = 460f;
        IsReady = false;
        BaseXZ = Vector2.zero;
        SummitXZ = Vector2.zero;
        HasRoute = false;
        _routeStableSummit = Vector2.zero;
        _routeStableTimer = 0f;
    }
}
