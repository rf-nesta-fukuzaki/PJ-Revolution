using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PeakPlunder.Localization;

/// <summary>
/// GDD §21.2 — コンテキストヒントシステム。
/// </summary>
public class HintManager : MonoBehaviour, IHintService
{
    private static HintManager _instance;

    [System.Obsolete("GameServices.Hints を使用してください")]
    public static HintManager Instance => _instance;

    public static class HintId
    {
        public const int FirstClimbApproach = 1;
        public const int DashIntroduction   = 2;
        public const int StaminaDepleted    = 3;
        public const int RelicApproach      = 4;
        public const int RelicWithClimb     = 5;
        public const int RopePlayerNearby   = 6;
        public const int Zone2Entry         = 7;
        public const int ReturnOrZone3      = 8;
    }

    private static readonly string[] HINT_TEXTS =
    {
        "",
        "左クリックで掴もう！黄色いポイントに手を伸ばして",
        "Shiftでダッシュ！ ただしスタミナに注意",
        "スタミナが切れた！壁から手が離れます",
        "遺物を発見！左クリックで掴んで運ぼう。Gキーで丁寧に置けます",
        "遺物を持ったままでは壁を登れません。Gキーで置いてから登りましょう",
        "ロープでチームメイトとつながろう！近づいてEキーで接続",
        "Fキーでマーカーを設置！チームに危険やルートを知らせよう",
        "ベースキャンプに戻るか、フレアガンでヘリを呼ぼう（上空に向けて発射！）",
    };

    [Header("UI")]
    [SerializeField] private GameObject      _hintRoot;
    [SerializeField] private TextMeshProUGUI _hintText;

    [Header("表示設定")]
    [SerializeField] private float _displayDuration = 5f;
    [SerializeField] private float _fadeDuration    = 0.5f;

    private bool _isShowing;
    private static readonly System.Collections.Generic.HashSet<string> s_shownLocalizedHints = new();

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        GameServices.Register((IHintService)this);
        EnsureUi();
        if (_hintRoot != null) _hintRoot.SetActive(false);
    }

    public void TriggerLocalizedHint(string localizationKey)
    {
        if (string.IsNullOrEmpty(localizationKey)) return;

        var save = GameServices.Save;
        if (save != null && !save.IsTutorialHintsEnabled()) return;
        if (!s_shownLocalizedHints.Add(localizationKey)) return;

        string text = LocalizedText.Get(localizationKey, LocalizationKeys.TableHint);
        if (string.IsNullOrEmpty(text) || text == localizationKey)
            return;

        StartCoroutine(ShowHintCoroutine(text));
    }

    public void TriggerHint(int hintId)
    {
        Debug.Assert(hintId >= 1 && hintId < HINT_TEXTS.Length,
            $"[Contract] HintManager.TriggerHint: hintId 範囲外 ({hintId})");
        if (!ShouldShow(hintId)) return;

        GameServices.Save?.AddSeenHint(hintId);

        string text = ResolveHintText(hintId);
        if (string.IsNullOrEmpty(text)) return;

        StartCoroutine(ShowHintCoroutine(text));
    }

    private static string ResolveHintText(int hintId)
    {
        string key = hintId switch
        {
            HintId.FirstClimbApproach => LocalizationKeys.HintContextFirstClimb,
            HintId.DashIntroduction   => LocalizationKeys.HintContextDashIntro,
            HintId.StaminaDepleted      => LocalizationKeys.HintContextStaminaEmpty,
            HintId.RelicApproach        => LocalizationKeys.HintContextRelicApproach,
            HintId.RelicWithClimb       => LocalizationKeys.HintContextRelicClimb,
            HintId.RopePlayerNearby     => LocalizationKeys.HintContextRopeNearby,
            HintId.Zone2Entry           => LocalizationKeys.HintContextPinMarker,
            HintId.ReturnOrZone3        => LocalizationKeys.HintContextReturnHeli,
            _                         => null,
        };

        if (!string.IsNullOrEmpty(key))
        {
            string localized = LocalizedText.Get(key, LocalizationKeys.TableHint);
            if (!string.IsNullOrEmpty(localized) && localized != key)
                return localized;
        }

        return hintId >= 1 && hintId < HINT_TEXTS.Length ? HINT_TEXTS[hintId] : string.Empty;
    }

    private bool ShouldShow(int hintId)
    {
        var save = GameServices.Save;
        if (save != null && !save.IsTutorialHintsEnabled()) return false;
        if (save != null && save.HasSeenHint(hintId)) return false;
        return hintId >= 1 && hintId < HINT_TEXTS.Length;
    }

    private IEnumerator ShowHintCoroutine(string text)
    {
        while (_isShowing) yield return null;

        _isShowing = true;
        EnsureUi();

        if (_hintText != null) _hintText.text = text;
        if (_hintRoot != null) _hintRoot.SetActive(true);

        CanvasGroup canvasGroup = null;
        if (_hintRoot != null)
        {
            canvasGroup = _hintRoot.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = _hintRoot.AddComponent<CanvasGroup>();
        }

        yield return Fade(canvasGroup, 0f, 1f, _fadeDuration);
        yield return new WaitForSeconds(_displayDuration);
        yield return Fade(canvasGroup, 1f, 0f, _fadeDuration);

        if (_hintRoot != null) _hintRoot.SetActive(false);
        _isShowing = false;
    }

    private static IEnumerator Fade(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null) yield break;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        cg.alpha = to;
    }

    public void ForceShowHint(int hintId)
    {
        if (hintId < 1 || hintId >= HINT_TEXTS.Length) return;
        StopAllCoroutines();
        _isShowing = false;
        StartCoroutine(ShowHintCoroutine(ResolveHintText(hintId)));
    }

    private void EnsureUi()
    {
        if (_hintRoot != null && _hintText != null) return;

        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var canvasGo = new GameObject("HintCanvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        if (_hintRoot == null)
        {
            _hintRoot = new GameObject("HintPanel");
            _hintRoot.transform.SetParent(canvas.transform, false);

            var rect = _hintRoot.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot     = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 80f);
            rect.sizeDelta = new Vector2(720f, 64f);

            var bg = _hintRoot.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.65f);
            _hintRoot.AddComponent<CanvasGroup>();
        }

        if (_hintText == null)
        {
            var textGo = new GameObject("HintText");
            textGo.transform.SetParent(_hintRoot.transform, false);

            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 8f);
            textRect.offsetMax = new Vector2(-16f, -8f);

            _hintText = textGo.AddComponent<TextMeshProUGUI>();
            _hintText.fontSize = 22f;
            _hintText.alignment = TextAlignmentOptions.Center;
            _hintText.color = Color.white;
        }
    }

    public static int StepCount => HINT_TEXTS.Length - 1;

    public static string GetStepText(int index)
    {
        int hintId = index + 1;
        return ResolveHintText(hintId);
    }
}
