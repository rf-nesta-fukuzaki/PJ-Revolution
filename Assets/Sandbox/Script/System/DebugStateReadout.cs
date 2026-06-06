using System.Text;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// デバッグメニューに表示する「実装機能の現在値」をリアルタイムに文字列化するヘルパー。
/// 各システムの読み取り専用 API のみを参照し、状態を変更しない（観測専用）。
/// </summary>
public static class DebugStateReadout
{
    private static readonly StringBuilder s_sb = new(512);

    private static float s_fps;
    private static int   s_fpsFrame = -1;

    // ── リッチテキスト色（UiPalette と整合する簡易トーン） ──
    private const string COL_OK   = "#9BE08A"; // 緑
    private const string COL_WARN = "#F2C14E"; // アンバー
    private const string COL_BAD  = "#E86A5C"; // 赤
    private const string COL_DIM  = "#B7AFA0"; // 副次

    /// <summary>全システムの現在値を1つのリッチテキスト文字列にまとめて返す。</summary>
    public static string Build()
    {
        UpdateFps();

        s_sb.Clear();

        AppendPlayerVitals();
        AppendPositionAltitude();
        AppendEnvironmentEffects();
        AppendWeather();
        AppendExpedition();
        AppendProgression();
        AppendWorld();

        return s_sb.ToString();
    }

    private static void AppendPlayerVitals()
    {
        var health = DebugLocalPlayer.Health();
        if (health == null)
        {
            Line("プレイヤー", Dim("未検出"));
            return;
        }

        string state = "Alive";
        if (health.IsDead)        state = Color("Ghost(死亡)", COL_BAD);
        else if (health.IsDowned) state = Color("Downed(瀕死)", COL_WARN);

        Line("プレイヤー", state);
        Bar("HP", health.CurrentHp, health.MaxHp);

        var stamina = DebugLocalPlayer.Stamina();
        if (stamina != null)
        {
            Bar("気力", stamina.CurrentStamina, stamina.MaxStamina);
            if (stamina.IsExhausted) Append("  " + Color("[疲労失神リスク]", COL_BAD));
        }
    }

    private static void AppendPositionAltitude()
    {
        var root = DebugLocalPlayer.Root();
        if (root == null) return;

        Vector3 p = root.transform.position;
        float fraction = MountainProfile.IsReady ? MountainProfile.Fraction(p.y) : 0f;
        string altText = $"{p.y:F0}m ({fraction * 100f:F0}%)";

        var rb = root.GetComponent<Rigidbody>();
        string vy = rb != null ? $"  落下 {rb.linearVelocity.y:+0.0;-0.0}m/s" : string.Empty;

        Line("高度", altText + vy);
        Line("座標", Dim($"({p.x:F0}, {p.y:F0}, {p.z:F0})"));
    }

    private static void AppendEnvironmentEffects()
    {
        var stamina = DebugLocalPlayer.Stamina();
        bool oxygen = stamina != null && stamina.HasOxygenTank;

        var sickness = DebugLocalPlayer.Component<AltitudeSicknessEffect>();
        var frost    = DebugLocalPlayer.Component<FrostbiteDamage>();

        string ox = oxygen ? Color("酸素タンク✓", COL_OK) : Dim("酸素なし");
        string sick = sickness != null && sickness.IsAffected ? Color("高山病●", COL_BAD) : Dim("高山病なし");
        string fb = frost != null && frost.IsAtRiskAltitude && !frost.IsShelteredPublic
            ? Color("凍傷リスク●", COL_BAD)
            : Dim("凍傷なし");

        Line("環境", $"{ox}  {sick}  {fb}");
    }

    private static void AppendWeather()
    {
        var weather = GameServices.Weather as WeatherSystem;
        if (weather == null) { Line("天候", Dim("N/A")); return; }

        Line("天候", $"{weather.CurrentWeather}  風 {weather.WindSpeed:F1}m/s  滑り {weather.GetSliperiness():F2}");
    }

    private static void AppendExpedition()
    {
        var exp = GameServices.Expedition;
        string phase = exp != null ? exp.Phase.ToString() : "N/A";

        var timer = GameServices.Timer;
        string time = timer != null ? timer.GetFormattedTime() : "--:--";

        string net = "未接続";
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening) net = nm.IsHost ? "Host" : "Client";

        Line("遠征", $"{phase}  経過 {time}  ネット {Dim(net)}");
    }

    private static void AppendProgression()
    {
        var quota = ExtractionQuotaSystem.Instance;
        if (quota != null)
        {
            int peek = quota.PeekExtractedValue();
            string col = peek >= quota.RequiredQuota ? COL_OK : COL_WARN;
            Line("ノルマ", $"Lv{quota.Level}  搬入 {Color($"{peek}", col)}/{quota.RequiredQuota}pt");
        }

        var score = GameServices.Score as ScoreTracker;
        string relics = score != null ? $"  回収遺物 {score.CollectedRelicCount}" : string.Empty;
        Line("所持金", $"{CurrencyWallet.Balance}{Dim(relics)}");
    }

    private static void AppendWorld()
    {
        int enemies = CountActive<EnemyController>();
        int players = CountActive<PlayerHealthSystem>();
        float scale = Time.timeScale;
        string scaleStr = Mathf.Approximately(scale, 1f) ? "1.0" : Color($"{scale:F2}", COL_WARN);

        Line("ワールド", $"敵 {enemies}体  人 {players}  時間 x{scaleStr}  {s_fps:F0}fps");
    }

    // ── 整形ユーティリティ ──────────────────────────────────
    private static int CountActive<T>() where T : Component
    {
        var all = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
        int n = 0;
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null) n++;
        return n;
    }

    private static void Bar(string label, float value, float max)
    {
        float ratio = max > 0f ? Mathf.Clamp01(value / max) : 0f;
        string col = ratio > 0.5f ? COL_OK : ratio > 0.25f ? COL_WARN : COL_BAD;
        Append($"{Dim(label)} {Color($"{value:F0}", col)}/{max:F0}   ");
    }

    private static void Line(string label, string value)
    {
        if (s_sb.Length > 0) s_sb.Append('\n');
        s_sb.Append(Dim(label)).Append("  ").Append(value);
    }

    private static void Append(string text) => s_sb.Append(text);

    private static string Color(string text, string hex) => $"<color={hex}>{text}</color>";
    private static string Dim(string text) => Color(text, COL_DIM);

    private static void UpdateFps()
    {
        if (s_fpsFrame == Time.frameCount) return;
        s_fpsFrame = Time.frameCount;
        float dt = Time.unscaledDeltaTime;
        float instant = dt > 0f ? 1f / dt : 0f;
        s_fps = s_fps <= 0f ? instant : Mathf.Lerp(s_fps, instant, 0.1f);
    }
}
