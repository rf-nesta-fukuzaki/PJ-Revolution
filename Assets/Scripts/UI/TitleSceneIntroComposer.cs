using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class TitleSceneIntroComposer
{
    public sealed class Context
    {
        public bool UseUnscaledTime;
        public float IntroTitleDuration;
        public float IntroMenuDuration;
        public float IntroButtonDuration;
        public float IntroButtonStagger;
        public float IntroTitleOffsetY;
        public float IntroMenuOffsetY;
        public float IntroButtonOffsetY;
        public RectTransform BackgroundRect;
        public CanvasGroup BackgroundCanvasGroup;
        public RectTransform TitleRect;
        public CanvasGroup TitleCanvasGroup;
        public RectTransform MenuRootRect;
        public CanvasGroup MenuRootCanvasGroup;
        public RectTransform FooterRect;
        public CanvasGroup FooterCanvasGroup;
        public CanvasGroup SceneFadeCanvasGroup;
        public Vector2 TitleDefaultPos;
        public Vector2 MenuDefaultPos;
        public Vector2 FooterDefaultPos;
        public Vector2 BackgroundDefaultPos;
        public Vector3 BackgroundDefaultScale;
    }

    public void PrepareInitialState(Context context, IReadOnlyList<CozyTitleButtonFx> buttonFx)
    {
        if (context.BackgroundRect != null)
        {
            context.BackgroundRect.anchoredPosition = context.BackgroundDefaultPos;
            context.BackgroundRect.localScale = context.BackgroundDefaultScale * 1.03f;
        }
        if (context.BackgroundCanvasGroup != null)
        {
            context.BackgroundCanvasGroup.alpha = 0f;
        }

        if (context.TitleRect != null)
        {
            context.TitleRect.anchoredPosition = context.TitleDefaultPos + Vector2.up * context.IntroTitleOffsetY;
            context.TitleRect.localScale = Vector3.one * 0.94f;
        }
        if (context.TitleCanvasGroup != null)
        {
            context.TitleCanvasGroup.alpha = 0f;
        }

        if (context.MenuRootRect != null)
        {
            context.MenuRootRect.anchoredPosition = context.MenuDefaultPos + Vector2.down * context.IntroMenuOffsetY;
            context.MenuRootRect.localScale = Vector3.one;
        }
        if (context.MenuRootCanvasGroup != null)
        {
            context.MenuRootCanvasGroup.alpha = 0f;
        }

        if (context.FooterRect != null)
        {
            context.FooterRect.anchoredPosition = context.FooterDefaultPos + Vector2.down * 14f;
        }
        if (context.FooterCanvasGroup != null)
        {
            context.FooterCanvasGroup.alpha = 0f;
        }

        TitleSceneViewFacade.ResetSceneFadeOverlay(context.SceneFadeCanvasGroup);

        foreach (CozyTitleButtonFx fx in buttonFx)
        {
            fx.PrepareIntro(context.IntroButtonOffsetY);
        }
    }

    public Sequence BuildIntroSequence(Context context, IReadOnlyList<CozyTitleButtonFx> buttonFx, TweenCallback onComplete)
    {
        Sequence introSequence = DOTween.Sequence().SetUpdate(context.UseUnscaledTime);

        if (context.BackgroundCanvasGroup != null)
        {
            introSequence.Insert(0f, context.BackgroundCanvasGroup.DOFade(1f, 0.5f).SetEase(Ease.OutSine));
        }
        if (context.BackgroundRect != null)
        {
            introSequence.Insert(0f, context.BackgroundRect.DOScale(context.BackgroundDefaultScale, 0.8f).SetEase(Ease.OutCubic));
        }

        if (context.TitleCanvasGroup != null)
        {
            introSequence.Insert(0.08f, context.TitleCanvasGroup.DOFade(1f, context.IntroTitleDuration).SetEase(Ease.OutSine));
        }
        if (context.TitleRect != null)
        {
            introSequence.Insert(0.08f, context.TitleRect.DOAnchorPos(context.TitleDefaultPos, context.IntroTitleDuration).SetEase(Ease.OutBack));
            introSequence.Insert(0.18f, context.TitleRect.DOScale(1f, context.IntroTitleDuration).SetEase(Ease.OutBack));
            introSequence.Insert(0.58f, context.TitleRect.DOPunchScale(Vector3.one * 0.045f, 0.24f, 7, 0.85f));
        }

        if (context.MenuRootCanvasGroup != null)
        {
            introSequence.Insert(0.2f, context.MenuRootCanvasGroup.DOFade(1f, context.IntroMenuDuration).SetEase(Ease.OutSine));
        }
        if (context.MenuRootRect != null)
        {
            introSequence.Insert(0.2f, context.MenuRootRect.DOAnchorPos(context.MenuDefaultPos, context.IntroMenuDuration).SetEase(Ease.OutCubic));
        }

        for (int i = 0; i < buttonFx.Count; i++)
        {
            float at = 0.28f + i * context.IntroButtonStagger;
            introSequence.Insert(at, buttonFx[i].PlayIntro(context.IntroButtonDuration, context.IntroButtonOffsetY));
        }

        if (context.FooterCanvasGroup != null)
        {
            introSequence.Insert(0.52f, context.FooterCanvasGroup.DOFade(1f, 0.3f).SetEase(Ease.OutSine));
        }
        if (context.FooterRect != null)
        {
            introSequence.Insert(0.52f, context.FooterRect.DOAnchorPos(context.FooterDefaultPos, 0.34f).SetEase(Ease.OutCubic));
        }

        introSequence.OnComplete(onComplete);
        return introSequence;
    }

    public float ComputeFailsafeDelay(Context context, int buttonCount)
    {
        return Mathf.Max(
            context.IntroTitleDuration +
            context.IntroMenuDuration +
            context.IntroButtonDuration +
            context.IntroButtonStagger * Mathf.Max(0, buttonCount - 1) +
            0.8f,
            1.2f);
    }
}
