using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;
using PeakPlunder.Audio;

namespace Sandbox.UI
{
    /// <summary>
    /// ポーズメニューの見た目（オーバーレイ Canvas・カード・ボタン）を実行時に生成し、
    /// 各参照を保持するビュー。外部アセットに依存せず、どのシーンでも同じ見た目を保証する。
    ///
    /// 生成構造:
    ///   PauseMenu_Canvas (Canvas + CanvasScaler + GraphicRaycaster + CanvasGroup)
    ///     ├ Backdrop      (暗転 + クリック吸収)
    ///     ├ MenuCard      (タイトル + 3 ボタン + ヒント)
    ///     └ ConfirmCard   (離脱確認ダイアログ)
    /// </summary>
    public sealed class PauseMenuView
    {
        // ── パレット（FlowUiTheme / UiPalette へ統合） ─────────────
        private static Color Backdrop => new Color(UiPalette.Ink.r, UiPalette.Ink.g, UiPalette.Ink.b, 0.88f);

        // ── 参照 ─────────────────────────────────────────────────
        public GameObject Root { get; private set; }
        public CanvasGroup Group { get; private set; }
        public RectTransform CardTransform { get; private set; }

        public GameObject MenuCard { get; private set; }
        public GameObject ConfirmCard { get; private set; }

        public Button ResumeButton { get; private set; }
        public Button SettingsButton { get; private set; }
        public Button LeaveButton { get; private set; }
        public Button ConfirmYesButton { get; private set; }
        public Button ConfirmNoButton { get; private set; }

        /// <summary>親 Transform 配下にオーバーレイ UI を構築する。</summary>
        public static PauseMenuView Build(Transform parent)
        {
            EnsureEventSystem();

            var view = new PauseMenuView();

            var canvasGo = new GameObject("PauseMenu_Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasGo.transform.SetParent(parent, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000; // HUD より前面

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            view.Root = canvasGo;
            view.Group = canvasGo.GetComponent<CanvasGroup>();

            // PEAK 風の暗い山岳オーバーレイ（没入感 + 可読性）
            var backdropRoot = new GameObject("PauseBackdrop", typeof(RectTransform));
            backdropRoot.transform.SetParent(canvasGo.transform, false);
            Stretch((RectTransform)backdropRoot.transform);
            FlowUiTheme.CreatePauseBackdrop(backdropRoot.transform);

            // 追加の暗転（クリックをブロックして裏のゲームに触らせない）
            var backdrop = NewImage("Backdrop", canvasGo.transform, Backdrop);
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;
            backdrop.transform.SetSiblingIndex(backdropRoot.transform.GetSiblingIndex() + 1);

            view.BuildMenuCard(canvasGo.transform);
            view.BuildConfirmCard(canvasGo.transform);

            return view;
        }

        // ── ルートメニュー ───────────────────────────────────────
        private void BuildMenuCard(Transform canvas)
        {
            var cardRt = FlowUiTheme.CreateTerminalPanel(canvas, "MenuCard",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-250f, -240f), new Vector2(250f, 240f));
            CardTransform = cardRt;
            MenuCard = cardRt.gameObject;

            var title = CreateText("Title", cardRt, "一時停止", 48, new Vector2(0.5f, 0.82f));
            title.fontStyle = FontStyles.Bold;
            title.color = UiPalette.Amber;
            FlowUiTheme.StyleReadable(title, 0.18f);

            var divider = FlowUiTheme.NewRect("TitleDivider", cardRt);
            divider.anchorMin = new Vector2(0.12f, 0.74f);
            divider.anchorMax = new Vector2(0.88f, 0.74f);
            divider.sizeDelta = new Vector2(0f, 2f);
            FlowUiTheme.AddSprite(divider, UiSprite.RoundedRect(2),
                new Color(UiPalette.Amber.r, UiPalette.Amber.g, UiPalette.Amber.b, 0.45f));

            var sub = CreateText("Subtitle", cardRt, "E X P E D I T I O N  P A U S E D", 16, new Vector2(0.5f, 0.66f));
            sub.color = FlowUiTheme.TerminalAccent;
            sub.characterSpacing = 4f;

            var listGo = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup));
            listGo.transform.SetParent(cardRt, false);
            var listRt = (RectTransform)listGo.transform;
            listRt.anchorMin = new Vector2(0.1f, 0.12f);
            listRt.anchorMax = new Vector2(0.9f, 0.62f);
            listRt.offsetMin = listRt.offsetMax = Vector2.zero;

            var layout = listGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;

            ResumeButton   = CreateThemedButton(listGo.transform, "ResumeButton", "ゲームに戻る", MenuUiKit.BtnPrimary);
            SettingsButton = CreateThemedButton(listGo.transform, "SettingsButton", "設定", MenuUiKit.BtnSecondary);
            LeaveButton    = CreateThemedButton(listGo.transform, "LeaveButton", "タイトルへ戻る", MenuUiKit.BtnDanger);

            var hint = CreateText("Hint", cardRt, "Esc：戻る  ·  ↑↓ + Enter：選択", 15, new Vector2(0.5f, 0.06f));
            hint.color = UiPalette.CreamDim;
        }

        private void BuildConfirmCard(Transform canvas)
        {
            var cardRt = FlowUiTheme.CreateTerminalPanel(canvas, "ConfirmCard",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-300f, -150f), new Vector2(300f, 150f));
            ConfirmCard = cardRt.gameObject;

            var warnStripe = FlowUiTheme.NewRect("WarnStripe", cardRt);
            warnStripe.anchorMin = new Vector2(0f, 1f);
            warnStripe.anchorMax = new Vector2(1f, 1f);
            warnStripe.pivot = new Vector2(0.5f, 1f);
            warnStripe.sizeDelta = new Vector2(0f, 6f);
            warnStripe.anchoredPosition = Vector2.zero;
            FlowUiTheme.AddSprite(warnStripe, UiSprite.RoundedRect(3),
                new Color(UiPalette.Amber.r, UiPalette.Amber.g, UiPalette.Amber.b, 0.85f));

            var tag = CreateText("Tag", cardRt, "EXPEDITION ABORT", 14, new Vector2(0.5f, 0.86f));
            tag.color = FlowUiTheme.TerminalAccent;
            tag.characterSpacing = 3f;

            var title = CreateText("Title", cardRt, "タイトルへ戻りますか？", 30, new Vector2(0.5f, 0.66f));
            title.fontStyle = FontStyles.Bold;
            title.color = UiPalette.Cream;
            FlowUiTheme.StyleReadable(title, 0.16f);

            var divider = FlowUiTheme.NewRect("Divider", cardRt);
            divider.anchorMin = new Vector2(0.1f, 0.52f);
            divider.anchorMax = new Vector2(0.9f, 0.52f);
            divider.sizeDelta = new Vector2(0f, 2f);
            FlowUiTheme.AddSprite(divider, UiSprite.RoundedRect(2),
                new Color(FlowUiTheme.TerminalBorder.r, FlowUiTheme.TerminalBorder.g,
                    FlowUiTheme.TerminalBorder.b, 0.55f));

            var msg = CreateText("Message", cardRt,
                "進行中の探索記録は失われます。\n本当に離脱しますか？", 18, new Vector2(0.5f, 0.36f));
            msg.color = UiPalette.CreamDim;
            msg.lineSpacing = -4f;
            msg.rectTransform.sizeDelta = new Vector2(520f, 58f);

            ConfirmYesButton = CreateThemedButton(cardRt, "ConfirmYes", "離脱する", MenuUiKit.BtnDanger);
            SetButtonRect(ConfirmYesButton, new Vector2(0.5f, 0.12f), new Vector2(-118f, 0f), new Vector2(200f, 54f));

            ConfirmNoButton = CreateThemedButton(cardRt, "ConfirmNo", "戻る", MenuUiKit.BtnNeutral);
            SetButtonRect(ConfirmNoButton, new Vector2(0.5f, 0.12f), new Vector2(118f, 0f), new Vector2(200f, 54f));
        }

        private static Button CreateThemedButton(Transform parent, string name, string label, Color fill)
        {
            var btn = MenuUiKit.CreateMenuButton(parent, name, label,
                new Vector2(0.5f, 0.5f), new Vector2(360f, 58f), fill, null);
            var le = btn.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 58f;
            le.minHeight = 58f;
            if (btn.gameObject.GetComponent<UiHoverSfx>() == null)
                btn.gameObject.AddComponent<UiHoverSfx>();
            return btn;
        }

        // ── 生成ヘルパー ─────────────────────────────────────────
        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string text, int size, Vector2 anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400f, size * 1.6f);
            rt.anchoredPosition = Vector2.zero;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = UiPalette.Cream;
            tmp.raycastTarget = false;
            if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            FlowUiTheme.StyleReadable(tmp, 0.12f);
            return tmp;
        }

        // legacy CreateButton — MenuUiKit へ移行済み。互換のため残す。
        private static Button CreateButton(string name, Transform parent, string label, Color baseColor)
        {
            return CreateThemedButton(parent, name, label, baseColor);
        }

        private static void SetButtonRect(Button button, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
        {
            var rt = (RectTransform)button.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            var le = button.GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = true;
        }

        private static void EnsureEventSystem()
        {
        if (EventSystem.current != null) return;
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;

        // シーンに EventSystem が無い場合のフォールバック（シーンローカル生成）。
        // DontDestroyOnLoad はシーン遷移時の二重 EventSystem を招くため付けない。
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }

    /// <summary>
    /// ボタンにマウスホバー／ゲームパッド選択が当たった瞬間に UI ホバー SE を鳴らす補助。
    /// ポーズメニューのボタンへ自動付与する（GDD §15.2 ui_hover）。
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public sealed class UiHoverSfx : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        public void OnPointerEnter(PointerEventData _) => Play();
        public void OnSelect(BaseEventData _) => Play();

        private void Play()
        {
            var selectable = GetComponent<Selectable>();
            if (selectable == null || !selectable.interactable) return;
            GameServices.Audio?.PlaySE2D(SoundId.UiHover);
        }
    }
}
