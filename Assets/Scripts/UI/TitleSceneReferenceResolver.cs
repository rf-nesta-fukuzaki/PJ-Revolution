using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class TitleSceneReferenceResolver
{
    public static void EnsureTitleReferences(
        RectTransform canvasRect,
        ref RectTransform titleRect,
        ref CanvasGroup titleCanvasGroup)
    {
        if (canvasRect == null)
        {
            return;
        }

        if (titleRect == null)
        {
            titleRect = FindDescendantRect(canvasRect, "BG_Darken");
        }

        if (titleRect != null && titleCanvasGroup == null)
        {
            titleCanvasGroup = titleRect.GetComponent<CanvasGroup>();
            if (titleCanvasGroup == null)
            {
                titleCanvasGroup = titleRect.gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    public static CanvasGroup EnsureTransitionOverlay(RectTransform canvasRect, CanvasGroup currentOverlay)
    {
        if (canvasRect == null)
        {
            return currentOverlay;
        }

        RectTransform overlayRect = currentOverlay != null
            ? currentOverlay.transform as RectTransform
            : FindDescendantRect(canvasRect, "FX_SceneFadeOverlay");

        if (overlayRect == null || overlayRect.parent != canvasRect)
        {
            GameObject overlayObject = new(
                "FX_SceneFadeOverlay",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage),
                typeof(CanvasGroup));
            overlayRect = overlayObject.GetComponent<RectTransform>();
            overlayRect.SetParent(canvasRect, false);
        }

        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.pivot = new Vector2(0.5f, 0.5f);
        overlayRect.anchoredPosition = Vector2.zero;
        overlayRect.sizeDelta = Vector2.zero;
        overlayRect.SetAsLastSibling();

        RawImage overlayImage = overlayRect.GetComponent<RawImage>();
        overlayImage.texture = Texture2D.whiteTexture;
        overlayImage.color = new Color(0.07f, 0.04f, 0.02f, 1f);
        overlayImage.raycastTarget = false;

        CanvasGroup overlayGroup = overlayRect.GetComponent<CanvasGroup>();
        overlayGroup.alpha = 0f;
        overlayGroup.interactable = false;
        overlayGroup.blocksRaycasts = false;
        return overlayGroup;
    }

    public static RectTransform FindDescendantRect(RectTransform root, string name)
    {
        if (root == null)
        {
            return null;
        }

        foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
        {
            if (rect != null && string.Equals(rect.name, name, StringComparison.Ordinal))
            {
                return rect;
            }
        }

        return null;
    }

    public static void SyncEntryReferences(IList<TitleMenuEntryBinding> menuEntries)
    {
        if (menuEntries == null)
        {
            return;
        }

        foreach (TitleMenuEntryBinding entry in menuEntries)
        {
            TryResolveMenuEntry(
                entry,
                out _,
                out _,
                out _,
                out _,
                out _);
        }
    }

    public static void WarnMissingReferences(IList<TitleMenuEntryBinding> menuEntries, string logPrefix)
    {
        if (menuEntries == null)
        {
            Debug.LogWarning($"{logPrefix} menuEntries is null.");
            return;
        }

        foreach (TitleMenuEntryBinding entry in menuEntries)
        {
            if (entry?.button == null)
            {
                continue;
            }

            if (entry.buttonFx == null)
            {
                Debug.LogWarning($"{logPrefix} CozyTitleButtonFx is missing on '{entry.button.name}'.");
            }

            if (entry.canvasGroup == null)
            {
                Debug.LogWarning($"{logPrefix} CanvasGroup is missing on '{entry.button.name}'.");
            }
        }
    }

    public static bool TryResolveMenuEntry(
        TitleMenuEntryBinding entry,
        out Button button,
        out RectTransform visualRoot,
        out CanvasGroup canvasGroup,
        out TMP_Text label,
        out CozyTitleButtonFx buttonFx)
    {
        button = null;
        visualRoot = null;
        canvasGroup = null;
        label = null;
        buttonFx = null;

        if (entry == null || entry.button == null)
        {
            return false;
        }

        button = entry.button;
        visualRoot = entry.visualRoot != null
            ? entry.visualRoot
            : entry.button.transform as RectTransform;
        canvasGroup = entry.canvasGroup != null
            ? entry.canvasGroup
            : entry.button.GetComponent<CanvasGroup>();
        label = entry.label != null
            ? entry.label
            : entry.button.GetComponentInChildren<TMP_Text>(true);
        buttonFx = entry.buttonFx != null
            ? entry.buttonFx
            : entry.button.GetComponent<CozyTitleButtonFx>();

        entry.visualRoot = visualRoot;
        entry.canvasGroup = canvasGroup;
        entry.label = label;
        entry.buttonFx = buttonFx;

        return visualRoot != null && canvasGroup != null && buttonFx != null;
    }
}
