using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Netcode;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

/// <summary>
/// GDD §17.3 — ポーズメニュー。
///
/// 仕様:
///   - Esc キー / Menu ボタンでメニューオーバーレイを開閉
///   - Co-op 中は Time.timeScale を変更しない（ゲームは継続）
///   - オフライン時（ネットワーク未起動）のみ Time.timeScale = 0 で停止
///   - 3 ボタン: 「ゲームに戻る」「設定」「離脱」
///   - 離脱は確認ダイアログ経由で MainMenu シーンへ遷移 + ネットワーク切断
///   - 背景は blurPanel（Gaussian Blur のマテリアルを事前アタッチ）
///
/// 使用法:
///   シーンルートに PauseMenu.prefab を配置し Inspector で参照を解決する。
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [Header("ルートパネル")]
    [SerializeField] private GameObject _menuRoot;
    [SerializeField] private GameObject _confirmLeaveRoot;
    [SerializeField] private GameObject _blurPanel;

    [Header("ボタン")]
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _leaveButton;
    [SerializeField] private Button _confirmLeaveYes;
    [SerializeField] private Button _confirmLeaveNo;

    [Header("設定画面ルート (任意)")]
    [SerializeField] private GameObject _settingsRoot;

    [Header("遷移先")]
    [SerializeField] private string _mainMenuScene = "MainMenu";

    [Header("入力")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.Escape;

    public event Action OnPaused;
    public event Action OnResumed;

    private void Awake()
    {
        HideAll();
        WireButtons();
    }

    private void OnDestroy()
    {
        UnwireButtons();
    }

    private void Update()
    {
        if (Input.GetKeyDown(_toggleKey))
            Toggle();
    }

    public void Toggle()
    {
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;

        if (_menuRoot != null)       _menuRoot.SetActive(true);
        if (_blurPanel != null)      _blurPanel.SetActive(true);
        if (_confirmLeaveRoot != null) _confirmLeaveRoot.SetActive(false);
        if (_settingsRoot != null)   _settingsRoot.SetActive(false);

        // Co-op 中は停止しない (GDD §17.3)
        if (!IsNetworkedSession())
            Time.timeScale = 0f;

        // GDD §15.2 — ui_click（メニューを開く音）
        PPAudioManager.Instance?.PlaySE2D(SoundId.UiClick);

        OnPaused?.Invoke();
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        HideAll();
        Time.timeScale = 1f;

        // GDD §15.2 — ui_cancel（メニューを閉じる音）
        PPAudioManager.Instance?.PlaySE2D(SoundId.UiCancel);

        OnResumed?.Invoke();
    }

    private void OpenSettings()
    {
        if (_settingsRoot != null) _settingsRoot.SetActive(true);
        if (_menuRoot != null)     _menuRoot.SetActive(false);

        PPAudioManager.Instance?.PlaySE2D(SoundId.UiClick);
    }

    private void RequestLeave()
    {
        if (_confirmLeaveRoot != null) _confirmLeaveRoot.SetActive(true);
        if (_menuRoot != null)         _menuRoot.SetActive(false);

        PPAudioManager.Instance?.PlaySE2D(SoundId.UiClick);
    }

    private void CancelLeave()
    {
        if (_confirmLeaveRoot != null) _confirmLeaveRoot.SetActive(false);
        if (_menuRoot != null)         _menuRoot.SetActive(true);

        PPAudioManager.Instance?.PlaySE2D(SoundId.UiCancel);
    }

    private void ConfirmLeave()
    {
        IsPaused = false;
        Time.timeScale = 1f;

        PPAudioManager.Instance?.PlaySE2D(SoundId.UiClick);

        // ネットワークセッションを終了（ホスト切断はセッション終了扱い — GDD §11.3/§17.3）
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene(_mainMenuScene);
    }

    private void HideAll()
    {
        if (_menuRoot != null)         _menuRoot.SetActive(false);
        if (_confirmLeaveRoot != null) _confirmLeaveRoot.SetActive(false);
        if (_blurPanel != null)        _blurPanel.SetActive(false);
        if (_settingsRoot != null)     _settingsRoot.SetActive(false);
    }

    private void WireButtons()
    {
        if (_resumeButton != null)     _resumeButton.onClick.AddListener(Resume);
        if (_settingsButton != null)   _settingsButton.onClick.AddListener(OpenSettings);
        if (_leaveButton != null)      _leaveButton.onClick.AddListener(RequestLeave);
        if (_confirmLeaveYes != null)  _confirmLeaveYes.onClick.AddListener(ConfirmLeave);
        if (_confirmLeaveNo != null)   _confirmLeaveNo.onClick.AddListener(CancelLeave);
    }

    private void UnwireButtons()
    {
        if (_resumeButton != null)     _resumeButton.onClick.RemoveListener(Resume);
        if (_settingsButton != null)   _settingsButton.onClick.RemoveListener(OpenSettings);
        if (_leaveButton != null)      _leaveButton.onClick.RemoveListener(RequestLeave);
        if (_confirmLeaveYes != null)  _confirmLeaveYes.onClick.RemoveListener(ConfirmLeave);
        if (_confirmLeaveNo != null)   _confirmLeaveNo.onClick.RemoveListener(CancelLeave);
    }

    private static bool IsNetworkedSession()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }
}
