using DG.Tweening;
using UnityEngine;

public static class TitleSceneViewFacade
{
    public static void ToggleSinglePanel(GameObject target, GameObject other)
    {
        if (other != null)
        {
            other.SetActive(false);
        }

        if (target != null)
        {
            target.SetActive(!target.activeSelf);
        }
    }

    public static bool HasOpenModal(GameObject settingsPanel, GameObject creditsPanel)
    {
        return
            (settingsPanel != null && settingsPanel.activeSelf) ||
            (creditsPanel != null && creditsPanel.activeSelf);
    }

    public static void ResetSceneFadeOverlay(CanvasGroup sceneFadeCanvasGroup)
    {
        if (sceneFadeCanvasGroup == null)
        {
            return;
        }

        sceneFadeCanvasGroup.alpha = 0f;
        sceneFadeCanvasGroup.interactable = false;
        sceneFadeCanvasGroup.blocksRaycasts = false;
    }

    public static Sequence CreateSceneStartTransition(
        CanvasGroup sceneFadeCanvasGroup,
        float duration,
        float flashAlpha,
        bool useUnscaledTime,
        TweenCallback onComplete)
    {
        if (sceneFadeCanvasGroup == null)
        {
            onComplete?.Invoke();
            return null;
        }

        sceneFadeCanvasGroup.alpha = 0f;
        sceneFadeCanvasGroup.interactable = true;
        sceneFadeCanvasGroup.blocksRaycasts = true;

        float transitionDuration = Mathf.Max(0.1f, duration);
        float flash = Mathf.Clamp01(flashAlpha);
        Sequence sequence = DOTween.Sequence().SetUpdate(useUnscaledTime);

        if (flash > 0f)
        {
            sequence.Append(sceneFadeCanvasGroup.DOFade(flash, transitionDuration * 0.35f).SetEase(Ease.OutSine));
        }

        float fadeDuration = flash > 0f ? transitionDuration * 0.65f : transitionDuration;
        sequence.Append(sceneFadeCanvasGroup.DOFade(1f, fadeDuration).SetEase(Ease.InSine));
        sequence.OnComplete(onComplete);
        return sequence;
    }
}
