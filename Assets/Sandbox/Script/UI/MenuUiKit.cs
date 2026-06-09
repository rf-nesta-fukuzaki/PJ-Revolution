using PeakPlunder.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sandbox.UI
{
    /// <summary>
    /// ゲームループ UI 共通ビルダー（PEAK / R.E.P.O. / MIMESIS 統合テーマ）。
    /// </summary>
    public static class MenuUiKit
    {
        // UiPalette / FlowUiTheme へのエイリアス（後方互換）
        public static Color BgDark       => UiPalette.Ink;
        public static Color BgGradientTop => FlowUiTheme.PeakSunsetMid;
        public static Color AccentGold   => UiPalette.Amber;
        public static Color AccentTeal   => FlowUiTheme.TerminalAccent;
        public static Color TextMuted    => UiPalette.CreamDim;
        public static Color BtnPrimary   => new Color(0.50f, 0.32f, 0.15f, 0.96f); // PEAK 暖色アンバー
        public static Color BtnSecondary => new Color(0.14f, 0.30f, 0.36f, 0.96f); // 補色の冷たいティール
        public static Color BtnDanger    => new Color(0.45f, 0.16f, 0.14f, 0.95f);
        public static Color BtnNeutral   => new Color(0.18f, 0.19f, 0.22f, 0.95f);

        public static Canvas CreateOverlayCanvas(Transform parent, string name, int sortOrder = 0)
        {
            var canvasGo = new GameObject(name);
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void CreateGradientBackground(Transform parent)
        {
            FlowUiTheme.CreateSceneBackdrop(parent, FlowUiTheme.SceneFlavor.TitlePeak);
        }

        public static void CreateShopBackground(Transform parent)
        {
            FlowUiTheme.CreateSceneBackdrop(parent, FlowUiTheme.SceneFlavor.ShopRepo);
        }

        public static void CreateCoopBackground(Transform parent)
        {
            FlowUiTheme.CreateSceneBackdrop(parent, FlowUiTheme.SceneFlavor.CoopRepo);
        }

        /// <summary>R.E.P.O. 風の情報ボード（天気・ルート・遠征サマリー用）。</summary>
        public static TextMeshProUGUI CreateBulletinLine(Transform parent, string name, string text,
            int fontSize, Vector2 anchor, Color? color = null, FontStyles style = FontStyles.Normal)
        {
            var tmp = CreateText(parent, name, text, fontSize, anchor);
            tmp.color = color ?? UiPalette.Cream;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.rectTransform.sizeDelta = new Vector2(820f, fontSize * 1.8f);
            tmp.rectTransform.anchoredPosition = Vector2.zero;
            FlowUiTheme.StyleReadable(tmp, 0.14f);
            return tmp;
        }

        public static TextMeshProUGUI CreateTitleText(Transform parent, string name, string text,
            int size, Vector2 anchor, Color? color = null)
        {
            var tmp = CreateText(parent, name, text, size, anchor);
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = color ?? UiPalette.Amber;
            tmp.characterSpacing = 4f;
            FlowUiTheme.StyleReadable(tmp, 0.22f);
            return tmp;
        }

        public static TextMeshProUGUI CreateBodyText(Transform parent, string name, string text,
            int size, Vector2 anchor, Color? color = null)
        {
            var tmp = CreateText(parent, name, text, size, anchor);
            tmp.color = color ?? UiPalette.CreamDim;
            FlowUiTheme.StyleReadable(tmp, 0.12f);
            return tmp;
        }

        /// <summary>PEAK/R.E.P.O. 融合ボタン — 太枠+暗面+ホバー SE。</summary>
        public static Button CreateMenuButton(Transform parent, string name, string label,
            Vector2 anchor, Vector2 size, Color fillColor, UnityEngine.Events.UnityAction onClick,
            Color? borderColor = null)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;

            // 接地影
            var shadow = NewRect("Shadow", rt);
            Stretch(shadow, -6f);
            shadow.anchoredPosition = new Vector2(0f, -5f);
            FlowUiTheme.AddSprite(shadow, UiSprite.RoundedRect(16), new Color(0f, 0f, 0f, 0.4f));

            // 角丸の面（縦グラデで上を明るく）
            Color topFill = new(Mathf.Min(1f, fillColor.r * 1.35f + 0.04f), Mathf.Min(1f, fillColor.g * 1.35f + 0.04f), Mathf.Min(1f, fillColor.b * 1.35f + 0.04f), fillColor.a);
            var fill = FlowUiTheme.AddSprite(rt, UiSprite.VerticalGradient(fillColor, topFill), Color.white);

            // 発光枠
            var borderRt = NewRect("Border", rt);
            Stretch(borderRt);
            FlowUiTheme.AddSprite(borderRt, UiSprite.RoundedFrame(16, 2), borderColor ?? FlowUiTheme.TerminalBorder);

            // 上端グロスハイライト
            var gloss = NewRect("Gloss", rt);
            gloss.anchorMin = new Vector2(0.06f, 0.52f);
            gloss.anchorMax = new Vector2(0.94f, 0.94f);
            gloss.offsetMin = Vector2.zero; gloss.offsetMax = Vector2.zero;
            FlowUiTheme.AddSprite(gloss, UiSprite.RoundedRect(12), new Color(1f, 1f, 1f, 0.06f));

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = fill;
            var colors = btn.colors;
            colors.normalColor      = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.08f);
            colors.pressedColor     = new Color(0.82f, 0.82f, 0.78f);
            colors.selectedColor    = colors.highlightedColor;
            btn.colors = colors;

            btn.onClick.AddListener(() =>
            {
                GameServices.Audio?.PlaySE2D(SoundId.UiClick);
                onClick?.Invoke();
            });

            var labelRt = NewRect("Text", rt);
            Stretch(labelRt, 8f);
            var tmp = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = Mathf.Clamp(Mathf.RoundToInt(size.y * 0.36f), 18, 36);
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = UiPalette.Cream;
            ApplyFont(tmp);
            FlowUiTheme.StyleReadable(tmp, 0.14f);

            var trigger = rt.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ => GameServices.Audio?.PlaySE2D(SoundId.UiHover, 0.45f));
            trigger.triggers.Add(entry);

            return btn;
        }

        /// <summary>R.E.P.O. 端末風ルームコード表示。</summary>
        public static TextMeshProUGUI CreateTerminalCode(Transform parent, string name, string code,
            Vector2 anchor)
        {
            var panel = FlowUiTheme.CreateTerminalPanel(parent, name + "_Panel",
                anchor, anchor, Vector2.zero, Vector2.zero);
            panel.anchorMin = panel.anchorMax = anchor;
            panel.sizeDelta = new Vector2(480f, 88f);
            panel.anchoredPosition = Vector2.zero;

            var tmp = CreateText(panel, name, code, 36, new Vector2(0.5f, 0.5f));
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = FlowUiTheme.TerminalAccent;
            tmp.characterSpacing = 8f;
            FlowUiTheme.StyleReadable(tmp, 0.18f);
            return tmp;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name, string text,
            int size, Vector2 anchor)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1400f, size * 1.8f);
            rt.anchoredPosition = Vector2.zero;
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = UiPalette.Cream;
            ApplyFont(tmp);
            return tmp;
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            return FlowUiTheme.NewRect(name, parent);
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            FlowUiTheme.Stretch(rt, inset);
        }

        private static void ApplyFont(TextMeshProUGUI tmp)
        {
            EnsureDefaultFont(tmp);
        }

        /// <summary>実行時/エディタ生成 TMP に既定フォントを割り当てる。</summary>
        public static void EnsureDefaultFont(TMP_Text tmp)
        {
            if (tmp is TextMeshProUGUI ugui && ugui.font == null && TMP_Settings.defaultFontAsset != null)
                ugui.font = TMP_Settings.defaultFontAsset;
        }
    }
}
