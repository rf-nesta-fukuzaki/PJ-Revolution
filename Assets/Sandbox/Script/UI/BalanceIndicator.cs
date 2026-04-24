using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GDD §4.4 / §14.3 — 遺物運搬時の HUD バランスインジケーター。
/// 遺物を持っている間だけ表示される円形の水準器アイコン。
///
/// 中央 = 安定（緑）、外周 = 転倒リスク（黄 → 赤）。
/// RelicCarrier コンポーネントの傾き情報（relic.transform.up vs Vector3.up）から
/// バランスを計算してインジケーターの針を更新する。
/// </summary>
public class BalanceIndicator : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────
    [Header("UI 参照")]
    [SerializeField] private GameObject _indicatorRoot;   // ルートパネル（非運搬時は非表示）
    [SerializeField] private RectTransform _needle;       // 中央の針（RectTransform）
    [SerializeField] private Image    _outerRing;         // 外枠リング（色変化）
    [SerializeField] private Image    _background;        // 背景円

    [Header("表示設定")]
    [SerializeField] private float _maxNeedleOffset = 40f;  // 針の最大移動距離（px）
    [SerializeField] private float _smoothSpeed     = 8f;   // 針の追従速度

    // ── 色テーブル（GDD §14.3）────────────────────────────────
    private static readonly Color COLOR_STABLE    = new(0.2f, 0.9f, 0.2f);   // 緑：中央
    private static readonly Color COLOR_CAUTION   = new(1f,   0.8f, 0f);     // 黄：注意
    private static readonly Color COLOR_DANGER    = new(1f,   0.2f, 0.1f);   // 赤：危険

    // ── 閾値 ─────────────────────────────────────────────────
    private const float CAUTION_ANGLE = 20f;   // これ以上の傾きで黄色
    private const float DANGER_ANGLE  = 40f;   // これ以上の傾きで赤
    private const float CRITICAL_ANGLE = 45f;  // GDD §4.4 — 担架スリップ判定角度

    // ── 状態 ─────────────────────────────────────────────────
    private RelicCarrier _trackedCarrier;
    private Vector2      _currentOffset;

    // ── プレイヤー参照（PlayerInteraction 経由で注入） ─────────
    public void SetTrackedCarrier(RelicCarrier carrier)
    {
        _trackedCarrier = carrier;
        _indicatorRoot?.SetActive(carrier != null);
    }

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        _indicatorRoot?.SetActive(false);
    }

    private void Update()
    {
        if (_trackedCarrier == null || _trackedCarrier.gameObject == null)
        {
            _indicatorRoot?.SetActive(false);
            _trackedCarrier = null;
            return;
        }

        UpdateIndicator();
    }

    // ── インジケーター更新 ────────────────────────────────────
    private void UpdateIndicator()
    {
        // 遺物の傾き角度を計算（transform.up と World Up の角度差）
        float tiltAngle = Vector3.Angle(_trackedCarrier.transform.up, Vector3.up);

        // 傾き方向を 2D オフセットに変換
        Vector3 tiltDir = Vector3.ProjectOnPlane(
            _trackedCarrier.transform.up - Vector3.up, Vector3.up).normalized;

        float normalizedTilt = Mathf.Clamp01(tiltAngle / CRITICAL_ANGLE);
        Vector2 targetOffset = new Vector2(tiltDir.x, tiltDir.z) *
                               (normalizedTilt * _maxNeedleOffset);

        // 針をスムーズに移動
        _currentOffset = Vector2.Lerp(_currentOffset, targetOffset,
                                      Time.deltaTime * _smoothSpeed);

        if (_needle != null)
            _needle.anchoredPosition = _currentOffset;

        // 色を更新
        Color tiltColor;
        if (tiltAngle >= DANGER_ANGLE)
            tiltColor = COLOR_DANGER;
        else if (tiltAngle >= CAUTION_ANGLE)
            tiltColor = Color.Lerp(COLOR_CAUTION, COLOR_DANGER,
                                   (tiltAngle - CAUTION_ANGLE) / (DANGER_ANGLE - CAUTION_ANGLE));
        else
            tiltColor = Color.Lerp(COLOR_STABLE, COLOR_CAUTION,
                                   tiltAngle / CAUTION_ANGLE);

        if (_outerRing  != null) _outerRing.color  = tiltColor;
        if (_background != null) _background.color = new Color(tiltColor.r, tiltColor.g, tiltColor.b, 0.15f);
    }

    // ── 外部からの可視性制御 ──────────────────────────────────
    public void Show(RelicCarrier carrier) => SetTrackedCarrier(carrier);
    public void Hide()                      => SetTrackedCarrier(null);
}
