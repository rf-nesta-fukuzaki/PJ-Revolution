using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CozyTitleButtonFx :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    private static readonly Color LabelNormalColor = new(1f, 0.91f, 0.62f, 1f);
    private static readonly Color LabelHoverColor = new(1f, 0.96f, 0.76f, 1f);
    private static readonly Color BoardNormalColor = new(1f, 1f, 1f, 1f);
    private static readonly Color BoardHoverColor = new(1f, 0.96f, 0.87f, 1f);
    private static readonly Color BoardPressedColor = new(0.95f, 0.9f, 0.84f, 1f);

    private Button _button;
    private RectTransform _visualRoot;
    private CanvasGroup _group;
    private Image _buttonImage;
    private TMP_Text _label;
    private AudioSource _sfxSource;
    private AudioClip _hoverSfx;
    private AudioClip _clickSfx;
    private float _hoverScale;
    private float _pressedScale;
    private float _hoverOffsetX;
    private float _hoverOffsetY;
    private float _hoverTweenDuration;
    private bool _useUnscaledTime;
    private bool _isHovered;
    private Vector2 _defaultPos;
    private Vector3 _defaultScale;

    private Tween _moveTween;
    private Tween _scaleTween;
    private Tween _fadeTween;
    private Tween _labelTween;
    private Tween _imageTween;
    private Sequence _introSequence;
    private Sequence _clickSequence;

    public void Configure(
        Button button,
        RectTransform visualRoot,
        CanvasGroup group,
        TMP_Text label,
        float hoverScale,
        float pressedScale,
        float hoverOffsetX,
        float hoverOffsetY,
        float hoverTweenDuration,
        AudioSource sfxSource,
        AudioClip hoverSfx,
        AudioClip clickSfx,
        bool useUnscaledTime)
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleClickSfx);
        }

        _button = button;
        _visualRoot = visualRoot;
        _group = group;
        _buttonImage = _button != null ? _button.targetGraphic as Image : null;
        _label = label;
        _hoverScale = hoverScale;
        _pressedScale = pressedScale;
        _hoverOffsetX = hoverOffsetX;
        _hoverOffsetY = hoverOffsetY;
        _hoverTweenDuration = hoverTweenDuration;
        _sfxSource = sfxSource;
        _hoverSfx = hoverSfx;
        _clickSfx = clickSfx;
        _useUnscaledTime = useUnscaledTime;

        if (_button == null || _visualRoot == null || _group == null)
        {
            Debug.LogError("[CozyTitleButtonFx] Configure requires button/visualRoot/canvasGroup.");
            return;
        }

        _defaultPos = _visualRoot.anchoredPosition;
        _defaultScale = _visualRoot.localScale;
        _group.alpha = 1f;

        if (_label != null)
        {
            _label.color = LabelNormalColor;
        }
        if (_buttonImage != null)
        {
            _buttonImage.color = BoardNormalColor;
        }

        _button.onClick.RemoveListener(HandleClickSfx);
        _button.onClick.AddListener(HandleClickSfx);
    }

    public void PrepareIntro(float offsetY)
    {
        if (_visualRoot == null || _group == null || _button == null)
        {
            return;
        }

        _isHovered = false;
        _button.interactable = true;
        _group.alpha = 0f;
        _visualRoot.anchoredPosition = _defaultPos + Vector2.down * offsetY;
        _visualRoot.localScale = _defaultScale * 0.96f;
        if (_label != null)
        {
            _label.color = LabelNormalColor;
        }
        if (_buttonImage != null)
        {
            _buttonImage.color = BoardNormalColor;
        }
    }

    public Tween PlayIntro(float duration, float offsetY)
    {
        PrepareIntro(offsetY);
        _introSequence?.Kill();

        if (_visualRoot == null || _group == null)
        {
            _introSequence = DOTween.Sequence();
            return _introSequence;
        }

        _introSequence = DOTween.Sequence().SetUpdate(_useUnscaledTime);
        _fadeTween = _group.DOFade(1f, duration * 0.86f).SetEase(Ease.OutSine).SetUpdate(_useUnscaledTime);
        _moveTween = _visualRoot.DOAnchorPos(_defaultPos, duration).SetEase(Ease.OutCubic).SetUpdate(_useUnscaledTime);
        _scaleTween = _visualRoot.DOScale(_defaultScale, duration).SetEase(Ease.OutBack).SetUpdate(_useUnscaledTime);
        _introSequence.Join(_fadeTween);
        _introSequence.Join(_moveTween);
        _introSequence.Join(_scaleTween);
        _introSequence.OnComplete(ForceVisible);

        return _introSequence;
    }

    public void ForceVisible()
    {
        if (_visualRoot == null || _group == null || _button == null)
        {
            return;
        }

        _group.alpha = 1f;
        _visualRoot.anchoredPosition = _defaultPos;
        _visualRoot.localScale = _defaultScale;
        _button.interactable = true;
        if (_label != null)
        {
            _label.color = LabelNormalColor;
        }
        if (_buttonImage != null)
        {
            _buttonImage.color = BoardNormalColor;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_button == null || !_button.interactable)
        {
            return;
        }

        _isHovered = true;
        AnimateState(
            _defaultPos + new Vector2(_hoverOffsetX, _hoverOffsetY),
            _defaultScale * _hoverScale,
            LabelHoverColor,
            BoardHoverColor,
            _hoverTweenDuration,
            Ease.OutQuad);
        PlayOneShot(_hoverSfx);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        AnimateState(_defaultPos, _defaultScale, LabelNormalColor, BoardNormalColor, _hoverTweenDuration, Ease.OutQuad);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_button == null || !_button.interactable)
        {
            return;
        }

        AnimateState(
            _defaultPos + new Vector2(_hoverOffsetX * 0.6f, _hoverOffsetY * 0.35f),
            _defaultScale * _pressedScale,
            _isHovered ? LabelHoverColor : LabelNormalColor,
            BoardPressedColor,
            0.08f,
            Ease.OutQuad);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_button == null || !_button.interactable)
        {
            return;
        }

        AnimateState(
            _isHovered ? _defaultPos + new Vector2(_hoverOffsetX, _hoverOffsetY) : _defaultPos,
            _isHovered ? _defaultScale * _hoverScale : _defaultScale,
            _isHovered ? LabelHoverColor : LabelNormalColor,
            _isHovered ? BoardHoverColor : BoardNormalColor,
            0.1f,
            Ease.OutBack);
    }

    public void KillTweens()
    {
        _moveTween?.Kill();
        _moveTween = null;

        _scaleTween?.Kill();
        _scaleTween = null;

        _fadeTween?.Kill();
        _fadeTween = null;

        _labelTween?.Kill();
        _labelTween = null;

        _imageTween?.Kill();
        _imageTween = null;

        _introSequence?.Kill();
        _introSequence = null;

        _clickSequence?.Kill();
        _clickSequence = null;
    }

    private void OnDisable()
    {
        KillTweens();
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleClickSfx);
        }
    }

    private void HandleClickSfx()
    {
        if (_visualRoot == null)
        {
            return;
        }

        PlayOneShot(_clickSfx);

        _clickSequence?.Kill();
        _clickSequence = DOTween.Sequence().SetUpdate(_useUnscaledTime);
        _clickSequence.Append(_visualRoot.DOScale(_defaultScale * _pressedScale, 0.045f).SetEase(Ease.OutQuad));
        _clickSequence.Join(_visualRoot.DOPunchRotation(new Vector3(0f, 0f, -3f), 0.12f, 6, 0.8f));
        _clickSequence.Append(_visualRoot.DOScale(_isHovered ? _defaultScale * _hoverScale : _defaultScale, 0.12f).SetEase(Ease.OutBack));
    }

    private void AnimateState(Vector2 pos, Vector3 scale, Color labelColor, Color boardColor, float duration, Ease ease)
    {
        if (_visualRoot == null)
        {
            return;
        }

        _moveTween?.Kill();
        _moveTween = _visualRoot
            .DOAnchorPos(pos, duration)
            .SetEase(ease)
            .SetUpdate(_useUnscaledTime);

        _scaleTween?.Kill();
        _scaleTween = _visualRoot
            .DOScale(scale, duration)
            .SetEase(ease)
            .SetUpdate(_useUnscaledTime);

        if (_label != null)
        {
            _labelTween?.Kill();
            _labelTween = _label
                .DOColor(labelColor, duration)
                .SetEase(Ease.OutSine)
                .SetUpdate(_useUnscaledTime);
        }

        if (_buttonImage != null)
        {
            _imageTween?.Kill();
            _imageTween = _buttonImage
                .DOColor(boardColor, duration)
                .SetEase(Ease.OutSine)
                .SetUpdate(_useUnscaledTime);
        }
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (_sfxSource == null || clip == null)
        {
            return;
        }

        _sfxSource.PlayOneShot(clip);
    }
}
