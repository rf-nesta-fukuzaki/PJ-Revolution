using UnityEngine;
using TMPro;
using Sandbox.UI;

/// <summary>
/// GDD §14.3 — 標高メーター HUD 要素。
///
/// プレイヤーの現在標高(m) とゾーン名を画面上部中央に表示する。
/// 例: 「1,250m — 神殿遺跡」
///
/// ゾーン判定:
///   §10.2 のゾーン高度レンジに基づく（ベースキャンプ 0-100m, ゾーン1 100-400m, ...）。
///   将来的にゾーン境界は ZoneRegion 等の Trigger ベースに差し替え可能。
/// </summary>
public class AltitudeMeter : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private Transform         _player;
    [SerializeField] private TextMeshProUGUI   _label;

    [Header("表示オプション")]
    [SerializeField] private float _updateInterval = 0.2f;

    // ── GDD §10.2 ゾーン名（山高に対する割合で解決）──────────────
    // 手続き山の実山高(~460m)に追従させるため、絶対メートルではなく MountainProfile の
    // 割合(0=基地,1=山頂)でゾーンを区切る。これにより登攀中に 低地→中腹→高地→山頂 の
    // 全 7 ゾーン名が必ず順に表示される（旧来は絶対値固定で森林帯/岩場帯止まりだった）。
    // 値 = そのゾーンの上限割合（累積）。
    private static readonly (float maxFrac, string name)[] ZoneBands = {
        (0.08f, "ベースキャンプ"),
        (0.28f, "森林帯"),
        (0.48f, "岩場帯"),
        (0.66f, "急壁"),
        (0.80f, "神殿遺跡"),
        (0.94f, "氷壁"),
        (2f,    "山頂遺跡"),
    };

    private float _nextUpdateTime;

    private void Awake()
    {
        if (_player == null)
        {
            var playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null) _player = playerGO.transform;
        }
        PlaceBelowQuota();
    }

    /// <summary>
    /// 標高ラベルが上中央でノルマ/抽出パネルと座標衝突して読めない問題を解消する。
    /// 上端中央アンカーでノルマ表示の下へずらし、上中央を「ノルマ→抽出→標高」の
    /// 整った縦スタックにする（中央集約ミニマル）。シーンアセットは書き換えない（非破壊）。
    /// </summary>
    private void PlaceBelowQuota()
    {
        if (_label == null) return;

        var rt = _label.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -96f);
        _label.color = UiPalette.Cream;
    }

    private void Update()
    {
        if (_player == null || _label == null) return;
        if (Time.time < _nextUpdateTime) return;
        _nextUpdateTime = Time.time + _updateInterval;

        float altitude = _player.position.y;
        string zoneName = ResolveZoneName(altitude);
        _label.text = $"{altitude:N0}m — {zoneName}";

        // GDD §18.2 — profile.json highestAltitude の更新。
        // SaveManager 内部では更新のみでファイル書き込みは遠征終了時にまとめて行う。
        GameServices.Save?.UpdateHighestAltitude(altitude);
    }

    private static string ResolveZoneName(float y)
    {
        // 山高に対する割合へ変換。MountainProfile 未準備時は実測帯(~50-460m)で概算する。
        float frac = MountainProfile.IsReady
            ? MountainProfile.Fraction(y)
            : Mathf.Clamp01(Mathf.InverseLerp(50f, 460f, y));

        for (int i = 0; i < ZoneBands.Length; i++)
            if (frac < ZoneBands[i].maxFrac)
                return ZoneBands[i].name;
        return ZoneBands[ZoneBands.Length - 1].name;
    }
}
