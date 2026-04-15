using System;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cozy Cave Crew 風タイトル画面の UI 制御。
/// </summary>
public sealed class CozyCaveTitleController : MonoBehaviour
{
    private const string DefaultConfigResourcePath = "Title/DefaultTitleSceneConfig";

    [Header("Configuration (Required ScriptableObject)")]
    [SerializeField] private TitleSceneConfig titleConfig;
    [SerializeField] private string resourcesConfigPath = DefaultConfigResourcePath;

    [Header("Scene")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject creditsPanel;

    [Header("Root References")]
    [SerializeField] private RectTransform backgroundRect;
    [SerializeField] private CanvasGroup backgroundCanvasGroup;
    [SerializeField] private RectTransform titleRect;
    [SerializeField] private CanvasGroup titleCanvasGroup;
    [SerializeField] private RectTransform menuRootRect;
    [SerializeField] private CanvasGroup menuRootCanvasGroup;
    [SerializeField] private RectTransform footerRect;
    [SerializeField] private CanvasGroup footerCanvasGroup;
    [SerializeField] private TMP_Text versionText;
    [SerializeField] private TMP_Text copyrightText;

    [Header("Font Stability")]
    [SerializeField] private TMP_FontAsset readableFallbackFontAsset;

    [Header("Menu Buttons")]
    [SerializeField] private List<TitleMenuEntryBinding> menuEntries = new();

    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip hoverSfx;
    [SerializeField] private AudioClip clickSfx;

    [Header("Scene Transition")]
    [SerializeField] private CanvasGroup sceneFadeCanvasGroup;
#pragma warning disable CS0414
    [SerializeField] [Min(0.1f)] private float startTransitionDuration = 0.42f;
    [SerializeField] [Range(0f, 1f)] private float startTransitionFlashAlpha = 0.16f;
#pragma warning restore CS0414

    private Sequence _introSequence;
    private Tween _introFailsafeTween;
    private Tween _backgroundMoveTween;
    private Tween _backgroundScaleTween;
    private Sequence _startTransitionSequence;
    private readonly TitleSceneIntroComposer _introComposer = new();
    private readonly List<CozyTitleButtonFx> _buttonFx = new();
    private readonly List<(Button button, UnityEngine.Events.UnityAction callback)> _clickBindings = new();
    private readonly TitleSceneStateMachine _stateMachine = new();
    private ITitleSceneNavigator _sceneNavigator = new UnityTitleSceneNavigator();
    private ITitleMenuInteractor _menuInteractor;
    private bool _hasDeluxeFx;
    private static bool s_hasLoggedFontStabilizationInfo;

    private Vector2 _titleDefaultPos;
    private Vector2 _menuDefaultPos;
    private Vector2 _footerDefaultPos;
    private Vector2 _backgroundDefaultPos;
    private Vector3 _backgroundDefaultScale;

    private string _resolvedStartSceneName;
    private bool _resolvedUseUnscaledTime;
    private float _resolvedIntroTitleDuration;
    private float _resolvedIntroMenuDuration;
    private float _resolvedIntroButtonDuration;
    private float _resolvedIntroButtonStagger;
    private float _resolvedIntroTitleOffsetY;
    private float _resolvedIntroMenuOffsetY;
    private float _resolvedIntroButtonOffsetY;
    private float _resolvedHoverScale;
    private float _resolvedHoverOffsetX;
    private float _resolvedHoverOffsetY;
    private float _resolvedPressedScale;
    private float _resolvedHoverTweenDuration;
    private float _resolvedBackgroundFloatStrength;
    private float _resolvedBackgroundFloatDuration;
    private string _resolvedPreloadCharacters;

    public void SetSceneNavigator(ITitleSceneNavigator sceneNavigator)
    {
        _sceneNavigator = sceneNavigator ?? new UnityTitleSceneNavigator();
        BuildMenuInteractor();
    }

    private void Awake()
    {
        ResolveSettings();
        if (!enabled)
        {
            return;
        }

        BuildMenuInteractor();
        EnsureViewReferences();
        TitleSceneReferenceResolver.SyncEntryReferences(menuEntries);
        _hasDeluxeFx = GetComponent<TitleSceneDeluxeFx>() != null;
        CacheDefaults();
        InitializeStaticTexts();
        StabilizeAndPrewarmTitleFonts();
        SetupButtons();
        PrepareInitialState();
    }

    private void OnEnable()
    {
        PlayIntro();
    }

    private void OnDisable()
    {
        KillTweens();
    }

    private void OnDestroy()
    {
        foreach ((Button button, UnityEngine.Events.UnityAction callback) in _clickBindings)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(callback);
            }
        }

        _clickBindings.Clear();
        KillTweens();
    }

    private void OnValidate()
    {
        ResolveSettings();
        EnsureViewReferences();
        TitleSceneReferenceResolver.SyncEntryReferences(menuEntries);
        TitleSceneReferenceResolver.WarnMissingReferences(menuEntries, "[CozyCaveTitleController]");
    }

    private void ResolveSettings()
    {
        TitleSceneConfig config = ResolveConfigAsset();
        if (config == null)
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[CozyCaveTitleController] titleConfig is required. Assign a TitleSceneConfig asset.");
                enabled = false;
            }

            return;
        }

        _resolvedStartSceneName = config.startSceneName;
        _resolvedUseUnscaledTime = config.useUnscaledTime;
        _resolvedIntroTitleDuration = config.introTitleDuration;
        _resolvedIntroMenuDuration = config.introMenuDuration;
        _resolvedIntroButtonDuration = config.introButtonDuration;
        _resolvedIntroButtonStagger = config.introButtonStagger;
        _resolvedIntroTitleOffsetY = config.introTitleOffsetY;
        _resolvedIntroMenuOffsetY = config.introMenuOffsetY;
        _resolvedIntroButtonOffsetY = config.introButtonOffsetY;
        _resolvedHoverScale = config.hoverScale;
        _resolvedHoverOffsetX = config.hoverOffsetX;
        _resolvedHoverOffsetY = config.hoverOffsetY;
        _resolvedPressedScale = config.pressedScale;
        _resolvedHoverTweenDuration = config.hoverTweenDuration;
        _resolvedBackgroundFloatStrength = config.backgroundFloatStrength;
        _resolvedBackgroundFloatDuration = config.backgroundFloatDuration;
        _resolvedPreloadCharacters = config.preloadCharacters;
    }

    private void BuildMenuInteractor()
    {
        string startScene = _resolvedStartSceneName;
        if (string.IsNullOrWhiteSpace(startScene) && titleConfig != null)
        {
            startScene = titleConfig.startSceneName;
        }

        _menuInteractor = new TitleMenuInteractor(_stateMachine, _sceneNavigator, startScene);
    }

    private TitleSceneConfig ResolveConfigAsset()
    {
        if (titleConfig != null)
        {
            return titleConfig;
        }

#if UNITY_EDITOR
        if (string.IsNullOrWhiteSpace(resourcesConfigPath))
        {
            resourcesConfigPath = DefaultConfigResourcePath;
        }

        titleConfig = Resources.Load<TitleSceneConfig>(resourcesConfigPath);
#endif

        return titleConfig;
    }

    private void EnsureViewReferences()
    {
        RectTransform canvasRect = transform as RectTransform;
        if (canvasRect == null)
        {
            return;
        }

        TitleSceneReferenceResolver.EnsureTitleReferences(
            canvasRect,
            ref titleRect,
            ref titleCanvasGroup);

        sceneFadeCanvasGroup = TitleSceneReferenceResolver.EnsureTransitionOverlay(
            canvasRect,
            sceneFadeCanvasGroup);
    }

    private void InitializeStaticTexts()
    {
        if (versionText != null && string.IsNullOrWhiteSpace(versionText.text))
        {
            versionText.text = $"v{Application.version}";
        }

        if (copyrightText != null && string.IsNullOrWhiteSpace(copyrightText.text))
        {
            copyrightText.text = $"© {DateTime.Now.Year} Cozy Cave Crew";
        }
    }

    private void CacheDefaults()
    {
        if (titleRect != null)
        {
            _titleDefaultPos = titleRect.anchoredPosition;
        }

        if (menuRootRect != null)
        {
            _menuDefaultPos = menuRootRect.anchoredPosition;
        }

        if (footerRect != null)
        {
            _footerDefaultPos = footerRect.anchoredPosition;
        }

        if (backgroundRect != null)
        {
            _backgroundDefaultPos = backgroundRect.anchoredPosition;
            _backgroundDefaultScale = backgroundRect.localScale;
        }
    }

    /// <summary>
    /// 壊れたTMPアトラスを安全フォントへ切り替えた上で、必要グリフを先読みする。
    /// </summary>
    private void StabilizeAndPrewarmTitleFonts()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        TMP_FontAsset fallbackFontAsset = TitleSceneTmpStabilizer.ResolveReadableFallbackFont(readableFallbackFontAsset);
        int replaced = TitleSceneTmpStabilizer.StabilizeTexts(texts, fallbackFontAsset);

        if (replaced > 0)
        {
            string fallbackName = fallbackFontAsset != null ? fallbackFontAsset.name : "None";
            if (!s_hasLoggedFontStabilizationInfo)
            {
                Debug.Log($"[CozyCaveTitleController] Rebound {replaced} TMP labels to '{fallbackName}' because the original atlas looked invalid.");
                s_hasLoggedFontStabilizationInfo = true;
            }
        }

        PrewarmTitleFonts(texts);
    }

    private void PrewarmTitleFonts(IReadOnlyList<TMP_Text> texts)
    {
        if (texts == null || texts.Count == 0)
        {
            return;
        }

        HashSet<TMP_FontAsset> fonts = new();

        for (int i = 0; i < texts.Count; i++)
        {
            TMP_Text text = texts[i];
            if (text?.font != null)
            {
                CollectFontAndFallbacks(fonts, text.font);
            }
        }

        if (fonts.Count == 0)
        {
            return;
        }

        string characters = MergeUniqueCharacters(_resolvedPreloadCharacters, TitleSceneConfig.DefaultPreloadCharacters);

        foreach (TMP_FontAsset font in fonts)
        {
            if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            {
                continue;
            }

            font.isMultiAtlasTexturesEnabled = true;
            string supportedCharacters = FilterSupportedCharacters(font, characters);
            if (!string.IsNullOrEmpty(supportedCharacters))
            {
                font.TryAddCharacters(supportedCharacters, out _);
            }
        }

        for (int i = 0; i < texts.Count; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            text.havePropertiesChanged = true;
            text.ForceMeshUpdate(true, true);
        }
    }

    private static void CollectFontAndFallbacks(HashSet<TMP_FontAsset> cache, TMP_FontAsset font)
    {
        if (font == null || !cache.Add(font))
        {
            return;
        }

        List<TMP_FontAsset> fallbackFonts = font.fallbackFontAssetTable;
        if (fallbackFonts == null || fallbackFonts.Count == 0)
        {
            return;
        }

        foreach (TMP_FontAsset fallbackFont in fallbackFonts)
        {
            CollectFontAndFallbacks(cache, fallbackFont);
        }
    }

    private static string FilterSupportedCharacters(TMP_FontAsset font, string characters)
    {
        if (font == null || string.IsNullOrEmpty(characters))
        {
            return string.Empty;
        }

        HashSet<char> uniqueCharacters = new();
        List<char> supportedCharacters = new();
        Font sourceFont = font.sourceFontFile;
        bool allowSourceExpansion = sourceFont != null;

        foreach (char character in characters)
        {
            if (char.IsControl(character) || !uniqueCharacters.Add(character))
            {
                continue;
            }

            bool existsInSource = allowSourceExpansion && sourceFont.HasCharacter(character);
            bool existsInFont = font.HasCharacter(character, false, false);
            if (existsInSource || existsInFont)
            {
                supportedCharacters.Add(character);
            }
        }

        return supportedCharacters.Count == 0 ? string.Empty : new string(supportedCharacters.ToArray());
    }

    private static string MergeUniqueCharacters(string preferred, string fallback)
    {
        HashSet<char> seen = new();
        StringBuilder builder = new(
            (preferred?.Length ?? 0) +
            (fallback?.Length ?? 0));

        AppendCharacters(preferred, seen, builder);
        AppendCharacters(fallback, seen, builder);
        return builder.ToString();
    }

    private static void AppendCharacters(string source, ISet<char> seen, StringBuilder builder)
    {
        if (string.IsNullOrEmpty(source))
        {
            return;
        }

        foreach (char character in source)
        {
            if (!seen.Add(character))
            {
                continue;
            }

            builder.Append(character);
        }
    }

    private void SetupButtons()
    {
        foreach ((Button button, UnityEngine.Events.UnityAction callback) in _clickBindings)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(callback);
            }
        }

        _buttonFx.Clear();
        _clickBindings.Clear();

        if (menuEntries == null)
        {
            return;
        }

        HashSet<TitleMenuAction> registeredActions = new();

        foreach (TitleMenuEntryBinding entry in menuEntries)
        {
            if (!TryResolveMenuEntry(entry, out Button button, out RectTransform visual, out CanvasGroup group, out TMP_Text label, out CozyTitleButtonFx fx))
            {
                continue;
            }

            fx.Configure(
                button,
                visual,
                group,
                label,
                _resolvedHoverScale,
                _resolvedPressedScale,
                _resolvedHoverOffsetX,
                _resolvedHoverOffsetY,
                _resolvedHoverTweenDuration,
                sfxSource,
                hoverSfx,
                clickSfx,
                _resolvedUseUnscaledTime);

            _buttonFx.Add(fx);

            TitleMenuAction action = entry.action;
            UnityEngine.Events.UnityAction callback = () => HandleMenuAction(action);
            button.onClick.AddListener(callback);
            _clickBindings.Add((button, callback));
            registeredActions.Add(action);
        }

        if (!registeredActions.Contains(TitleMenuAction.Exit))
        {
            Debug.LogWarning("[CozyCaveTitleController] Exit action is not mapped in menuEntries.");
        }
    }

    private bool TryResolveMenuEntry(
        TitleMenuEntryBinding entry,
        out Button button,
        out RectTransform visual,
        out CanvasGroup group,
        out TMP_Text label,
        out CozyTitleButtonFx fx)
    {
        bool resolved = TitleSceneReferenceResolver.TryResolveMenuEntry(
            entry,
            out button,
            out visual,
            out group,
            out label,
            out fx);

        if (!resolved)
        {
            string buttonName = entry?.button != null ? entry.button.name : "(null)";
            Debug.LogError($"[CozyCaveTitleController] Menu entry '{buttonName}' is missing required references (visual/canvasGroup/buttonFx).");
            return false;
        }

        return true;
    }

    private void PrepareInitialState()
    {
        _introComposer.PrepareInitialState(BuildIntroContext(), _buttonFx);
    }

    private void PlayIntro()
    {
        _stateMachine.EnterIntro();
        KillTweens();
        PrepareInitialState();

        TitleSceneIntroComposer.Context introContext = BuildIntroContext();
        _introSequence = _introComposer.BuildIntroSequence(introContext, _buttonFx, CompleteIntroState);

        float failsafeDelay = _introComposer.ComputeFailsafeDelay(introContext, _buttonFx.Count);
        _introFailsafeTween = DOVirtual
            .DelayedCall(failsafeDelay, CompleteIntroState, _resolvedUseUnscaledTime)
            .SetUpdate(_resolvedUseUnscaledTime);
    }

    private TitleSceneIntroComposer.Context BuildIntroContext()
    {
        return new TitleSceneIntroComposer.Context
        {
            UseUnscaledTime = _resolvedUseUnscaledTime,
            IntroTitleDuration = _resolvedIntroTitleDuration,
            IntroMenuDuration = _resolvedIntroMenuDuration,
            IntroButtonDuration = _resolvedIntroButtonDuration,
            IntroButtonStagger = _resolvedIntroButtonStagger,
            IntroTitleOffsetY = _resolvedIntroTitleOffsetY,
            IntroMenuOffsetY = _resolvedIntroMenuOffsetY,
            IntroButtonOffsetY = _resolvedIntroButtonOffsetY,
            BackgroundRect = backgroundRect,
            BackgroundCanvasGroup = backgroundCanvasGroup,
            TitleRect = titleRect,
            TitleCanvasGroup = titleCanvasGroup,
            MenuRootRect = menuRootRect,
            MenuRootCanvasGroup = menuRootCanvasGroup,
            FooterRect = footerRect,
            FooterCanvasGroup = footerCanvasGroup,
            SceneFadeCanvasGroup = sceneFadeCanvasGroup,
            TitleDefaultPos = _titleDefaultPos,
            MenuDefaultPos = _menuDefaultPos,
            FooterDefaultPos = _footerDefaultPos,
            BackgroundDefaultPos = _backgroundDefaultPos,
            BackgroundDefaultScale = _backgroundDefaultScale
        };
    }

    private void CompleteIntroState()
    {
        if (_stateMachine.Current != TitleSceneState.IntroPlaying)
        {
            return;
        }

        _stateMachine.TryFire(TitleSceneTrigger.IntroComplete);
        ForceSettledState();
        StartBackgroundIdle();
        RefreshModalState();
    }

    private void StartBackgroundIdle()
    {
        _backgroundMoveTween?.Kill();
        _backgroundScaleTween?.Kill();

        if (backgroundRect == null)
        {
            return;
        }

        if (!_hasDeluxeFx)
        {
            _backgroundMoveTween = backgroundRect
                .DOAnchorPosY(_backgroundDefaultPos.y + _resolvedBackgroundFloatStrength, _resolvedBackgroundFloatDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(_resolvedUseUnscaledTime);
        }

        _backgroundScaleTween = backgroundRect
            .DOScale(_backgroundDefaultScale * 1.015f, _resolvedBackgroundFloatDuration * 0.9f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(_resolvedUseUnscaledTime);
    }

    private void ForceSettledState()
    {
        if (backgroundRect != null)
        {
            backgroundRect.anchoredPosition = _backgroundDefaultPos;
            backgroundRect.localScale = _backgroundDefaultScale;
        }
        if (backgroundCanvasGroup != null)
        {
            backgroundCanvasGroup.alpha = 1f;
        }

        if (titleRect != null)
        {
            titleRect.anchoredPosition = _titleDefaultPos;
            titleRect.localScale = Vector3.one;
        }
        if (titleCanvasGroup != null)
        {
            titleCanvasGroup.alpha = 1f;
        }

        if (menuRootRect != null)
        {
            menuRootRect.anchoredPosition = _menuDefaultPos;
            menuRootRect.localScale = Vector3.one;
        }
        if (menuRootCanvasGroup != null)
        {
            menuRootCanvasGroup.alpha = 1f;
        }

        if (footerRect != null)
        {
            footerRect.anchoredPosition = _footerDefaultPos;
        }
        if (footerCanvasGroup != null)
        {
            footerCanvasGroup.alpha = 1f;
        }

        TitleSceneViewFacade.ResetSceneFadeOverlay(sceneFadeCanvasGroup);

        foreach (CozyTitleButtonFx fx in _buttonFx)
        {
            fx.ForceVisible();
        }
    }

    private void HandleMenuAction(TitleMenuAction action)
    {
        TitleCommand command = _menuInteractor != null
            ? _menuInteractor.Handle(action)
            : TitleCommand.None(TitleCommandFailure.InvalidState);

        if (command.Type == TitleCommandType.None)
        {
            HandleCommandFailure(action, command.Failure);
            return;
        }

        switch (command.Type)
        {
            case TitleCommandType.LoadScene:
                StartGame(command.SceneName);
                break;
            case TitleCommandType.ToggleSettings:
                ToggleSinglePanel(settingsPanel, creditsPanel);
                break;
            case TitleCommandType.ToggleCredits:
                ToggleSinglePanel(creditsPanel, settingsPanel);
                break;
            case TitleCommandType.Quit:
                QuitGame();
                break;
        }
    }

    private void HandleCommandFailure(TitleMenuAction action, TitleCommandFailure failure)
    {
        if (action != TitleMenuAction.StartGame)
        {
            return;
        }

        if (failure == TitleCommandFailure.StartSceneNameMissing)
        {
            Debug.LogWarning("[CozyCaveTitleController] startSceneName is empty.");
            return;
        }

        if (failure == TitleCommandFailure.StartSceneNotInBuildSettings)
        {
            Debug.LogWarning($"[CozyCaveTitleController] Scene '{_resolvedStartSceneName}' is not in Build Settings.");
        }
    }

    private void StartGame(string sceneName)
    {
        _startTransitionSequence?.Kill();
        IrisTransition.Instance.LoadScene(sceneName);
    }

    private void ToggleSinglePanel(GameObject target, GameObject other)
    {
        TitleSceneViewFacade.ToggleSinglePanel(target, other);
        RefreshModalState();
    }

    private void RefreshModalState()
    {
        bool hasModal = TitleSceneViewFacade.HasOpenModal(settingsPanel, creditsPanel);

        if (hasModal)
        {
            if (!_stateMachine.TryFire(TitleSceneTrigger.OpenModal))
            {
                Debug.LogWarning($"[CozyCaveTitleController] Could not open modal from state '{_stateMachine.Current}'.");
            }
        }
        else
        {
            if (!_stateMachine.TryFire(TitleSceneTrigger.CloseModal) &&
                _stateMachine.Current != TitleSceneState.Ready)
            {
                Debug.LogWarning($"[CozyCaveTitleController] Could not close modal from state '{_stateMachine.Current}'.");
            }
        }
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void KillTweens()
    {
        _introSequence?.Kill();
        _introSequence = null;

        _introFailsafeTween?.Kill();
        _introFailsafeTween = null;

        _backgroundMoveTween?.Kill();
        _backgroundMoveTween = null;

        _backgroundScaleTween?.Kill();
        _backgroundScaleTween = null;

        _startTransitionSequence?.Kill();
        _startTransitionSequence = null;

        foreach (CozyTitleButtonFx fx in _buttonFx)
        {
            fx.KillTweens();
        }

        TitleSceneViewFacade.ResetSceneFadeOverlay(sceneFadeCanvasGroup);
    }
}
