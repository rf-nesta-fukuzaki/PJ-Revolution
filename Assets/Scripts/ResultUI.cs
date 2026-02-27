using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ゲーム結果（脱出成功 / 全員ダウン）を表示するリザルト画面。
///
/// [動作フロー]
///   1. OnEnable() で GameManager.OnGameStateChanged をSubscribe
///   2. EscapeSuccess または AllDowned 受信時に ShowResult() を呼ぶ
///   3. ResultPanel を SetActive(true) にしてスライドインアニメーションを再生
///   4. リトライ: 現在のシーンを再ロード / 終了: Application.Quit()
///
/// [アニメーション]
///   LeanTween 不使用。Mathf.Lerp をコルーチンで実装し、
///   Y+スライドオフセットの位置から正位置へ 0.4 秒でスライドイン（イーズアウト）。
///
/// [Update() 不使用]
///   イベント駆動設計のため Update() は実装しない。
/// </summary>
public class ResultUI : MonoBehaviour
{
    // ─── Inspector: UI参照 ───────────────────────────────────────────────

    [Header("UI参照")]
    [Tooltip("リザルトパネルの GameObject（最初は非表示にしておく）")]
    [SerializeField] private GameObject リザルトパネル;

    [Tooltip("タイトルテキスト（「✨ 脱出成功！」または「💀 全員ダウン...」を表示）")]
    [SerializeField] private TMP_Text タイトルテキスト;

    [Tooltip("探索時間を表示するテキスト（例: 探索時間: 02:34）")]
    [SerializeField] private TMP_Text 時間テキスト;

    [Tooltip("獲得宝石数を表示するテキスト（例: 獲得宝石: 42個）")]
    [SerializeField] private TMP_Text 宝石テキスト;

    [Tooltip("成功 / 失敗を表すアイコン Image（Sprite を差し替える）")]
    [SerializeField] private Image リザルトアイコン;

    [Tooltip("リトライボタン（現在のシーンを再ロードする）")]
    [SerializeField] private Button リトライボタン;

    [Tooltip("ゲーム終了ボタン（エディタでは再生停止）")]
    [SerializeField] private Button 終了ボタン;

    // ─── Inspector: アニメーション ───────────────────────────────────────

    [Header("アニメーション")]
    [Tooltip("スライドインにかける時間（秒）")]
    [Range(0.1f, 1f)]
    [SerializeField] private float スライドイン時間 = 0.4f;

    [Tooltip("スライドイン開始位置の Y オフセット（ピクセル）。正の値で上から降りてくる")]
    [SerializeField] private float スライドオフセット = 50f;

    // ─── Inspector: 成功/失敗素材 ────────────────────────────────────────

    [Header("成功 / 失敗素材")]
    [Tooltip("脱出成功時に表示するアイコン Sprite")]
    [SerializeField] private Sprite 成功アイコン;

    [Tooltip("全員ダウン時に表示するアイコン Sprite")]
    [SerializeField] private Sprite 失敗アイコン;

    // ─── 内部状態 ────────────────────────────────────────────────────────

    private RectTransform _panelRect;
    private Vector2       _panelAnchoredPos; // 最終表示位置（Awakeで記録）

    // ─── Unity Lifecycle ─────────────────────────────────────────────────

    private void Awake()
    {
        // リザルトパネルは初期状態で非表示にする
        if (リザルトパネル != null)
        {
            _panelRect        = リザルトパネル.GetComponent<RectTransform>();
            // Awake 時点の anchoredPosition を「最終表示位置」として記録する
            _panelAnchoredPos = _panelRect != null ? _panelRect.anchoredPosition : Vector2.zero;
            リザルトパネル.SetActive(false);
        }

        // ボタンリスナーを登録
        if (リトライボタン != null)
            リトライボタン.onClick.AddListener(OnRetryClicked);

        if (終了ボタン != null)
            終了ボタン.onClick.AddListener(OnQuitClicked);
    }

    /// <summary>OnEnable で GameManager のイベントを Subscribe する。</summary>
    private void OnEnable()
    {
        GameManager.OnGameStateChanged += OnGameStateChanged;
    }

    /// <summary>OnDisable で GameManager のイベントを Unsubscribe する（リーク防止）。</summary>
    private void OnDisable()
    {
        GameManager.OnGameStateChanged -= OnGameStateChanged;
    }

    // ─── イベントハンドラ ────────────────────────────────────────────────

    private void OnGameStateChanged(GameState state)
    {
        if (state == GameState.EscapeSuccess || state == GameState.AllDowned)
            ShowResult(state);
    }

    // ─── リザルト表示 ────────────────────────────────────────────────────

    private void ShowResult(GameState state)
    {
        bool isSuccess = state == GameState.EscapeSuccess;

        // ── タイトルテキスト ──
        if (タイトルテキスト != null)
        {
            タイトルテキスト.text  = isSuccess ? "✨ 脱出成功！" : "💀 全員ダウン...";
            タイトルテキスト.color = isSuccess ? Color.yellow : Color.red;
        }

        // ── 探索時間 ──
        if (時間テキスト != null)
        {
            float elapsed = GameManager.Instance != null ? GameManager.Instance.ElapsedTime : 0f;
            int   minutes = (int)elapsed / 60;
            int   seconds = (int)elapsed % 60;
            時間テキスト.text = string.Format("探索時間: {0:00}:{1:00}", minutes, seconds);
        }

        // ── 宝石数 ──
        if (宝石テキスト != null)
        {
            int gems = GameManager.Instance != null ? GameManager.Instance.CollectedGems : 0;
            宝石テキスト.text = $"獲得宝石: {gems}個";
        }

        // ── アイコン ──
        if (リザルトアイコン != null)
        {
            Sprite icon = isSuccess ? 成功アイコン : 失敗アイコン;
            if (icon != null)
                リザルトアイコン.sprite = icon;
        }

        // ── パネル表示 & スライドイン ──
        if (リザルトパネル != null)
        {
            リザルトパネル.SetActive(true);
            StartCoroutine(SlideInCoroutine());
        }
    }

    // ─── スライドインアニメーション ──────────────────────────────────────

    /// <summary>
    /// ResultPanel を Y+スライドオフセットの位置から正位置へ 0.4 秒でスライドインする。
    /// イーズアウト（二次曲線）で自然な減速を表現する。
    /// </summary>
    private IEnumerator SlideInCoroutine()
    {
        if (_panelRect == null) yield break;

        Vector2 startPos = _panelAnchoredPos + Vector2.up * スライドオフセット;
        Vector2 endPos   = _panelAnchoredPos;

        _panelRect.anchoredPosition = startPos;

        float elapsed = 0f;
        while (elapsed < スライドイン時間)
        {
            elapsed += Time.deltaTime;
            float t       = Mathf.Clamp01(elapsed / スライドイン時間);
            // イーズアウト: t の二次曲線で滑らかに減速
            float smoothT = 1f - (1f - t) * (1f - t);
            _panelRect.anchoredPosition = Vector2.Lerp(startPos, endPos, smoothT);
            yield return null;
        }

        // 最終位置を正確にセット
        _panelRect.anchoredPosition = endPos;
    }

    // ─── ボタンハンドラ ──────────────────────────────────────────────────

    private void OnRetryClicked()
    {
        // 現在のシーンを再ロードする
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        // エディタでは再生を停止する
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
