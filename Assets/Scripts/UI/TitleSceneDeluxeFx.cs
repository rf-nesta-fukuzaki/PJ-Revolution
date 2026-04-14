using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class TitleSceneDeluxeFx : MonoBehaviour
{
    [Serializable]
    private sealed class SparkleRuntime
    {
        public RectTransform rect;
        public CanvasGroup group;
        public RawImage image;
        public Sequence sequence;
    }

    [Serializable]
    private sealed class ButtonHaloRuntime
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Sequence sequence;
    }

    [Header("References (Auto-bind enabled by default)")]
    [SerializeField] private bool autoBindReferences = true;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private RectTransform backgroundRect;
    [SerializeField] private RectTransform titleRect;
    [SerializeField] private RectTransform menuRootRect;
    [SerializeField] private RectTransform footerRect;
    [SerializeField] private List<Button> menuButtons = new();

    [Header("Parallax")]
    [SerializeField] [Range(0f, 80f)] private float backgroundParallaxStrength = 24f;
    [SerializeField] [Range(0f, 40f)] private float titleParallaxStrength = 12f;
    [SerializeField] [Range(0f, 30f)] private float menuParallaxStrength = 9f;
    [SerializeField] [Range(0f, 20f)] private float footerParallaxStrength = 4.5f;
    [SerializeField] [Range(1f, 24f)] private float parallaxDamping = 10f;

    [Header("Ambient Glow")]
    [SerializeField] [Range(0f, 1f)] private float warmGlowMinAlpha = 0.08f;
    [SerializeField] [Range(0f, 1f)] private float warmGlowMaxAlpha = 0.26f;
    [SerializeField] [Min(0.2f)] private float warmGlowPulseDuration = 2.6f;
    [SerializeField] [Range(0f, 1f)] private float coolGlowMinAlpha = 0.05f;
    [SerializeField] [Range(0f, 1f)] private float coolGlowMaxAlpha = 0.16f;
    [SerializeField] [Min(0.2f)] private float coolGlowPulseDuration = 3.7f;
    [SerializeField] [Range(0f, 1f)] private float titleAuraMinAlpha = 0.08f;
    [SerializeField] [Range(0f, 1f)] private float titleAuraMaxAlpha = 0.22f;
    [SerializeField] [Min(0.2f)] private float titleAuraPulseDuration = 1.7f;
    [SerializeField] [Min(0f)] private float titleFloatStrength = 8f;
    [SerializeField] [Min(0.2f)] private float titleFloatDuration = 3.2f;

    [Header("Sparkles")]
    [SerializeField] [Range(8, 64)] private int sparkleCount = 28;
    [SerializeField] [Min(0f)] private float sparkleSpawnPadding = 60f;
    [SerializeField] [Min(0.1f)] private float sparkleMinLifetime = 1.6f;
    [SerializeField] [Min(0.1f)] private float sparkleMaxLifetime = 3.4f;
    [SerializeField] [Min(1f)] private float sparkleMinSize = 6f;
    [SerializeField] [Min(1f)] private float sparkleMaxSize = 18f;
    [SerializeField] [Range(0.02f, 1f)] private float sparkleMinAlpha = 0.14f;
    [SerializeField] [Range(0.02f, 1f)] private float sparkleMaxAlpha = 0.42f;

    [Header("Button Halos")]
    [SerializeField] [Range(0f, 1f)] private float buttonHaloMaxAlpha = 0.25f;
    [SerializeField] [Min(0.2f)] private float buttonHaloPulseDuration = 1.5f;
    [SerializeField] [Min(0f)] private float buttonHaloScaleBoost = 0.09f;

    [Header("Timing")]
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] [Min(0f)] private float introSettleDelay = 1.1f;

    private static Texture2D s_softCircleTexture;

    private RectTransform _fxBackdropRoot;
    private RectTransform _fxForegroundRoot;
    private RectTransform _sparkleRoot;
    private CanvasGroup _warmGlowGroup;
    private CanvasGroup _coolGlowGroup;
    private CanvasGroup _titleAuraGroup;
    private CanvasGroup _titleShimmerGroup;
    private RectTransform _titleVisualRoot;

    private readonly List<SparkleRuntime> _sparkles = new();
    private readonly List<ButtonHaloRuntime> _buttonHalos = new();
    private readonly List<Coroutine> _sparkleLoopCoroutines = new();

    private Tween _warmGlowTween;
    private Tween _coolGlowTween;
    private Tween _titleAuraTween;
    private Tween _titleShimmerTween;
    private Tween _titleFloatTween;
    private Tween _introUnlockTween;

    private Vector2 _backgroundDefaultPos;
    private Vector2 _titleDefaultPos;
    private Vector2 _menuDefaultPos;
    private Vector2 _footerDefaultPos;

    private Vector2 _backgroundParallaxOffset;
    private Vector2 _titleParallaxOffset;
    private Vector2 _menuParallaxOffset;
    private Vector2 _footerParallaxOffset;

    private bool _parallaxUnlocked;

    private void Awake()
    {
        ResolveReferences();
        BuildFxHierarchy();
        BuildSparkles();
        BuildButtonHalos();
        CacheDefaults();
    }

    private void OnEnable()
    {
        PlayIntroAndIdle();
    }

    private void OnDisable()
    {
        KillTweens();
        ResetToDefaults();
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    private void OnValidate()
    {
        if (!autoBindReferences)
        {
            return;
        }

        if (canvasRect == null)
        {
            canvasRect = transform as RectTransform;
        }
    }

    private void LateUpdate()
    {
        if (!_parallaxUnlocked)
        {
            return;
        }

        if (parallaxDamping <= 0f)
        {
            return;
        }

        if (backgroundRect == null && _titleVisualRoot == null && menuRootRect == null && footerRect == null)
        {
            return;
        }

        if (backgroundParallaxStrength <= 0f &&
            titleParallaxStrength <= 0f &&
            menuParallaxStrength <= 0f &&
            footerParallaxStrength <= 0f)
        {
            return;
        }

        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (delta <= 0f)
        {
            return;
        }

        float width = Mathf.Max(Screen.width, 1);
        float height = Mathf.Max(Screen.height, 1);
        Vector2 pointerPosition = GetPointerScreenPosition(width, height);
        Vector2 pointer = new(
            Mathf.Clamp01(pointerPosition.x / width),
            Mathf.Clamp01(pointerPosition.y / height));
        Vector2 centered = (pointer - Vector2.one * 0.5f) * 2f;

        Vector2 backgroundTarget = new(-centered.x * backgroundParallaxStrength, -centered.y * backgroundParallaxStrength);
        Vector2 titleTarget = new(centered.x * titleParallaxStrength, centered.y * titleParallaxStrength * 0.75f);
        Vector2 menuTarget = new(centered.x * menuParallaxStrength, centered.y * menuParallaxStrength * 0.6f);
        Vector2 footerTarget = new(centered.x * footerParallaxStrength, centered.y * footerParallaxStrength * 0.3f);

        float t = Mathf.Clamp01(parallaxDamping * delta);
        _backgroundParallaxOffset = Vector2.Lerp(_backgroundParallaxOffset, backgroundTarget, t);
        _titleParallaxOffset = Vector2.Lerp(_titleParallaxOffset, titleTarget, t);
        _menuParallaxOffset = Vector2.Lerp(_menuParallaxOffset, menuTarget, t);
        _footerParallaxOffset = Vector2.Lerp(_footerParallaxOffset, footerTarget, t);

        if (backgroundRect != null)
        {
            backgroundRect.anchoredPosition = _backgroundDefaultPos + _backgroundParallaxOffset;
        }

        if (_titleVisualRoot != null)
        {
            _titleVisualRoot.anchoredPosition = _titleDefaultPos + _titleParallaxOffset;
        }

        if (menuRootRect != null)
        {
            menuRootRect.anchoredPosition = _menuDefaultPos + _menuParallaxOffset;
        }

        if (footerRect != null)
        {
            footerRect.anchoredPosition = _footerDefaultPos + _footerParallaxOffset;
        }
    }

    private void ResolveReferences()
    {
        if (canvasRect == null)
        {
            canvasRect = transform as RectTransform;
        }

        if (!autoBindReferences || canvasRect == null)
        {
            return;
        }

        backgroundRect = backgroundRect == null ? TitleSceneReferenceResolver.FindDescendantRect(canvasRect, "BG_Root") : backgroundRect;
        menuRootRect = menuRootRect == null ? TitleSceneReferenceResolver.FindDescendantRect(canvasRect, "Menu_Root") : menuRootRect;
        footerRect = footerRect == null ? TitleSceneReferenceResolver.FindDescendantRect(canvasRect, "Footer_Root") : footerRect;
        titleRect = titleRect == null ? TitleSceneReferenceResolver.FindDescendantRect(canvasRect, "BG_Darken") : titleRect;

        if (menuButtons.Count == 0 && menuRootRect != null)
        {
            Button[] buttons = menuRootRect.GetComponentsInChildren<Button>(true);
            menuButtons.AddRange(buttons);
        }
    }

    private void CacheDefaults()
    {
        if (backgroundRect != null)
        {
            _backgroundDefaultPos = backgroundRect.anchoredPosition;
        }

        if (_titleVisualRoot != null)
        {
            _titleDefaultPos = _titleVisualRoot.anchoredPosition;
        }
        else if (titleRect != null)
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

        _backgroundParallaxOffset = Vector2.zero;
        _titleParallaxOffset = Vector2.zero;
        _menuParallaxOffset = Vector2.zero;
        _footerParallaxOffset = Vector2.zero;
    }

    private void ResetToDefaults()
    {
        _parallaxUnlocked = false;

        if (backgroundRect != null)
        {
            backgroundRect.anchoredPosition = _backgroundDefaultPos;
        }

        if (_titleVisualRoot != null)
        {
            _titleVisualRoot.anchoredPosition = _titleDefaultPos;
        }

        if (menuRootRect != null)
        {
            menuRootRect.anchoredPosition = _menuDefaultPos;
        }

        if (footerRect != null)
        {
            footerRect.anchoredPosition = _footerDefaultPos;
        }
    }

    private void BuildFxHierarchy()
    {
        if (canvasRect == null)
        {
            return;
        }

        _fxBackdropRoot = EnsureStretchRect("FX_DeluxeBackdrop", canvasRect);
        _fxForegroundRoot = EnsureStretchRect("FX_DeluxeForeground", canvasRect);

        if (menuRootRect != null)
        {
            _fxBackdropRoot.SetSiblingIndex(Mathf.Clamp(menuRootRect.GetSiblingIndex() - 1, 0, canvasRect.childCount - 1));
            _fxForegroundRoot.SetSiblingIndex(Mathf.Clamp(menuRootRect.GetSiblingIndex() + 1, 0, canvasRect.childCount - 1));
        }

        _warmGlowGroup = EnsureGlow(
            _fxBackdropRoot,
            "FX_WarmGlow",
            new Vector2(-220f, -150f),
            new Vector2(980f, 700f),
            new Color(1f, 0.48f, 0.08f, warmGlowMaxAlpha));

        _coolGlowGroup = EnsureGlow(
            _fxBackdropRoot,
            "FX_CoolGlow",
            new Vector2(360f, 230f),
            new Vector2(760f, 640f),
            new Color(0.35f, 0.45f, 1f, coolGlowMaxAlpha));

        if (titleRect != null)
        {
            _titleVisualRoot = EnsureStretchRect("FX_TitleVisualRoot", titleRect);
            _titleVisualRoot.SetAsFirstSibling();

            _titleAuraGroup = EnsureGlow(
                _titleVisualRoot,
                "FX_TitleAura",
                new Vector2(0f, 438f),
                new Vector2(1080f, 220f),
                new Color(1f, 0.9f, 0.42f, titleAuraMaxAlpha));

            _titleShimmerGroup = EnsureGlow(
                _titleVisualRoot,
                "FX_TitleShimmer",
                new Vector2(0f, 438f),
                new Vector2(880f, 128f),
                new Color(1f, 0.97f, 0.74f, 0.18f));
        }

        _sparkleRoot = EnsureStretchRect("FX_Sparkles", _fxForegroundRoot);
    }

    private void BuildSparkles()
    {
        if (_sparkleRoot == null)
        {
            return;
        }

        while (_sparkles.Count > sparkleCount)
        {
            int last = _sparkles.Count - 1;
            SparkleRuntime sparkle = _sparkles[last];
            if (sparkle?.rect != null)
            {
                DestroySafe(sparkle.rect.gameObject);
            }

            _sparkles.RemoveAt(last);
        }

        while (_sparkles.Count < sparkleCount)
        {
            int index = _sparkles.Count;
            GameObject sparkleObject = new($"FX_Sparkle_{index + 1:00}", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(CanvasGroup));
            RectTransform rect = sparkleObject.GetComponent<RectTransform>();
            rect.SetParent(_sparkleRoot, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            RawImage image = sparkleObject.GetComponent<RawImage>();
            image.texture = GetSoftCircleTexture();
            image.raycastTarget = false;

            CanvasGroup group = sparkleObject.GetComponent<CanvasGroup>();
            group.alpha = 0f;

            _sparkles.Add(new SparkleRuntime
            {
                rect = rect,
                image = image,
                group = group
            });
        }
    }

    private void BuildButtonHalos()
    {
        foreach (ButtonHaloRuntime halo in _buttonHalos)
        {
            if (halo?.rect != null)
            {
                DestroySafe(halo.rect.gameObject);
            }
        }

        _buttonHalos.Clear();

        foreach (Button button in menuButtons)
        {
            if (button == null)
            {
                continue;
            }

            RectTransform buttonRect = button.transform as RectTransform;
            Image sourceImage = button.targetGraphic as Image;
            if (buttonRect == null || sourceImage == null || sourceImage.sprite == null)
            {
                continue;
            }

            GameObject haloObject = new("FX_ButtonHalo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            RectTransform haloRect = haloObject.GetComponent<RectTransform>();
            haloRect.SetParent(buttonRect, false);
            haloRect.SetAsFirstSibling();
            haloRect.anchorMin = Vector2.zero;
            haloRect.anchorMax = Vector2.one;
            haloRect.offsetMin = new Vector2(-24f, -18f);
            haloRect.offsetMax = new Vector2(24f, 18f);

            Image haloImage = haloObject.GetComponent<Image>();
            haloImage.sprite = sourceImage.sprite;
            haloImage.type = Image.Type.Simple;
            haloImage.color = new Color(1f, 0.87f, 0.45f, 0f);
            haloImage.raycastTarget = false;

            CanvasGroup haloGroup = haloObject.GetComponent<CanvasGroup>();
            haloGroup.alpha = 0f;

            _buttonHalos.Add(new ButtonHaloRuntime
            {
                rect = haloRect,
                group = haloGroup
            });
        }
    }

    private void PlayIntroAndIdle()
    {
        KillTweens();
        CacheDefaults();

        if (_warmGlowGroup != null)
        {
            _warmGlowGroup.alpha = warmGlowMinAlpha;
            _warmGlowTween = _warmGlowGroup
                .DOFade(warmGlowMaxAlpha, warmGlowPulseDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(useUnscaledTime);
        }

        if (_coolGlowGroup != null)
        {
            _coolGlowGroup.alpha = coolGlowMinAlpha;
            _coolGlowTween = _coolGlowGroup
                .DOFade(coolGlowMaxAlpha, coolGlowPulseDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(useUnscaledTime);
        }

        if (_titleAuraGroup != null)
        {
            _titleAuraGroup.alpha = titleAuraMinAlpha;
            _titleAuraTween = _titleAuraGroup
                .DOFade(titleAuraMaxAlpha, titleAuraPulseDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(useUnscaledTime);
        }

        if (_titleVisualRoot != null && titleFloatStrength > 0f)
        {
            _titleFloatTween = _titleVisualRoot
                .DOAnchorPosY(_titleDefaultPos.y + titleFloatStrength, titleFloatDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(useUnscaledTime);
        }

        PlayTitleShimmer();
        PlaySparkleLoops();
        PlayButtonHaloLoops();

        _introUnlockTween = DOVirtual
            .DelayedCall(introSettleDelay, () =>
            {
                CacheDefaults();
                _parallaxUnlocked = true;
            }, useUnscaledTime)
            .SetUpdate(useUnscaledTime);
    }

    private void PlayTitleShimmer()
    {
        if (_titleShimmerGroup == null)
        {
            return;
        }

        _titleShimmerGroup.alpha = 0f;
        _titleShimmerTween?.Kill();
        Sequence shimmer = DOTween.Sequence().SetLoops(-1).SetUpdate(useUnscaledTime);
        shimmer.AppendInterval(2.3f);
        shimmer.Append(_titleShimmerGroup.DOFade(0.2f, 0.24f).SetEase(Ease.OutSine));
        shimmer.Append(_titleShimmerGroup.DOFade(0f, 0.58f).SetEase(Ease.InSine));
        _titleShimmerTween = shimmer;
    }

    private void PlaySparkleLoops()
    {
        StopSparkleLoops();

        foreach (SparkleRuntime sparkle in _sparkles)
        {
            if (sparkle == null)
            {
                continue;
            }

            Coroutine loop = StartCoroutine(RunSparkleLoop(sparkle, UnityEngine.Random.Range(0f, sparkleMaxLifetime)));
            _sparkleLoopCoroutines.Add(loop);
        }
    }

    private IEnumerator RunSparkleLoop(SparkleRuntime sparkle, float delay)
    {
        float currentDelay = delay;
        while (isActiveAndEnabled)
        {
            Sequence cycle = BuildSparkleCycle(sparkle, currentDelay);
            if (cycle == null)
            {
                yield break;
            }

            sparkle.sequence = cycle;
            yield return cycle.WaitForCompletion();
            currentDelay = UnityEngine.Random.Range(0.12f, 0.7f);
        }
    }

    private Sequence BuildSparkleCycle(SparkleRuntime sparkle, float delay)
    {
        if (sparkle?.rect == null || sparkle.group == null || sparkle.image == null || _sparkleRoot == null)
        {
            return null;
        }

        sparkle.sequence?.Kill();

        float width = Mathf.Max(_sparkleRoot.rect.width * 0.5f - sparkleSpawnPadding, 120f);
        float height = Mathf.Max(_sparkleRoot.rect.height * 0.5f - sparkleSpawnPadding, 90f);

        Vector2 start = new(
            UnityEngine.Random.Range(-width, width),
            UnityEngine.Random.Range(-height, height));
        float size = UnityEngine.Random.Range(sparkleMinSize, sparkleMaxSize);
        float life = UnityEngine.Random.Range(sparkleMinLifetime, sparkleMaxLifetime);
        float driftX = UnityEngine.Random.Range(-28f, 28f);
        float driftY = UnityEngine.Random.Range(36f, 126f);
        float targetAlpha = UnityEngine.Random.Range(sparkleMinAlpha, sparkleMaxAlpha);
        Color tint = Color.Lerp(
            new Color(1f, 0.76f, 0.3f, 1f),
            new Color(0.55f, 0.78f, 1f, 1f),
            UnityEngine.Random.value);

        sparkle.rect.anchoredPosition = start;
        sparkle.rect.sizeDelta = new Vector2(size, size);
        sparkle.rect.localScale = Vector3.one * UnityEngine.Random.Range(0.7f, 1.05f);
        sparkle.group.alpha = 0f;
        sparkle.image.color = tint;

        Sequence sequence = DOTween.Sequence().SetUpdate(useUnscaledTime);
        sequence.AppendInterval(delay);
        sequence.Append(sparkle.group.DOFade(targetAlpha, life * 0.35f).SetEase(Ease.OutSine));
        sequence.Join(sparkle.rect.DOAnchorPos(start + new Vector2(driftX, driftY), life).SetEase(Ease.OutSine));
        sequence.Join(sparkle.rect.DOScale(UnityEngine.Random.Range(1.05f, 1.7f), life).SetEase(Ease.OutSine));
        sequence.Append(sparkle.group.DOFade(0f, life * 0.38f).SetEase(Ease.InSine));
        return sequence;
    }

    private void PlayButtonHaloLoops()
    {
        for (int i = 0; i < _buttonHalos.Count; i++)
        {
            ButtonHaloRuntime halo = _buttonHalos[i];
            if (halo?.rect == null || halo.group == null)
            {
                continue;
            }

            halo.group.alpha = 0f;
            halo.rect.localScale = Vector3.one;

            halo.sequence?.Kill();
            halo.sequence = DOTween.Sequence().SetUpdate(useUnscaledTime).SetLoops(-1);
            halo.sequence.AppendInterval(0.18f * i);
            halo.sequence.Append(halo.group.DOFade(buttonHaloMaxAlpha, buttonHaloPulseDuration * 0.42f).SetEase(Ease.OutSine));
            halo.sequence.Join(halo.rect.DOScale(1f + buttonHaloScaleBoost, buttonHaloPulseDuration).SetEase(Ease.OutSine));
            halo.sequence.Append(halo.group.DOFade(0f, buttonHaloPulseDuration * 0.58f).SetEase(Ease.InSine));
            halo.sequence.Join(halo.rect.DOScale(1f, buttonHaloPulseDuration * 0.58f).SetEase(Ease.InSine));
            halo.sequence.AppendInterval(0.28f);
        }
    }

    private void KillTweens()
    {
        _warmGlowTween?.Kill();
        _warmGlowTween = null;

        _coolGlowTween?.Kill();
        _coolGlowTween = null;

        _titleAuraTween?.Kill();
        _titleAuraTween = null;

        _titleShimmerTween?.Kill();
        _titleShimmerTween = null;

        _titleFloatTween?.Kill();
        _titleFloatTween = null;

        _introUnlockTween?.Kill();
        _introUnlockTween = null;

        StopSparkleLoops();

        foreach (SparkleRuntime sparkle in _sparkles)
        {
            sparkle?.sequence?.Kill();
            if (sparkle?.group != null)
            {
                sparkle.group.alpha = 0f;
            }
        }

        foreach (ButtonHaloRuntime halo in _buttonHalos)
        {
            halo?.sequence?.Kill();
            if (halo?.group != null)
            {
                halo.group.alpha = 0f;
            }
            if (halo?.rect != null)
            {
                halo.rect.localScale = Vector3.one;
            }
        }
    }

    private void StopSparkleLoops()
    {
        for (int i = 0; i < _sparkleLoopCoroutines.Count; i++)
        {
            Coroutine loop = _sparkleLoopCoroutines[i];
            if (loop != null)
            {
                StopCoroutine(loop);
            }
        }

        _sparkleLoopCoroutines.Clear();
    }

    private static Vector2 GetPointerScreenPosition(float fallbackWidth, float fallbackHeight)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#else
        return new Vector2(fallbackWidth * 0.5f, fallbackHeight * 0.5f);
#endif
    }

    private static RectTransform EnsureStretchRect(string name, RectTransform parent)
    {
        RectTransform existing = TitleSceneReferenceResolver.FindDescendantRect(parent, name);
        if (existing != null && existing.parent == parent)
        {
            StretchToParent(existing);
            return existing;
        }

        GameObject created = new(name, typeof(RectTransform));
        RectTransform rect = created.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        StretchToParent(rect);
        return rect;
    }

    private static CanvasGroup EnsureGlow(
        RectTransform parent,
        string name,
        Vector2 anchoredPos,
        Vector2 sizeDelta,
        Color color)
    {
        RectTransform rect = TitleSceneReferenceResolver.FindDescendantRect(parent, name);
        if (rect == null || rect.parent != parent)
        {
            GameObject glowObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(CanvasGroup));
            rect = glowObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = sizeDelta;

        RawImage image = rect.GetComponent<RawImage>();
        image.texture = GetSoftCircleTexture();
        image.color = color;
        image.raycastTarget = false;

        CanvasGroup group = rect.GetComponent<CanvasGroup>();
        group.alpha = color.a;
        return group;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static Texture2D GetSoftCircleTexture()
    {
        if (s_softCircleTexture != null)
        {
            return s_softCircleTexture;
        }

        const int size = 128;
        s_softCircleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = "TitleSoftCircleRuntime",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[size * size];
        Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = Mathf.Pow(alpha, 2.4f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        s_softCircleTexture.SetPixels(pixels);
        s_softCircleTexture.Apply();
        return s_softCircleTexture;
    }

    private static void DestroySafe(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
