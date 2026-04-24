using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GDD §21.3 — ベースキャンプ初回来訪時のショップ操作チュートリアル。
///
/// 4 ステップのテキストと「次へ」「スキップ」ボタンを表示する。
/// 完了またはスキップ時は <see cref="SaveManager.MarkShopGuideCompleted"/> を呼び、
/// 次回以降は表示しない。
///
/// 呼び出し側:
///   BasecampShop.Start() から <see cref="ShowIfFirstTime"/>。
/// </summary>
[DisallowMultipleComponent]
public class ShopTutorialOverlay : MonoBehaviour
{
    public static ShopTutorialOverlay Instance { get; private set; }

    // ── GDD §21.3 の 4 ステップ文言（表の要約）───────────────
    private static readonly string[] STEPS = new string[]
    {
        "予算 100pt はチーム共有です。早い者勝ちで誰でも買えます。",
        "アイテム行をクリックで購入、再クリックで返品できます。装備数の上限に注意。",
        "各アイテムの説明を読み、遠征ルートに合わせて組み合わせましょう。",
        "準備ができたら「出発」ボタンで遠征開始！ 予算や装備は持ち越せません。",
    };

    // ── Inspector 参照 ────────────────────────────────────────
    [Header("UI 参照（未設定なら動的生成）")]
    [SerializeField] private GameObject      _root;
    [SerializeField] private TextMeshProUGUI _stepLabel;
    [SerializeField] private TextMeshProUGUI _counterLabel;   // "1/4" 等
    [SerializeField] private Button          _nextButton;
    [SerializeField] private Button          _skipButton;

    // ── 状態 ────────────────────────────────────────────────
    private int _stepIndex;

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (_root != null) _root.SetActive(false);

        if (_nextButton != null) _nextButton.onClick.AddListener(OnNextClicked);
        if (_skipButton != null) _skipButton.onClick.AddListener(OnSkipClicked);
    }

    private void OnDestroy()
    {
        if (_nextButton != null) _nextButton.onClick.RemoveListener(OnNextClicked);
        if (_skipButton != null) _skipButton.onClick.RemoveListener(OnSkipClicked);
        if (Instance == this) Instance = null;
    }

    // ── 公開 API ─────────────────────────────────────────────
    /// <summary>プロフィールに完了記録が無い場合のみ表示する。</summary>
    public void ShowIfFirstTime()
    {
        var save = GameServices.Save;
        if (save != null && save.IsShopGuideCompleted()) return;
        Show();
    }

    /// <summary>強制的にステップ 1 から表示する（デバッグ・強制再表示用）。</summary>
    public void Show()
    {
        _stepIndex = 0;
        if (_root != null) _root.SetActive(true);
        UpdateStepDisplay();
    }

    /// <summary>即座に閉じる。完了フラグは立てない。</summary>
    public void HideSilently()
    {
        if (_root != null) _root.SetActive(false);
    }

    // ── ボタンコールバック ──────────────────────────────────
    private void OnNextClicked()
    {
        _stepIndex++;
        if (_stepIndex >= STEPS.Length)
        {
            Complete();
            return;
        }
        UpdateStepDisplay();
    }

    private void OnSkipClicked()
    {
        Complete();
    }

    // ── 内部処理 ─────────────────────────────────────────────
    private void UpdateStepDisplay()
    {
        if (_stepIndex < 0 || _stepIndex >= STEPS.Length) return;
        if (_stepLabel    != null) _stepLabel.text    = STEPS[_stepIndex];
        if (_counterLabel != null) _counterLabel.text = $"{_stepIndex + 1}/{STEPS.Length}";
    }

    private void Complete()
    {
        GameServices.Save?.MarkShopGuideCompleted();
        if (_root != null) _root.SetActive(false);
        Debug.Log("[ShopTutorial] 完了記録を保存しました");
    }

    // ── ステップ文言へのアクセス（テスト・外部参照用）────────
    public static int StepCount => STEPS.Length;
    public static string GetStepText(int index)
    {
        if (index < 0 || index >= STEPS.Length) return string.Empty;
        return STEPS[index];
    }
}
