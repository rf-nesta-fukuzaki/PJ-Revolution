using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// GDD §21.2 — コンテキストヒントシステム。
///
/// プレイ中の特定条件でヒントテキストを画面中央下に表示。
/// 各ヒントは初回のみ（profile.json の seenHints に記録済みなら非表示）。
/// 設定の「チュートリアルヒント表示」が OFF の場合は全件スキップ。
///
/// 外部から呼ぶ方法:
///   HintManager.Instance.TriggerHint(HintId.FirstClimbApproach);
///
/// 自動トリガーは各システムが判断して呼び出す:
///   - ClimbingController → HintId.FirstClimbApproach
///   - ExplorerController → HintId.DashIntroduction (5秒後)
///   - StaminaSystem      → HintId.StaminaDepleted
///   - PlayerInteraction  → HintId.RelicApproach, HintId.RelicWithClimb
///   - 他
/// </summary>
public class HintManager : MonoBehaviour
{
    public static HintManager Instance { get; private set; }

    // ── ヒント ID（GDD §21.2 の番号に対応）──────────────────
    public static class HintId
    {
        public const int FirstClimbApproach    = 1;
        public const int DashIntroduction      = 2;
        public const int StaminaDepleted       = 3;
        public const int RelicApproach         = 4;
        public const int RelicWithClimb        = 5;
        public const int RopePlayerNearby      = 6;
        public const int Zone2Entry            = 7;
        public const int ReturnOrZone3         = 8;
    }

    // ── ヒントテキスト（GDD §21.2 テーブル）─────────────────
    private static readonly string[] HINT_TEXTS = new string[]
    {
        "",   // [0] 未使用（1-indexed）
        "左クリックで掴もう！黄色いポイントに手を伸ばして",
        "Shiftでダッシュ！ ただしスタミナに注意",
        "スタミナが切れた！壁から手が離れます",
        "遺物を発見！左クリックで掴んで運ぼう。Gキーで丁寧に置けます",
        "遺物を持ったままでは壁を登れません。Gキーで置いてから登りましょう",
        "ロープでチームメイトとつながろう！近づいてEキーで接続",
        "Fキーでマーカーを設置！チームに危険やルートを知らせよう",
        "ベースキャンプに戻るか、フレアガンでヘリを呼ぼう（上空に向けて発射！）",
    };

    // ── Inspector ─────────────────────────────────────────────
    [Header("UI")]
    [SerializeField] private GameObject       _hintRoot;      // ヒントパネル全体
    [SerializeField] private TextMeshProUGUI  _hintText;

    [Header("表示設定")]
    [SerializeField] private float _displayDuration = 5f;    // 表示秒数
    [SerializeField] private float _fadeDuration    = 0.5f;  // フェード秒数

    // ── 状態 ─────────────────────────────────────────────────
    private bool _isShowing;

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // Unity fake-null を正しく検出するため ?. ではなく != null を使用
        if (_hintRoot != null) _hintRoot.SetActive(false);
    }

    // ── ヒントトリガー（外部から呼ぶ）───────────────────────
    /// <summary>ヒントを表示する。未表示かつ有効な場合のみ表示される。</summary>
    public void TriggerHint(int hintId)
    {
        Debug.Assert(hintId >= 1 && hintId < HINT_TEXTS.Length,
            $"[Contract] HintManager.TriggerHint: hintId 範囲外 ({hintId})");
        if (!ShouldShow(hintId)) return;

        // 既読として記録（GameServices.Save 経由）
        GameServices.Save?.AddSeenHint(hintId);

        string text = (hintId >= 1 && hintId < HINT_TEXTS.Length)
            ? HINT_TEXTS[hintId]
            : string.Empty;

        if (string.IsNullOrEmpty(text)) return;

        StartCoroutine(ShowHintCoroutine(text));
    }

    private bool ShouldShow(int hintId)
    {
        // 設定で無効化されている（GameServices.Save 経由）
        var save = GameServices.Save;
        if (save != null && !save.IsTutorialHintsEnabled())
            return false;

        // 既読
        if (save != null && save.HasSeenHint(hintId))
            return false;

        // 範囲外
        return hintId >= 1 && hintId < HINT_TEXTS.Length;
    }

    // ── 表示コルーチン ────────────────────────────────────────
    private IEnumerator ShowHintCoroutine(string text)
    {
        // 前のヒントが表示中なら待機
        while (_isShowing) yield return null;

        _isShowing = true;

        if (_hintText != null) _hintText.text = text;
        if (_hintRoot != null) _hintRoot.SetActive(true);

        CanvasGroup canvasGroup = null;
        if (_hintRoot != null)
        {
            canvasGroup = _hintRoot.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = _hintRoot.AddComponent<CanvasGroup>();
        }

        // フェードイン
        yield return Fade(canvasGroup, 0f, 1f, _fadeDuration);

        // 表示維持
        yield return new WaitForSeconds(_displayDuration);

        // フェードアウト
        yield return Fade(canvasGroup, 1f, 0f, _fadeDuration);

        if (_hintRoot != null) _hintRoot.SetActive(false);
        _isShowing = false;
    }

    private static IEnumerator Fade(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null) yield break;

        float t = 0f;
        while (t < duration)
        {
            t      += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        cg.alpha = to;
    }

    // ── SaveManager 依存を避けたフォールバック（テスト用）──
    /// <summary>SaveManager なしで直接表示する（デバッグ・テスト用）。</summary>
    public void ForceShowHint(int hintId)
    {
        if (hintId < 1 || hintId >= HINT_TEXTS.Length) return;
        StopAllCoroutines();
        _isShowing = false;
        StartCoroutine(ShowHintCoroutine(HINT_TEXTS[hintId]));
    }
}
