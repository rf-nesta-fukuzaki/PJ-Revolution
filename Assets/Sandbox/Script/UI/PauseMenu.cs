using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using PeakPlunder.Audio;
using Sandbox.UI;

/// <summary>
/// GDD §17.3 — ポーズメニュー（Esc メニュー）。
///
/// 仕様:
///   - Esc キー / ゲームパッド Start でメニューを開閉
///   - UI は実行時に <see cref="PauseMenuView"/> が生成するため、シーン側の参照配線は不要
///   - ページ遷移は <see cref="PauseMenuNavigation"/> の純粋ロジックで「常に 1 段戻る」を保証
///       Menu  --Esc-->  Resume（ゲームへ戻る）
///       Settings / ConfirmLeave  --Esc-->  Menu
///   - Co-op 中（ネットワーク稼働時）は Time.timeScale を変更しない（ゲームは継続）
///   - オフライン時のみ Time.timeScale = 0 で停止
///   - 開閉はフェード + カードのポップで演出（Time.timeScale=0 でも動くよう unscaled 時間）
///   - 設定は GameServices.Settings を介して開閉し、外部クローズも検知してメニューへ戻す
///
/// 使用法:
///   シーンの任意 GameObject に本コンポーネントを 1 つ置くだけでよい。
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [Header("遷移先")]
    [SerializeField] private string _mainMenuScene = "MainMenu";

    [Header("演出")]
    [Tooltip("開閉フェード時間（秒・unscaled）")]
    [SerializeField] private float _fadeDuration = 0.16f;

    public event Action OnPaused;
    public event Action OnResumed;

    private PauseMenuView _view;
    private PausePage _page = PausePage.None;
    private Coroutine _fadeRoutine;
    private Coroutine _popRoutine;

    private void Awake()
    {
        _view = PauseMenuView.Build(transform);
        WireButtons();

        // 初期状態は完全非表示（ゲームプレイの入力を妨げない）。
        _view.MenuCard.SetActive(false);
        _view.ConfirmCard.SetActive(false);
        _view.Group.alpha = 0f;
        SetInteractable(false);

        IsPaused = false;
        _page = PausePage.None;
    }

    private void OnDestroy()
    {
        UnwireButtons();

        // ポーズ中にシーンが破棄されても timeScale を確実に戻す。
        if (IsPaused)
        {
            IsPaused = false;
            Time.timeScale = 1f;
        }
    }

    private void Update()
    {
        if (!IsPaused)
        {
            if (InputStateReader.EscapePressedThisFrame())
                Pause();
            return;
        }

        // 設定画面側の「閉じる」ボタンで外部クローズされたらメニューへ戻す。
        if (_page == PausePage.Settings && GameServices.Settings is { IsOpen: false })
        {
            ShowMenuPage();
            return;
        }

        if (InputStateReader.EscapePressedThisFrame())
            HandleCancel();
    }

    // ── 開閉 ─────────────────────────────────────────────────
    public void Toggle()
    {
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;

        // Co-op 中は停止しない（GDD §17.3）。
        if (!IsNetworkedSession())
            Time.timeScale = 0f;

        GameServices.Audio?.PlaySE2D(SoundId.UiClick);
        GameplayCursorPolicy.SetMenuMode();

        ShowMenuPage();
        AnimateIn();

        OnPaused?.Invoke();
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        _page = PausePage.None;

        if (GameServices.Settings is { IsOpen: true })
            GameServices.Settings.Close();

        GameServices.Audio?.PlaySE2D(SoundId.UiCancel);
        GameplayCursorPolicy.SetGameplayMode();
        Time.timeScale = 1f;

        AnimateOut();

        OnResumed?.Invoke();
    }

    /// <summary>Esc / Cancel の 1 段戻る処理。</summary>
    private void HandleCancel()
    {
        switch (PauseMenuNavigation.OnCancel(_page))
        {
            case PauseNavAction.Resume:
                Resume();
                break;
            case PauseNavAction.GoToMenu:
                GameServices.Audio?.PlaySE2D(SoundId.UiCancel);
                ShowMenuPage();
                break;
        }
    }

    // ── ページ表示 ───────────────────────────────────────────
    private void ShowMenuPage()
    {
        _page = PausePage.Menu;

        if (GameServices.Settings is { IsOpen: true })
            GameServices.Settings.Close();

        if (!_view.Root.activeSelf)
            _view.Root.SetActive(true);

        _view.MenuCard.SetActive(true);
        _view.ConfirmCard.SetActive(false);

        _view.Group.alpha = 1f;
        SetInteractable(true);
        PopCard(_view.CardTransform);

        UiFocus.Select(_view.ResumeButton, _view.MenuCard);
    }

    private void OpenSettings()
    {
        var settings = GameServices.Settings;
        if (settings == null)
        {
            // 設定サービスが無い環境ではメニューに留まる（フェイルソフト）。
            Debug.LogWarning("[PauseMenu] 設定サービスが見つからないため設定を開けません。");
            return;
        }

        _page = PausePage.Settings;
        GameServices.Audio?.PlaySE2D(SoundId.UiClick);

        // 自前オーバーレイを退避して、設定パネル（別 Canvas）を前面に見せる。
        _view.Root.SetActive(false);
        settings.Open();
    }

    private void RequestLeave()
    {
        _page = PausePage.ConfirmLeave;
        GameServices.Audio?.PlaySE2D(SoundId.UiClick);

        _view.MenuCard.SetActive(false);
        _view.ConfirmCard.SetActive(true);
        PopCard((RectTransform)_view.ConfirmCard.transform);

        // 誤確定を避けるため安全側の「戻る」を初期フォーカスにする。
        UiFocus.Select(_view.ConfirmNoButton, _view.ConfirmCard);
    }

    private void CancelLeave()
    {
        GameServices.Audio?.PlaySE2D(SoundId.UiCancel);
        ShowMenuPage();
    }

    private void ConfirmLeave()
    {
        IsPaused = false;
        _page = PausePage.None;
        Time.timeScale = 1f;

        GameServices.Audio?.PlaySE2D(SoundId.UiClick);

        // ホスト切断はセッション終了扱い（GDD §11.3 / §17.3）。
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        GameplayCursorPolicy.SetMenuMode();
        SceneManager.LoadScene(_mainMenuScene);
    }

    // ── アニメーション（unscaled 時間）────────────────────────
    private void AnimateIn()
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeGroup(0f, 1f, makeInteractableAtEnd: true));
        PopCard(_view.CardTransform);
    }

    private void AnimateOut()
    {
        SetInteractable(false);
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeOutAndHide());
    }

    private IEnumerator FadeGroup(float from, float to, bool makeInteractableAtEnd)
    {
        SetInteractable(false);
        float t = 0f;
        _view.Group.alpha = from;
        while (t < _fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            _view.Group.alpha = Mathf.Lerp(from, to, t / _fadeDuration);
            yield return null;
        }
        _view.Group.alpha = to;
        if (makeInteractableAtEnd) SetInteractable(true);
        _fadeRoutine = null;
    }

    private IEnumerator FadeOutAndHide()
    {
        float from = _view.Group.alpha;
        float t = 0f;
        while (t < _fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            _view.Group.alpha = Mathf.Lerp(from, 0f, t / _fadeDuration);
            yield return null;
        }
        _view.Group.alpha = 0f;
        _view.MenuCard.SetActive(false);
        _view.ConfirmCard.SetActive(false);
        if (!_view.Root.activeSelf)
            _view.Root.SetActive(true); // 次回 Pause に備えてオーバーレイは残す
        _fadeRoutine = null;
    }

    private void PopCard(RectTransform card)
    {
        if (card == null) return;
        if (_popRoutine != null) StopCoroutine(_popRoutine);
        _popRoutine = StartCoroutine(PopCardRoutine(card));
    }

    private IEnumerator PopCardRoutine(RectTransform card)
    {
        const float startScale = 0.94f;
        float t = 0f;
        while (t < _fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(startScale, 1f, t / _fadeDuration);
            card.localScale = new Vector3(k, k, 1f);
            yield return null;
        }
        card.localScale = Vector3.one;
        _popRoutine = null;
    }

    private void SetInteractable(bool value)
    {
        _view.Group.interactable = value;
        _view.Group.blocksRaycasts = value;
    }

    // ── ボタン配線 ───────────────────────────────────────────
    private void WireButtons()
    {
        _view.ResumeButton.onClick.AddListener(Resume);
        _view.SettingsButton.onClick.AddListener(OpenSettings);
        _view.LeaveButton.onClick.AddListener(RequestLeave);
        _view.ConfirmYesButton.onClick.AddListener(ConfirmLeave);
        _view.ConfirmNoButton.onClick.AddListener(CancelLeave);
    }

    private void UnwireButtons()
    {
        if (_view == null) return;
        _view.ResumeButton.onClick.RemoveListener(Resume);
        _view.SettingsButton.onClick.RemoveListener(OpenSettings);
        _view.LeaveButton.onClick.RemoveListener(RequestLeave);
        _view.ConfirmYesButton.onClick.RemoveListener(ConfirmLeave);
        _view.ConfirmNoButton.onClick.RemoveListener(CancelLeave);
    }

    private static bool IsNetworkedSession()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }
}
