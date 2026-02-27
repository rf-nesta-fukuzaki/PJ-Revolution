using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// オプション画面の UI 制御コンポーネント。
///
/// [設定項目]
///   - マスター音量    : Slider → AudioListener.volume に反映
///   - マウス感度      : Slider → シーン上の FirstPersonLook.sensitivityX/Y に反映
///   - 画面モード      : Dropdown（フルスクリーン / ウィンドウ）
///   - 解像度          : Dropdown（Screen.resolutions から生成）
///
/// [永続化]
///   PlayerPrefs キー: "MasterVolume" / "MouseSensitivity" / "ScreenMode" / "ResolutionIndex"
///
/// [ボタン]
///   - 適用 : 設定を Screen / AudioListener に反映し PlayerPrefs に保存
///   - 戻る : 変更を破棄してパネルを閉じる
/// </summary>
public class OptionsUI : MonoBehaviour
{
    // ─────────────── PlayerPrefs キー ───────────────

    private const string KeyVolume     = "MasterVolume";
    private const string KeySensitivity = "MouseSensitivity";
    private const string KeyScreenMode  = "ScreenMode";
    private const string KeyResolution  = "ResolutionIndex";

    // ─────────────── Inspector ───────────────

    [Header("音量設定")]
    [Tooltip("マスター音量スライダー (0～1)")]
    [SerializeField] private Slider volumeSlider;

    [Header("感度設定")]
    [Tooltip("マウス感度スライダー (0.1～5.0)")]
    [SerializeField] private Slider sensitivitySlider;

    [Header("画面設定")]
    [Tooltip("画面モード選択ドロップダウン（フルスクリーン / ウィンドウ）")]
    [SerializeField] private TMP_Dropdown screenModeDropdown;

    [Tooltip("解像度選択ドロップダウン")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    [Header("ボタン")]
    [Tooltip("「適用」ボタン")]
    [SerializeField] private Button applyButton;

    [Tooltip("「戻る」ボタン")]
    [SerializeField] private Button backButton;

    // ─────────────── 内部状態 ───────────────

    private Resolution[] _resolutions;

    // ─────────────── Unity Lifecycle ───────────────

    private void Awake()
    {
        if (applyButton != null) applyButton.onClick.AddListener(OnApplyClicked);
        if (backButton  != null) backButton.onClick.AddListener(OnBackClicked);

        BuildResolutionDropdown();
        LoadSettings();
    }

    private void OnEnable()
    {
        // 画面を開くたびに現在の PlayerPrefs 値を UI に反映する
        LoadSettings();
    }

    // ─────────────── 初期化 ───────────────

    private void BuildResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        _resolutions = Screen.resolutions;
        var options = new List<string>();
        int currentIndex = 0;

        for (int i = 0; i < _resolutions.Length; i++)
        {
            options.Add($"{_resolutions[i].width} x {_resolutions[i].height} @ {_resolutions[i].refreshRateRatio.numerator}Hz");

            // 現在の解像度と一致するエントリをデフォルト選択にする
            if (_resolutions[i].width  == Screen.currentResolution.width &&
                _resolutions[i].height == Screen.currentResolution.height)
            {
                currentIndex = i;
            }
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentIndex;
        resolutionDropdown.RefreshShownValue();
    }

    private void BuildScreenModeDropdown()
    {
        if (screenModeDropdown == null) return;

        screenModeDropdown.ClearOptions();
        screenModeDropdown.AddOptions(new List<string> { "フルスクリーン", "ウィンドウ" });
    }

    // ─────────────── 設定の読み込み ───────────────

    private void LoadSettings()
    {
        // 画面モードドロップダウンはここで構築（ClearOptions が必要なため毎回再構築）
        BuildScreenModeDropdown();

        if (volumeSlider != null)
        {
            float vol = PlayerPrefs.GetFloat(KeyVolume, AudioListener.volume);
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.value    = vol;
        }

        if (sensitivitySlider != null)
        {
            float sens = PlayerPrefs.GetFloat(KeySensitivity, 2f);
            sensitivitySlider.minValue = 0.1f;
            sensitivitySlider.maxValue = 5f;
            sensitivitySlider.value    = sens;
        }

        if (screenModeDropdown != null)
        {
            // 0: フルスクリーン, 1: ウィンドウ
            int mode = PlayerPrefs.GetInt(KeyScreenMode, Screen.fullScreen ? 0 : 1);
            screenModeDropdown.value = Mathf.Clamp(mode, 0, 1);
            screenModeDropdown.RefreshShownValue();
        }

        if (resolutionDropdown != null && _resolutions != null)
        {
            int idx = PlayerPrefs.GetInt(KeyResolution, resolutionDropdown.value);
            idx = Mathf.Clamp(idx, 0, _resolutions.Length - 1);
            resolutionDropdown.value = idx;
            resolutionDropdown.RefreshShownValue();
        }
    }

    // ─────────────── 設定の適用 ───────────────

    private void ApplySettings()
    {
        // ── 音量 ──
        if (volumeSlider != null)
        {
            float vol = volumeSlider.value;
            AudioListener.volume = vol;
            PlayerPrefs.SetFloat(KeyVolume, vol);
        }

        // ── マウス感度 ──
        if (sensitivitySlider != null)
        {
            float sens = sensitivitySlider.value;
            PlayerPrefs.SetFloat(KeySensitivity, sens);

            // シーン上の FirstPersonLook にリアルタイム反映する
            var look = FindFirstObjectByType<FirstPersonLook>();
            if (look != null)
            {
                look.SetSensitivity(sens);
            }
        }

        // ── 解像度 ──
        int resIndex = 0;
        if (resolutionDropdown != null && _resolutions != null)
        {
            resIndex = resolutionDropdown.value;
            PlayerPrefs.SetInt(KeyResolution, resIndex);
        }

        // ── 画面モード ──
        if (screenModeDropdown != null)
        {
            int modeValue = screenModeDropdown.value;
            PlayerPrefs.SetInt(KeyScreenMode, modeValue);

            bool isFullScreen = (modeValue == 0);
            if (_resolutions != null && resIndex < _resolutions.Length)
            {
                Resolution res = _resolutions[resIndex];
                Screen.SetResolution(res.width, res.height, isFullScreen);
            }
            else
            {
                Screen.fullScreen = isFullScreen;
            }
        }

        PlayerPrefs.Save();
        Debug.Log("[OptionsUI] 設定を保存しました。");
    }

    // ─────────────── ボタンハンドラ ───────────────

    private void OnApplyClicked()
    {
        ApplySettings();
    }

    private void OnBackClicked()
    {
        UIFlowController.Instance?.SetOptionsVisible(false);
    }
}
