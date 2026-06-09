using Sandbox.UI;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PeakPlunder.Localization;

/// <summary>
/// GDD §21.3 — ベースキャンプ初回来訪時のショップ操作チュートリアル。
/// </summary>
[DisallowMultipleComponent]
public class ShopTutorialOverlay : MonoBehaviour
{
    public static ShopTutorialOverlay Instance { get; private set; }

    private static readonly string[] STEP_KEYS =
    {
        LocalizationKeys.TutorialShopStep1,
        LocalizationKeys.TutorialShopStep2,
        LocalizationKeys.TutorialShopStep3,
        LocalizationKeys.TutorialShopStep4,
    };

    private static readonly string[] STEP_FALLBACK_JA =
    {
        "予算 100pt はチーム共有です。早い者勝ちで誰でも買えます。",
        "アイテム行をクリックで購入、再クリックで返品できます。装備数の上限に注意。",
        "各アイテムの説明を読み、遠征ルートに合わせて組み合わせましょう。",
        "準備ができたら「出発」ボタンで遠征開始！ 予算や装備は持ち越せません。",
    };

    [Header("UI 参照（未設定なら動的生成）")]
    [SerializeField] private GameObject      _root;
    [SerializeField] private TextMeshProUGUI _stepLabel;
    [SerializeField] private TextMeshProUGUI _counterLabel;
    [SerializeField] private Button          _nextButton;
    [SerializeField] private Button          _skipButton;

    private int _stepIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (_root == null)
            BuildRuntimeUiIfMissing();

        if (_root != null) _root.SetActive(false);
        if (_nextButton != null) _nextButton.onClick.AddListener(OnNextClicked);
        if (_skipButton != null) _skipButton.onClick.AddListener(OnSkipClicked);
    }

    /// <summary>Shop シーン等、Inspector 未配線時にオーバーレイ UI を生成する。</summary>
    private void BuildRuntimeUiIfMissing()
    {
        var canvasGo = new GameObject("ShopTutorialCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        _root = new GameObject("TutorialRoot");
        _root.transform.SetParent(canvasGo.transform, false);
        var rootRt = _root.AddComponent<RectTransform>();
        FlowUiTheme.Stretch(rootRt);
        var dim = _root.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);

        var panel = FlowUiTheme.CreateTerminalPanel(_root.transform, "TutorialPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-360f, -180f), new Vector2(360f, 180f));

        MenuUiKit.CreateTitleText(panel, "TutorialHeader", "BASE CAMP GUIDE", 32,
            new Vector2(0.5f, 0.82f), FlowUiTheme.TerminalAccent);

        _stepLabel = CreateTutorialLabel(panel, "StepLabel", "", 22,
            new Vector2(0.5f, 0.52f), new Vector2(620f, 140f));
        _counterLabel = CreateTutorialLabel(panel, "Counter", "1/4", 18,
            new Vector2(0.5f, 0.22f), new Vector2(200f, 32f), UiPalette.Amber);

        _nextButton = CreateTutorialButton(panel, "Next", "次へ",
            new Vector2(120f, -130f), MenuUiKit.BtnPrimary);
        _skipButton = CreateTutorialButton(panel, "Skip", "スキップ",
            new Vector2(-120f, -130f), MenuUiKit.BtnNeutral);
    }

    private static TextMeshProUGUI CreateTutorialLabel(Transform parent, string name, string text,
        int size, Vector2 anchor, Vector2 boxSize, Color? color = null)
    {
        var tmp = MenuUiKit.CreateBodyText(parent, name, text, size, anchor, color ?? UiPalette.Cream);
        tmp.rectTransform.sizeDelta = boxSize;
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    private static Button CreateTutorialButton(Transform parent, string name, string label,
        Vector2 pos, Color fill)
    {
        var btn = MenuUiKit.CreateMenuButton(parent, name, label,
            new Vector2(0.5f, 0.5f), new Vector2(180f, 52f), fill, null);
        btn.GetComponent<RectTransform>().anchoredPosition = pos;
        return btn;
    }

    private void OnDestroy()
    {
        if (_nextButton != null) _nextButton.onClick.RemoveListener(OnNextClicked);
        if (_skipButton != null) _skipButton.onClick.RemoveListener(OnSkipClicked);
        if (Instance == this) Instance = null;
    }

    public void ShowIfFirstTime()
    {
        var save = GameServices.Save;
        if (save != null && save.IsShopGuideCompleted()) return;
        Show();
    }

    public void Show()
    {
        _stepIndex = 0;
        if (_root != null) _root.SetActive(true);
        UpdateStepDisplay();
    }

    public void HideSilently()
    {
        if (_root != null) _root.SetActive(false);
    }

    private void OnNextClicked()
    {
        _stepIndex++;
        if (_stepIndex >= STEP_KEYS.Length)
        {
            Complete();
            return;
        }
        UpdateStepDisplay();
    }

    private void OnSkipClicked() => Complete();

    private void UpdateStepDisplay()
    {
        if (_stepIndex < 0 || _stepIndex >= STEP_KEYS.Length) return;
        if (_stepLabel    != null) _stepLabel.text    = ResolveStepText(_stepIndex);
        if (_counterLabel != null) _counterLabel.text = $"{_stepIndex + 1}/{STEP_KEYS.Length}";
    }

    private static string ResolveStepText(int index)
    {
        if (index < 0 || index >= STEP_KEYS.Length) return string.Empty;

        string localized = LocalizedText.Get(STEP_KEYS[index], LocalizationKeys.TableHint);
        if (!string.IsNullOrEmpty(localized) && localized != STEP_KEYS[index])
            return localized;

        return STEP_FALLBACK_JA[index];
    }

    private void Complete()
    {
        GameServices.Save?.MarkShopGuideCompleted();
        if (_root != null) _root.SetActive(false);
        Debug.Log("[ShopTutorial] 完了記録を保存しました");
    }

    public static int StepCount => STEP_KEYS.Length;

    public static string GetStepText(int index) => ResolveStepText(index);
}
