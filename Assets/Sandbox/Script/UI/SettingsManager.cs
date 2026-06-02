using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GDD §14.7 — 設定画面。Esc キーから遷移。4 タブ構成。
///
/// タブ：グラフィック / オーディオ / 操作 / アクセシビリティ
///
/// 設定は settings.json（GDD §18.6）へ保存し、互換のため PlayerPrefs にも同期する。
/// 閉じるボタン押下またはシーン開始時に ApplyAll() で即時反映。
///
/// アクセス方法：
///   - GameServices.Settings.Open()   — パネルを開く
///   - GameServices.Settings.Close()  — パネルを閉じる（自動保存）
///   - GameServices.Settings.Settings — 現在の SettingsData を取得
/// </summary>
public class SettingsManager : MonoBehaviour, ISettingsService
{
    private static SettingsManager _instance;

    [System.Obsolete("GameServices.Settings を使用してください")]
    public static SettingsManager Instance => _instance;

    private readonly SettingsViewModel _viewModel = new();

    // ── Inspector ─────────────────────────────────────────────
    [Header("パネル")]
    [SerializeField] private GameObject _settingsPanel;

    [Header("タブボタン")]
    [SerializeField] private Button _tabGraphics;
    [SerializeField] private Button _tabAudio;
    [SerializeField] private Button _tabControls;
    [SerializeField] private Button _tabAccessibility;

    [Header("タブコンテンツ")]
    [SerializeField] private GameObject _panelGraphics;
    [SerializeField] private GameObject _panelAudio;
    [SerializeField] private GameObject _panelControls;
    [SerializeField] private GameObject _panelAccessibility;

    [Header("グラフィック")]
    [SerializeField] private TMP_Dropdown _ddResolution;
    [SerializeField] private TMP_Dropdown _ddWindowMode;
    [SerializeField] private TMP_Dropdown _ddQuality;
    [SerializeField] private TMP_Dropdown _ddFpsCap;
    [SerializeField] private Toggle       _togVSync;
    [SerializeField] private TMP_Dropdown _ddShadow;
    [SerializeField] private TMP_Dropdown _ddParticle;

    [Header("オーディオ")]
    [SerializeField] private Slider _slMaster;
    [SerializeField] private Slider _slBgm;
    [SerializeField] private Slider _slSe;
    [SerializeField] private Slider _slVoice;
    [SerializeField] private Slider _slMicGain;
    [SerializeField] private Button _btnMicTest;
    [SerializeField] private AudioMixer _audioMixer;   // 任意

    [Header("操作")]
    [SerializeField] private Slider       _slMouseSens;
    [SerializeField] private Toggle       _togInvertY;
    [SerializeField] private TMP_Dropdown _ddGamepadPreset;

    [Header("アクセシビリティ")]
    [SerializeField] private Toggle       _togSubtitles;
    [SerializeField] private Slider       _slUiScale;
    [SerializeField] private TMP_Dropdown _ddColorBlind;
    [SerializeField] private Toggle       _togCameraShake;
    [SerializeField] private Image        _crosshairColorSwatch;   // クロスヘア色のプレビュー

    [Header("閉じるボタン")]
    [SerializeField] private Button _btnClose;

    // ── 公開状態 ─────────────────────────────────────────────
    public SettingsData Settings { get; private set; }

    SettingsData ISettingsService.Settings => Settings;
    float ISettingsService.MouseSensitivity => MouseSensitivity;
    bool ISettingsService.InvertY => InvertY;
    bool ISettingsService.IsOpen => IsOpen;
    void ISettingsService.Open() => Open();
    void ISettingsService.Close() => Close();
    void ISettingsService.ApplyAll(SettingsData data) => ApplyAll(data);

    // ── マウス感度プロパティ（ExplorerCameraLook から参照）────
    public float MouseSensitivity => Settings.mouseSensitivity;
    public bool  InvertY          => Settings.invertY;

    /// <summary>設定パネルが現在表示中か。PauseMenu が外部クローズを検知するために参照する。</summary>
    public bool IsOpen => _settingsPanel != null && _settingsPanel.activeSelf;

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        GameServices.Register((ISettingsService)this);
    }

    private void Start()
    {
        Settings = _viewModel.Load(Application.persistentDataPath);
        ApplyAll(Settings);
        _viewModel.ApplyGameplaySnapshotToProfile();
        BindUI();
        PopulateUI(Settings);
        ShowTab(0);
        _settingsPanel?.SetActive(false);
    }

    // ── 開閉 ─────────────────────────────────────────────────
    public void Open()
    {
        _settingsPanel?.SetActive(true);
        PopulateUI(Settings);
        ShowTab(0);
    }

    public void Close()
    {
        Save(Settings);
        _settingsPanel?.SetActive(false);
    }
    private void ShowTab(int index)
    {
        _panelGraphics?.SetActive(index     == 0);
        _panelAudio?.SetActive(index        == 1);
        _panelControls?.SetActive(index     == 2);
        _panelAccessibility?.SetActive(index == 3);
    }

    // ── UI バインド ───────────────────────────────────────────
    private void BindUI()
    {
        _tabGraphics?.onClick.AddListener(() => ShowTab(0));
        _tabAudio?.onClick.AddListener(() => ShowTab(1));
        _tabControls?.onClick.AddListener(() => ShowTab(2));
        _tabAccessibility?.onClick.AddListener(() => ShowTab(3));
        _btnClose?.onClick.AddListener(Close);

        // グラフィック
        _ddResolution?.onValueChanged.AddListener(v   => { var s = Settings; s.resolutionIndex  = v; ApplyAndSave(s); });
        _ddWindowMode?.onValueChanged.AddListener(v   => { var s = Settings; s.windowMode       = v; ApplyAndSave(s); });
        _ddQuality?.onValueChanged.AddListener(v      => { var s = Settings; s.qualityLevel     = v; ApplyAndSave(s); });
        _ddFpsCap?.onValueChanged.AddListener(v       => { var s = Settings; s.fpsCap           = v; ApplyAndSave(s); });
        _togVSync?.onValueChanged.AddListener(v       => { var s = Settings; s.vSync            = v; ApplyAndSave(s); });
        _ddShadow?.onValueChanged.AddListener(v       => { var s = Settings; s.shadowQuality    = v; ApplyAndSave(s); });
        _ddParticle?.onValueChanged.AddListener(v     => { var s = Settings; s.particleQuality  = v; ApplyAndSave(s); });

        // オーディオ
        _slMaster?.onValueChanged.AddListener(v  => { var s = Settings; s.masterVolume = Mathf.RoundToInt(v); ApplyAndSave(s); });
        _slBgm?.onValueChanged.AddListener(v     => { var s = Settings; s.bgmVolume    = Mathf.RoundToInt(v); ApplyAndSave(s); });
        _slSe?.onValueChanged.AddListener(v      => { var s = Settings; s.seVolume     = Mathf.RoundToInt(v); ApplyAndSave(s); });
        _slVoice?.onValueChanged.AddListener(v   => { var s = Settings; s.voiceVolume  = Mathf.RoundToInt(v); ApplyAndSave(s); });
        _slMicGain?.onValueChanged.AddListener(v => { var s = Settings; s.micGain      = Mathf.RoundToInt(v); ApplyAndSave(s); });
        _btnMicTest?.onClick.AddListener(StartMicTest);

        // 操作
        _slMouseSens?.onValueChanged.AddListener(v    => { var s = Settings; s.mouseSensitivity = v; ApplyAndSave(s); });
        _togInvertY?.onValueChanged.AddListener(v     => { var s = Settings; s.invertY         = v; ApplyAndSave(s); });
        _ddGamepadPreset?.onValueChanged.AddListener(v => { var s = Settings; s.gamepadPreset  = v; ApplyAndSave(s); });

        // アクセシビリティ
        _togSubtitles?.onValueChanged.AddListener(v   => { var s = Settings; s.subtitles        = v; ApplyAndSave(s); });
        _slUiScale?.onValueChanged.AddListener(v      => { var s = Settings; s.uiScale          = Mathf.RoundToInt(v); ApplyAndSave(s); });
        _ddColorBlind?.onValueChanged.AddListener(v   => { var s = Settings; s.colorBlindMode   = v; ApplyAndSave(s); });
        _togCameraShake?.onValueChanged.AddListener(v => { var s = Settings; s.reduceCameraShake = v; ApplyAndSave(s); });

        PopulateResolutionDropdown();
    }

    private void PopulateResolutionDropdown()
    {
        if (_ddResolution == null) return;
        _ddResolution.ClearOptions();
        var resolutions = Screen.resolutions;
        foreach (var r in resolutions)
            _ddResolution.options.Add(new TMP_Dropdown.OptionData($"{r.width}×{r.height} {r.refreshRateRatio.value:F0}Hz"));
        _ddResolution.RefreshShownValue();
    }

    // ── UI → 状態 反映 ───────────────────────────────────────
    private void ApplyAndSave(SettingsData next)
    {
        Settings = next;
        ApplyAll(next);
        // 設定パネルが開いている間はリアルタイム反映。パネルを閉じたときに正式保存。
    }

    // ── UI への値書き込み ────────────────────────────────────
    private void PopulateUI(SettingsData s)
    {
        // グラフィック
        if (_ddResolution  != null) _ddResolution.SetValueWithoutNotify(s.resolutionIndex);
        if (_ddWindowMode  != null) _ddWindowMode.SetValueWithoutNotify(s.windowMode);
        if (_ddQuality     != null) _ddQuality.SetValueWithoutNotify(s.qualityLevel);
        if (_ddFpsCap      != null) _ddFpsCap.SetValueWithoutNotify(s.fpsCap);
        if (_togVSync      != null) _togVSync.SetIsOnWithoutNotify(s.vSync);
        if (_ddShadow      != null) _ddShadow.SetValueWithoutNotify(s.shadowQuality);
        if (_ddParticle    != null) _ddParticle.SetValueWithoutNotify(s.particleQuality);

        // オーディオ
        if (_slMaster  != null) _slMaster.SetValueWithoutNotify(s.masterVolume);
        if (_slBgm     != null) _slBgm.SetValueWithoutNotify(s.bgmVolume);
        if (_slSe      != null) _slSe.SetValueWithoutNotify(s.seVolume);
        if (_slVoice   != null) _slVoice.SetValueWithoutNotify(s.voiceVolume);
        if (_slMicGain != null) _slMicGain.SetValueWithoutNotify(s.micGain);

        // 操作
        if (_slMouseSens     != null) _slMouseSens.SetValueWithoutNotify(s.mouseSensitivity);
        if (_togInvertY      != null) _togInvertY.SetIsOnWithoutNotify(s.invertY);
        if (_ddGamepadPreset != null) _ddGamepadPreset.SetValueWithoutNotify(s.gamepadPreset);

        // アクセシビリティ
        if (_togSubtitles  != null) _togSubtitles.SetIsOnWithoutNotify(s.subtitles);
        if (_slUiScale     != null) _slUiScale.SetValueWithoutNotify(s.uiScale);
        if (_ddColorBlind  != null) _ddColorBlind.SetValueWithoutNotify(s.colorBlindMode);
        if (_togCameraShake != null) _togCameraShake.SetIsOnWithoutNotify(s.reduceCameraShake);
        UpdateCrosshairSwatch(s.crosshairColorHex);
    }

    private void UpdateCrosshairSwatch(string hex)
    {
        if (_crosshairColorSwatch == null) return;
        if (ColorUtility.TryParseHtmlString(hex, out Color c))
            _crosshairColorSwatch.color = c;
    }

    // ── 設定の適用 ───────────────────────────────────────────
    public void ApplyAll(SettingsData s)
    {
        _viewModel.ApplyCoreSettings(s, GameServices.ColorBlind);
        ApplyAudio(s);
    }

    private void ApplyAudio(SettingsData s)
    {
        if (_audioMixer != null)
        {
            _audioMixer.SetFloat("MasterVolume", SettingsApplier.VolumeToDb(s.masterVolume));
            _audioMixer.SetFloat("BgmVolume",    SettingsApplier.VolumeToDb(s.bgmVolume));
            _audioMixer.SetFloat("SeVolume",     SettingsApplier.VolumeToDb(s.seVolume));
            _audioMixer.SetFloat("VoiceVolume",  SettingsApplier.VolumeToDb(s.voiceVolume));
        }
        else
        {
            AudioListener.volume = s.masterVolume / 100f;
        }
    }

    // ── マイクテスト（GDD §14.7）────────────────────────────
    private bool _micTesting;

    private void StartMicTest()
    {
        if (_micTesting) return;
        _micTesting = true;
        StartCoroutine(MicTestCoroutine());
    }

    private System.Collections.IEnumerator MicTestCoroutine()
    {
        const float duration = 3f;

        if (Microphone.devices.Length == 0)
        {
            Debug.Log("[Settings] マイクが見つかりません");
            _micTesting = false;
            yield break;
        }

        string device = Microphone.devices[0];
        var clip = Microphone.Start(device, false, Mathf.CeilToInt(duration), 44100);

        yield return new WaitForSeconds(duration);
        Microphone.End(device);

        // 録音データを再生
        var src = gameObject.AddComponent<AudioSource>();
        src.clip = clip;
        src.Play();

        yield return new WaitForSeconds(duration);
        Destroy(src);
        _micTesting = false;
    }

    // ── 保存 / 読み込み ──────────────────────────────────────
    public void Save(SettingsData s) => _viewModel.Save(Application.persistentDataPath, s);
}

// ── 設定データ（struct）──────────────────
/// <summary>GDD §14.7 の全設定値を保持する構造体。値型コピーで疑似イミュータブルに扱う。</summary>
public struct SettingsData
{
    // グラフィック
    public int   resolutionIndex;
    public int   windowMode;        // 0=フルスクリーン 1=ウィンドウ 2=ボーダーレス
    public int   qualityLevel;      // 0=低 1=中 2=高 3=最高
    public int   fpsCap;            // FPS_OPTIONS のインデックス
    public bool  vSync;
    public int   shadowQuality;     // 0=OFF 1=低 2=中 3=高
    public int   particleQuality;   // 0=低 1=中 2=高

    // オーディオ（0-100）
    public int   masterVolume;
    public int   bgmVolume;
    public int   seVolume;
    public int   voiceVolume;
    public int   micGain;           // 0-200

    // 操作
    public float mouseSensitivity;  // 0.5-10.0
    public bool  invertY;
    public int   gamepadPreset;     // 0=デフォルト 1=代替A 2=代替B

    // アクセシビリティ
    public bool   subtitles;
    public int    uiScale;          // 80-150 (%)
    public int    colorBlindMode;   // 0=OFF 1=Protan 2=Deutan 3=Tritan
    public bool   reduceCameraShake;
    public string crosshairColorHex;

    public SettingsData(
        int   resolutionIndex,
        int   windowMode,
        int   qualityLevel,
        int   fpsCap,
        bool  vSync,
        int   shadowQuality,
        int   particleQuality,
        int   masterVolume,
        int   bgmVolume,
        int   seVolume,
        int   voiceVolume,
        int   micGain,
        float mouseSensitivity,
        bool  invertY,
        int   gamepadPreset,
        bool  subtitles,
        int   uiScale,
        int   colorBlindMode,
        bool  reduceCameraShake,
        string crosshairColorHex)
    {
        this.resolutionIndex   = resolutionIndex;
        this.windowMode        = windowMode;
        this.qualityLevel      = qualityLevel;
        this.fpsCap            = fpsCap;
        this.vSync             = vSync;
        this.shadowQuality     = shadowQuality;
        this.particleQuality   = particleQuality;
        this.masterVolume      = masterVolume;
        this.bgmVolume         = bgmVolume;
        this.seVolume          = seVolume;
        this.voiceVolume       = voiceVolume;
        this.micGain           = micGain;
        this.mouseSensitivity  = mouseSensitivity;
        this.invertY           = invertY;
        this.gamepadPreset     = gamepadPreset;
        this.subtitles         = subtitles;
        this.uiScale           = uiScale;
        this.colorBlindMode    = colorBlindMode;
        this.reduceCameraShake = reduceCameraShake;
        this.crosshairColorHex = crosshairColorHex;
    }
}
