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
///   - SettingsManager.Instance.Open()   — パネルを開く
///   - SettingsManager.Instance.Close()  — パネルを閉じる（自動保存）
///   - SettingsManager.Settings          — 現在の SettingsData を取得
/// </summary>
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

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

    // ── マウス感度プロパティ（ExplorerCameraLook から参照）────
    public float MouseSensitivity => Settings.mouseSensitivity;
    public bool  InvertY          => Settings.invertY;

    // ── PlayerPrefs キー ─────────────────────────────────────
    private const string KEY_RESOLUTION     = "cfg_resolution";
    private const string KEY_WINDOW_MODE    = "cfg_window";
    private const string KEY_QUALITY        = "cfg_quality";
    private const string KEY_FPS_CAP        = "cfg_fps";
    private const string KEY_VSYNC          = "cfg_vsync";
    private const string KEY_SHADOW         = "cfg_shadow";
    private const string KEY_PARTICLE       = "cfg_particle";
    private const string KEY_MASTER_VOL     = "cfg_vol_master";
    private const string KEY_BGM_VOL        = "cfg_vol_bgm";
    private const string KEY_SE_VOL         = "cfg_vol_se";
    private const string KEY_VOICE_VOL      = "cfg_vol_voice";
    private const string KEY_MIC_GAIN       = "cfg_mic_gain";
    private const string KEY_MOUSE_SENS     = "cfg_mouse_sens";
    private const string KEY_INVERT_Y       = "cfg_invert_y";
    private const string KEY_GAMEPAD_PRESET = "cfg_gamepad";
    private const string KEY_SUBTITLES      = "cfg_subtitles";
    private const string KEY_UI_SCALE       = "cfg_ui_scale";
    private const string KEY_COLOR_BLIND    = "cfg_color_blind";
    private const string KEY_CAMERA_SHAKE   = "cfg_camera_shake";
    private const string KEY_CROSSHAIR_HEX  = "cfg_crosshair";

    // ── FPS 上限テーブル ──────────────────────────────────────
    private static readonly int[] FPS_OPTIONS = { 30, 60, 120, 0 };   // 0 = 無制限
    private SettingsGameplaySnapshot _loadedGameplaySnapshot = SettingsGameplaySnapshot.Default;
    private bool _hasLoadedGameplaySnapshot;

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        Settings = LoadSettings();
        ApplyAll(Settings);
        ApplyLoadedGameplaySnapshot();
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

    // ── タブ切替 ─────────────────────────────────────────────
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
        ApplyGraphics(s);
        ApplyAudio(s);
        // 操作はリアルタイム参照（MouseSensitivity プロパティ経由）
        ApplyAccessibility(s);
    }

    private void ApplyGraphics(SettingsData s)
    {
        // 解像度
        var res = Screen.resolutions;
        int idx = Mathf.Clamp(s.resolutionIndex, 0, res.Length - 1);
        if (res.Length > 0)
        {
            FullScreenMode mode = s.windowMode switch
            {
                1 => FullScreenMode.Windowed,
                2 => FullScreenMode.FullScreenWindow,   // ボーダーレス
                _ => FullScreenMode.ExclusiveFullScreen,
            };
            Screen.SetResolution(res[idx].width, res[idx].height, mode);
        }

        // 画質
        QualitySettings.SetQualityLevel(s.qualityLevel, true);

        // FPS 上限
        int fps = (s.fpsCap >= 0 && s.fpsCap < FPS_OPTIONS.Length) ? FPS_OPTIONS[s.fpsCap] : 60;
        Application.targetFrameRate = fps == 0 ? -1 : fps;

        // V-Sync
        QualitySettings.vSyncCount = s.vSync ? 1 : 0;

        // 影品質
        ApplyShadowQuality(s.shadowQuality);
    }

    private static void ApplyShadowQuality(int level)
    {
        switch (level)
        {
            case 0: QualitySettings.shadows = ShadowQuality.Disable; break;
            case 1: QualitySettings.shadows = ShadowQuality.HardOnly; QualitySettings.shadowDistance = 30f;  break;
            case 2: QualitySettings.shadows = ShadowQuality.All;      QualitySettings.shadowDistance = 80f;  break;
            case 3: QualitySettings.shadows = ShadowQuality.All;      QualitySettings.shadowDistance = 150f; break;
        }
    }

    private void ApplyAudio(SettingsData s)
    {
        if (_audioMixer != null)
        {
            // AudioMixer の exposed パラメーター名に合わせて調整
            _audioMixer.SetFloat("MasterVolume", VolumeToDb(s.masterVolume));
            _audioMixer.SetFloat("BgmVolume",    VolumeToDb(s.bgmVolume));
            _audioMixer.SetFloat("SeVolume",     VolumeToDb(s.seVolume));
            _audioMixer.SetFloat("VoiceVolume",  VolumeToDb(s.voiceVolume));
        }
        else
        {
            // AudioMixer 未設定の場合は AudioListener で代替
            AudioListener.volume = s.masterVolume / 100f;
        }
    }

    private static float VolumeToDb(int vol0to100)
    {
        float t = Mathf.Clamp(vol0to100, 0, 100) / 100f;
        return t < 0.001f ? -80f : 20f * Mathf.Log10(t);
    }

    private void ApplyAccessibility(SettingsData s)
    {
        // UI スケール：Canvas の scaleFactor を変更（対象 Canvas は検索）
        float scale = s.uiScale / 100f;
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas.renderMode != RenderMode.WorldSpace)
                canvas.scaleFactor = scale;
        }

        // GDD §14.7: 色覚サポート。パレットサービスへモードを伝搬し、購読済み UI を自動再着色。
        if (ColorBlindPaletteService.Instance != null)
            ColorBlindPaletteService.Instance.SetModeInt(s.colorBlindMode);
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
    public void Save(SettingsData s)
    {
        SaveToPlayerPrefs(s);

        string playerDisplayName = SaveManager.Instance != null
            ? SaveManager.Instance.PlayerDisplayName
            : string.Empty;
        bool tutorialHintsEnabled = SaveManager.Instance == null
            || SaveManager.Instance.IsTutorialHintsEnabled();
        SettingsJsonStore.Save(Application.persistentDataPath, s, playerDisplayName, tutorialHintsEnabled);
    }

    private SettingsData LoadSettings()
    {
        if (SettingsJsonStore.TryLoad(Application.persistentDataPath, out var loaded, out var gameplay))
        {
            _loadedGameplaySnapshot = gameplay;
            _hasLoadedGameplaySnapshot = true;
            SaveToPlayerPrefs(loaded);
            return loaded;
        }

        _loadedGameplaySnapshot = SettingsGameplaySnapshot.Default;
        _hasLoadedGameplaySnapshot = false;
        return LoadFromPlayerPrefs();
    }

    private static void SaveToPlayerPrefs(SettingsData s)
    {
        PlayerPrefs.SetInt(KEY_RESOLUTION,     s.resolutionIndex);
        PlayerPrefs.SetInt(KEY_WINDOW_MODE,    s.windowMode);
        PlayerPrefs.SetInt(KEY_QUALITY,        s.qualityLevel);
        PlayerPrefs.SetInt(KEY_FPS_CAP,        s.fpsCap);
        PlayerPrefs.SetInt(KEY_VSYNC,          s.vSync ? 1 : 0);
        PlayerPrefs.SetInt(KEY_SHADOW,         s.shadowQuality);
        PlayerPrefs.SetInt(KEY_PARTICLE,       s.particleQuality);
        PlayerPrefs.SetInt(KEY_MASTER_VOL,     s.masterVolume);
        PlayerPrefs.SetInt(KEY_BGM_VOL,        s.bgmVolume);
        PlayerPrefs.SetInt(KEY_SE_VOL,         s.seVolume);
        PlayerPrefs.SetInt(KEY_VOICE_VOL,      s.voiceVolume);
        PlayerPrefs.SetInt(KEY_MIC_GAIN,       s.micGain);
        PlayerPrefs.SetFloat(KEY_MOUSE_SENS,   s.mouseSensitivity);
        PlayerPrefs.SetInt(KEY_INVERT_Y,       s.invertY ? 1 : 0);
        PlayerPrefs.SetInt(KEY_GAMEPAD_PRESET, s.gamepadPreset);
        PlayerPrefs.SetInt(KEY_SUBTITLES,      s.subtitles ? 1 : 0);
        PlayerPrefs.SetInt(KEY_UI_SCALE,       s.uiScale);
        PlayerPrefs.SetInt(KEY_COLOR_BLIND,    s.colorBlindMode);
        PlayerPrefs.SetInt(KEY_CAMERA_SHAKE,   s.reduceCameraShake ? 1 : 0);
        PlayerPrefs.SetString(KEY_CROSSHAIR_HEX, s.crosshairColorHex);
        PlayerPrefs.Save();
    }

    private static SettingsData LoadFromPlayerPrefs()
    {
        return new SettingsData(
            resolutionIndex:   PlayerPrefs.GetInt(KEY_RESOLUTION,     Screen.resolutions.Length - 1),
            windowMode:        PlayerPrefs.GetInt(KEY_WINDOW_MODE,    0),
            qualityLevel:      PlayerPrefs.GetInt(KEY_QUALITY,        2),
            fpsCap:            PlayerPrefs.GetInt(KEY_FPS_CAP,        1),  // 60 fps
            vSync:             PlayerPrefs.GetInt(KEY_VSYNC,          1) == 1,
            shadowQuality:     PlayerPrefs.GetInt(KEY_SHADOW,         2),
            particleQuality:   PlayerPrefs.GetInt(KEY_PARTICLE,       1),
            masterVolume:      PlayerPrefs.GetInt(KEY_MASTER_VOL,     80),
            bgmVolume:         PlayerPrefs.GetInt(KEY_BGM_VOL,        70),
            seVolume:          PlayerPrefs.GetInt(KEY_SE_VOL,         80),
            voiceVolume:       PlayerPrefs.GetInt(KEY_VOICE_VOL,      100),
            micGain:           PlayerPrefs.GetInt(KEY_MIC_GAIN,       100),
            mouseSensitivity:  PlayerPrefs.GetFloat(KEY_MOUSE_SENS,   3.0f),
            invertY:           PlayerPrefs.GetInt(KEY_INVERT_Y,       0) == 1,
            gamepadPreset:     PlayerPrefs.GetInt(KEY_GAMEPAD_PRESET, 0),
            subtitles:         PlayerPrefs.GetInt(KEY_SUBTITLES,      0) == 1,
            uiScale:           PlayerPrefs.GetInt(KEY_UI_SCALE,       100),
            colorBlindMode:    PlayerPrefs.GetInt(KEY_COLOR_BLIND,    0),
            reduceCameraShake: PlayerPrefs.GetInt(KEY_CAMERA_SHAKE,   0) == 1,
            crosshairColorHex: PlayerPrefs.GetString(KEY_CROSSHAIR_HEX, "#FFFFFF")
        );
    }

    private void ApplyLoadedGameplaySnapshot()
    {
        if (!_hasLoadedGameplaySnapshot) return;
        if (SaveManager.Instance == null) return;

        string loadedName = _loadedGameplaySnapshot.PlayerDisplayName;
        if (!string.IsNullOrWhiteSpace(loadedName))
            SaveManager.Instance.PlayerDisplayName = loadedName;

        SaveManager.Instance.SetTutorialHintsEnabled(_loadedGameplaySnapshot.TutorialHintsEnabled);
    }
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
