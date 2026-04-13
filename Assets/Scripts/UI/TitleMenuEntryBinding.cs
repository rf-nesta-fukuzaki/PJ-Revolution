using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public sealed class TitleMenuEntryBinding
{
    public Button button;
    public RectTransform visualRoot;
    public TMP_Text label;
    public CanvasGroup canvasGroup;
    public CozyTitleButtonFx buttonFx;
    public TitleMenuAction action;
}
