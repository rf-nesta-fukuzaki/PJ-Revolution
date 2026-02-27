using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// プレイヤーの HUD を管理する UI マネージャー。
///
/// [管理する UI 要素]
///   - たいまつ燃料ゲージ（Slider + テキスト）
///   - たいまつ ON/OFF 状態テキスト
///   - HP / 酸素 / 空腹スライダー（SurvivalStats 連携）
///   - インタラクトプロンプト（画面中央下）
///   - ダウン中オーバーレイ（「救助を待っています...」）
///
/// [連携]
///   SimpleSpawner / 初期化処理から BindToPlayer() を呼ぶ。
///   null を渡すと購読解除。
/// </summary>
public class UIManager : MonoBehaviour
{
    // ─────────────── Inspector: 燃料ゲージ ───────────────

    [Header("参照設定")]
    [Tooltip("使用する TorchSystem。BindToPlayer() で上書きも可")]
    [SerializeField] private TorchSystem torchSystem;

    [Header("燃料ゲージ")]
    [Tooltip("燃料残量を表示する Slider。不要な場合は空欄")]
    [SerializeField] private Slider fuelSlider;

    [Tooltip("燃料をパーセント表示する TextMeshPro テキスト。不要な場合は空欄")]
    [SerializeField] private TMP_Text fuelText;

    [Header("たいまつ状態表示")]
    [Tooltip("たいまつの ON/OFF 状態を表示する TextMeshPro テキスト。不要な場合は空欄")]
    [SerializeField] private TMP_Text torchStatusText;

    // ─────────────── Inspector: サバイバルステータス ───────────────

    [Header("HP / 酸素 / 空腹ゲージ")]
    [Tooltip("HP を表示する Slider (min=0, max=1 に設定)")]
    [SerializeField] private Slider healthSlider;

    [Tooltip("HP 数値表示。{0} に 0～100 の値が入る。例: 'HP: {0:F0}'")]
    [SerializeField] private TMP_Text healthText;

    [Tooltip("酸素を表示する Slider (min=0, max=1 に設定)")]
    [SerializeField] private Slider oxygenSlider;

    [Tooltip("酸素数値表示。{0} に 0～100 の値が入る。例: '酸素: {0:F0}'")]
    [SerializeField] private TMP_Text oxygenText;

    [Tooltip("空腹度を表示する Slider (min=0, max=1 に設定)")]
    [SerializeField] private Slider hungerSlider;

    [Tooltip("空腹数値表示。{0} に 0～100 の値が入る。例: '食料: {0:F0}'")]
    [SerializeField] private TMP_Text hungerText;

    // ─────────────── Inspector: インタラクトプロンプト ───────────────

    [Header("インタラクトプロンプト (画面中央下)")]
    [Tooltip("インタラクト可能なオブジェクトに近づいた時に表示される TMP_Text")]
    [SerializeField] private TMP_Text interactPromptText;

    // ─────────────── Inspector: ダウン中画面 ───────────────

    [Header("ダウン中オーバーレイ")]
    [Tooltip("ダウン状態の時に表示する Panel GameObject")]
    [SerializeField] private GameObject downedScreenPanel;

    // ─────────────── Inspector: 表示文字列設定 ───────────────

    [Header("表示文字列設定")]
    [Tooltip("燃料テキストのフォーマット。{0} に 0～100 の値が入る。例: '燃料: {0:F0}%'")]
    [SerializeField] private string fuelTextFormat = "燃料: {0:F0}%";

    [Tooltip("たいまつ点灯時の表示テキスト")]
    [SerializeField] private string litStatusLabel = "たいまつ: 点灯中";

    [Tooltip("たいまつ消灯時の表示テキスト")]
    [SerializeField] private string unlitStatusLabel = "たいまつ: 消灯中";

    [Tooltip("HP テキストのフォーマット。{0} に 0～100 の値が入る")]
    [SerializeField] private string healthTextFormat = "HP: {0:F0}";

    [Tooltip("酸素テキストのフォーマット。{0} に 0～100 の値が入る")]
    [SerializeField] private string oxygenTextFormat = "酸素: {0:F0}";

    [Tooltip("食料テキストのフォーマット。{0} に 0～100 の値が入る")]
    [SerializeField] private string hungerTextFormat = "食料: {0:F0}";

    // ─────────────── 内部状態 ───────────────

    private SurvivalStats _boundStats;

    // ─────────────── Unity Lifecycle ───────────────

    private void Start()
    {
        if (interactPromptText != null) interactPromptText.gameObject.SetActive(false);
        if (downedScreenPanel  != null) downedScreenPanel.SetActive(false);

        if (torchSystem != null)
            BindTorchSystem(torchSystem);
        else
            SetHUDVisible(false);
    }

    private void OnDisable()
    {
        UnbindTorchSystem();
        UnbindSurvivalStats();
    }

    // ─────────────── 連携 API ───────────────

    /// <summary>
    /// プレイヤーの TorchSystem と SurvivalStats を HUD に紐付ける。
    /// SimpleSpawner からプレイヤー生成後に呼ぶ。
    /// null を渡すと購読を解除する。
    /// </summary>
    public void BindToPlayer(TorchSystem localTorchSystem, SurvivalStats survivalStats)
    {
        if (localTorchSystem != null || survivalStats != null)
        {
            BindTorchSystem(localTorchSystem);
            BindSurvivalStats(survivalStats);
            SetHUDVisible(true);
        }
        else
        {
            UnbindTorchSystem();
            UnbindSurvivalStats();
            SetHUDVisible(false);
            if (downedScreenPanel != null) downedScreenPanel.SetActive(false);
        }
    }

    /// <summary>
    /// PlayerInteractor からインタラクトプロンプトを更新するために呼ばれる。
    /// null または空文字を渡すと非表示になる。
    /// </summary>
    public void SetInteractPrompt(string promptText)
    {
        if (interactPromptText == null) return;

        bool hasPrompt = !string.IsNullOrEmpty(promptText);
        interactPromptText.gameObject.SetActive(hasPrompt);
        if (hasPrompt) interactPromptText.text = promptText;
    }

    // ─────────────── Torch 購読管理 ───────────────

    private void BindTorchSystem(TorchSystem ts)
    {
        UnbindTorchSystem();

        torchSystem = ts;
        if (torchSystem == null) return;

        torchSystem.OnFuelChanged  += HandleFuelChanged;
        torchSystem.OnTorchToggled += HandleTorchToggled;

        HandleFuelChanged(torchSystem.FuelRatio);
        HandleTorchToggled(torchSystem.IsLit);
    }

    private void UnbindTorchSystem()
    {
        if (torchSystem == null) return;

        torchSystem.OnFuelChanged  -= HandleFuelChanged;
        torchSystem.OnTorchToggled -= HandleTorchToggled;
        torchSystem = null;
    }

    // ─────────────── SurvivalStats 購読管理 ───────────────

    private void BindSurvivalStats(SurvivalStats stats)
    {
        UnbindSurvivalStats();

        _boundStats = stats;
        if (_boundStats == null) return;

        _boundStats.OnHealthChanged   += OnHealthChanged;
        _boundStats.OnOxygenChanged   += OnOxygenChanged;
        _boundStats.OnHungerChanged   += OnHungerChanged;
        _boundStats.OnIsDownedChanged += OnIsDownedChanged;

        // 現在値で即時反映
        OnHealthChanged(0f,    _boundStats.Health);
        OnOxygenChanged(0f,    _boundStats.Oxygen);
        OnHungerChanged(0f,    _boundStats.Hunger);
        OnIsDownedChanged(false, _boundStats.IsDowned);
    }

    private void UnbindSurvivalStats()
    {
        if (_boundStats == null) return;

        _boundStats.OnHealthChanged   -= OnHealthChanged;
        _boundStats.OnOxygenChanged   -= OnOxygenChanged;
        _boundStats.OnHungerChanged   -= OnHungerChanged;
        _boundStats.OnIsDownedChanged -= OnIsDownedChanged;
        _boundStats = null;
    }

    // ─────────────── 表示切り替え ───────────────

    private void SetHUDVisible(bool visible)
    {
        if (fuelSlider      != null) fuelSlider.gameObject.SetActive(visible);
        if (fuelText        != null) fuelText.gameObject.SetActive(visible);
        if (torchStatusText != null) torchStatusText.gameObject.SetActive(visible);
        if (healthSlider    != null) healthSlider.gameObject.SetActive(visible);
        if (healthText      != null) healthText.gameObject.SetActive(visible);
        if (oxygenSlider    != null) oxygenSlider.gameObject.SetActive(visible);
        if (oxygenText      != null) oxygenText.gameObject.SetActive(visible);
        if (hungerSlider    != null) hungerSlider.gameObject.SetActive(visible);
        if (hungerText      != null) hungerText.gameObject.SetActive(visible);
    }

    // ─────────────── イベントハンドラ: Torch ───────────────

    private void HandleFuelChanged(float fuelRatio)
    {
        if (fuelSlider != null) fuelSlider.value = fuelRatio;
        if (fuelText   != null) fuelText.text = string.Format(fuelTextFormat, fuelRatio * 100f);
    }

    private void HandleTorchToggled(bool isLit)
    {
        if (torchStatusText != null)
            torchStatusText.text = isLit ? litStatusLabel : unlitStatusLabel;
    }

    // ─────────────── イベントハンドラ: SurvivalStats ───────────────

    private void OnHealthChanged(float prev, float current)
    {
        if (healthSlider != null) healthSlider.value = current / 100f;
        if (healthText   != null) healthText.text = string.Format(healthTextFormat, current);
    }

    private void OnOxygenChanged(float prev, float current)
    {
        if (oxygenSlider != null) oxygenSlider.value = current / 100f;
        if (oxygenText   != null) oxygenText.text = string.Format(oxygenTextFormat, current);
    }

    private void OnHungerChanged(float prev, float current)
    {
        if (hungerSlider != null) hungerSlider.value = current / 100f;
        if (hungerText   != null) hungerText.text = string.Format(hungerTextFormat, current);
    }

    private void OnIsDownedChanged(bool prev, bool current)
    {
        if (downedScreenPanel != null) downedScreenPanel.SetActive(current);
        if (current) SetInteractPrompt(null);
    }
}
