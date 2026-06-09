using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sandbox.UI
{
    /// <summary>
    /// シーン遷移中の TIPS + プログレス表示（MIMESIS 風スキャンライン + PEAK 可読 TIPS）。
    /// </summary>
    public sealed class LoadingTipsOverlay : MonoBehaviour
    {
        private static LoadingTipsOverlay _instance;

        public static LoadingTipsOverlay Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var iris = IrisTransition.Instance;
                if (iris == null) return null;
                _instance = iris.GetComponent<LoadingTipsOverlay>();
                if (_instance == null)
                    _instance = iris.gameObject.AddComponent<LoadingTipsOverlay>();
                return _instance;
            }
        }

        private GameObject _root;
        private TextMeshProUGUI _headerLabel;
        private TextMeshProUGUI _tipsTagLabel;
        private TextMeshProUGUI _tipLabel;
        private TextMeshProUGUI _percentLabel;
        private Image _progressFill;
        private Image _progressGlow;
        private Coroutine _pulseRoutine;
        private Coroutine _glowRoutine;
        private LoadingTipTypewriter _typewriter;
        private float _displayProgress;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;
            BuildUiIfMissing();
            HideImmediate();
        }

        public void Show(string tipText)
        {
            BuildUiIfMissing();
            if (_root == null) return;

            if (_headerLabel != null)
            {
                _headerLabel.text = "EXPEDITION LOADING";
                _headerLabel.color = FlowUiTheme.MimicTeal;
            }
            if (_tipsTagLabel != null)
                _tipsTagLabel.text = "T I P S";
            SetProgress(0f);
            _root.SetActive(true);

            if (_tipLabel != null)
            {
                _tipLabel.text = string.Empty;
                _typewriter?.Show(tipText);
            }

            if (_pulseRoutine != null) StopCoroutine(_pulseRoutine);
            _pulseRoutine = StartCoroutine(PulseHeader());

            if (_glowRoutine != null) StopCoroutine(_glowRoutine);
            _glowRoutine = StartCoroutine(PulseProgressGlow());
        }

        public void SetProgress(float t)
        {
            _displayProgress = Mathf.Clamp01(t);
            if (_progressFill != null)
                _progressFill.fillAmount = _displayProgress;
            if (_percentLabel != null)
                _percentLabel.text = $"{Mathf.RoundToInt(_displayProgress * 100f)}%";
        }

        public void Hide()
        {
            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
                _pulseRoutine = null;
            }
            if (_glowRoutine != null)
            {
                StopCoroutine(_glowRoutine);
                _glowRoutine = null;
            }
            _typewriter?.Hide();
            HideImmediate();
        }

        private void HideImmediate()
        {
            if (_root != null) _root.SetActive(false);
        }

        private IEnumerator PulseHeader()
        {
            if (_headerLabel == null) yield break;
            var baseColor = FlowUiTheme.MimicTeal;
            while (_root != null && _root.activeSelf)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3.5f);
                _headerLabel.color = new Color(baseColor.r, baseColor.g, baseColor.b, pulse);
                yield return null;
            }
        }

        private IEnumerator PulseProgressGlow()
        {
            if (_progressGlow == null) yield break;
            var baseColor = FlowUiTheme.TerminalAccent;
            while (_root != null && _root.activeSelf)
            {
                float pulse = 0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
                _progressGlow.color = new Color(baseColor.r, baseColor.g, baseColor.b, pulse);
                yield return null;
            }
        }

        private void BuildUiIfMissing()
        {
            if (_root != null) return;

            var canvas = GetComponentInChildren<Canvas>();
            if (canvas == null) return;

            _root = new GameObject("LoadingTipsOverlay");
            _root.transform.SetParent(canvas.transform, false);
            var rt = _root.AddComponent<RectTransform>();
            FlowUiTheme.Stretch(rt);

            FlowUiTheme.CreateSceneBackdrop(_root.transform, FlowUiTheme.SceneFlavor.LoadingMimesis);

            // 中央 TIPS パネル（R.E.P.O. 端末枠 + PEAK テキスト）
            var panel = FlowUiTheme.CreateTerminalPanel(_root.transform, "TipPanel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-480f, -100f), new Vector2(480f, 100f));

            _headerLabel = CreateLabel(panel, "LoadingHeader", "EXPEDITION LOADING",
                28, new Vector2(0.5f, 0.78f), FlowUiTheme.MimicTeal, FontStyles.Bold);
            _tipsTagLabel = CreateLabel(panel, "TipsTag", "T I P S",
                13, new Vector2(0.5f, 0.58f), FlowUiTheme.TerminalAccent, FontStyles.Bold);
            _tipsTagLabel.characterSpacing = 8f;
            _tipLabel = CreateLabel(panel, "TipBody", "",
                22, new Vector2(0.5f, 0.32f), UiPalette.CreamDim, FontStyles.Italic);
            _tipLabel.alignment = TextAlignmentOptions.Center;
            _tipLabel.rectTransform.sizeDelta = new Vector2(860f, 96f);
            _tipLabel.textWrappingMode = TextWrappingModes.Normal;

            // 下部プログレスバー（角丸トラック + グロー）
            var barBg = FlowUiTheme.NewRect("ProgressBG", _root.transform);
            barBg.anchorMin = new Vector2(0.12f, 0.11f);
            barBg.anchorMax = new Vector2(0.88f, 0.11f);
            barBg.sizeDelta = new Vector2(0f, 18f);
            FlowUiTheme.AddSprite(barBg, UiSprite.RoundedRect(10), FlowUiTheme.TerminalBorder);

            var barInner = FlowUiTheme.NewRect("ProgressTrack", barBg);
            FlowUiTheme.Stretch(barInner, 3f);
            FlowUiTheme.AddSprite(barInner, UiSprite.RoundedRect(8), UiPalette.Track);

            var fillGo = FlowUiTheme.NewRect("ProgressFill", barInner);
            var fillRt = fillGo;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            _progressFill = fillGo.gameObject.AddComponent<Image>();
            _progressFill.sprite = UiSprite.RoundedRect(8);
            _progressFill.type = Image.Type.Filled;
            _progressFill.fillMethod = Image.FillMethod.Horizontal;
            _progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _progressFill.color = FlowUiTheme.TerminalAccent;
            _progressFill.fillAmount = 0f;

            var glowGo = FlowUiTheme.NewRect("ProgressGlow", barInner);
            glowGo.anchorMin = new Vector2(0f, 0f);
            glowGo.anchorMax = new Vector2(0.08f, 1f);
            glowGo.offsetMin = glowGo.offsetMax = Vector2.zero;
            _progressGlow = glowGo.gameObject.AddComponent<Image>();
            _progressGlow.sprite = UiSprite.RadialGlow(1.8f);
            _progressGlow.color = new Color(FlowUiTheme.TerminalAccent.r, FlowUiTheme.TerminalAccent.g,
                FlowUiTheme.TerminalAccent.b, 0.45f);

            _percentLabel = CreateLabel(_root.transform, "ProgressPercent", "0%",
                18, new Vector2(0.88f, 0.11f), UiPalette.Cream, FontStyles.Bold);
            _percentLabel.alignment = TextAlignmentOptions.Right;
            _percentLabel.rectTransform.sizeDelta = new Vector2(80f, 28f);
            _percentLabel.rectTransform.anchoredPosition = new Vector2(-8f, 0f);

            var hint = CreateLabel(_root.transform, "LoadingHint", "チーム全員の準備が整うまでお待ちください…",
                16, new Vector2(0.5f, 0.06f), UiPalette.CreamDim, FontStyles.Normal);
            hint.alignment = TextAlignmentOptions.Center;

            _typewriter = GetComponent<LoadingTipTypewriter>();
            if (_typewriter == null)
                _typewriter = gameObject.AddComponent<LoadingTipTypewriter>();
            _typewriter.Bind(_tipLabel);
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text,
            int size, Vector2 anchor, Color color, FontStyles style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(900f, size * 2.2f);
            rt.anchoredPosition = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            FlowUiTheme.StyleReadable(tmp, 0.14f);
            return tmp;
        }
    }
}
