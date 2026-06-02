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
        // ── パレット ─────────────────────────────────────────────
        private static readonly Color Backdrop   = new(0.04f, 0.05f, 0.08f, 0.82f);
        private static readonly Color CardBg      = new(0.10f, 0.12f, 0.17f, 0.98f);
        private static readonly Color ConfirmBg   = new(0.08f, 0.09f, 0.13f, 0.99f);
        private static readonly Color Accent      = new(0.96f, 0.80f, 0.38f, 1f);
        private static readonly Color TextMain    = new(0.96f, 0.97f, 1f, 1f);
        private static readonly Color TextDim     = new(0.62f, 0.68f, 0.78f, 1f);

        private static readonly Color BtnPrimary  = new(0.18f, 0.46f, 0.42f, 1f);
        private static readonly Color BtnNeutral  = new(0.18f, 0.22f, 0.31f, 1f);
        private static readonly Color BtnDanger   = new(0.46f, 0.20f, 0.22f, 1f);

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

            // 背景の暗転（クリックをブロックして裏のゲームに触らせない）
            var backdrop = NewImage("Backdrop", canvasGo.transform, Backdrop);
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;

            view.BuildMenuCard(canvasGo.transform);
            view.BuildConfirmCard(canvasGo.transform);

            return view;
        }

        // ── ルートメニュー ───────────────────────────────────────
        private void BuildMenuCard(Transform canvas)
        {
            var card = NewImage("MenuCard", canvas, CardBg);
            CardTransform = card.rectTransform;
            CardTransform.anchorMin = CardTransform.anchorMax = new Vector2(0.5f, 0.5f);
            CardTransform.pivot = new Vector2(0.5f, 0.5f);
            CardTransform.sizeDelta = new Vector2(480f, 460f);
            CardTransform.anchoredPosition = Vector2.zero;
            MenuCard = card.gameObject;

            // アクセントバー
            var accent = NewImage("Accent", card.transform, Accent);
            accent.rectTransform.anchorMin = new Vector2(0f, 1f);
            accent.rectTransform.anchorMax = new Vector2(1f, 1f);
            accent.rectTransform.pivot = new Vector2(0.5f, 1f);
            accent.rectTransform.sizeDelta = new Vector2(0f, 6f);
            accent.rectTransform.anchoredPosition = Vector2.zero;

            var title = CreateText("Title", card.transform, "一時停止", 52, new Vector2(0.5f, 1f));
            title.fontStyle = FontStyles.Bold;
            title.color = Accent;
            title.rectTransform.sizeDelta = new Vector2(440f, 70f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -54f);

            var sub = CreateText("Subtitle", card.transform, "P A U S E D", 18, new Vector2(0.5f, 1f));
            sub.color = TextDim;
            sub.characterSpacing = 6f;
            sub.rectTransform.sizeDelta = new Vector2(440f, 26f);
            sub.rectTransform.anchoredPosition = new Vector2(0f, -104f);

            // ボタン列（VerticalLayoutGroup で等間隔）
            var listGo = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup));
            listGo.transform.SetParent(card.transform, false);
            var listRt = (RectTransform)listGo.transform;
            listRt.anchorMin = new Vector2(0.5f, 0f);
            listRt.anchorMax = new Vector2(0.5f, 1f);
            listRt.pivot = new Vector2(0.5f, 0.5f);
            listRt.sizeDelta = new Vector2(360f, 0f);
            listRt.anchoredPosition = new Vector2(0f, -16f);
            listRt.offsetMin = new Vector2(listRt.offsetMin.x, 70f);
            listRt.offsetMax = new Vector2(listRt.offsetMax.x, -150f);

            var layout = listGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ResumeButton   = CreateButton("ResumeButton",   listGo.transform, "ゲームに戻る",   BtnPrimary);
            SettingsButton = CreateButton("SettingsButton", listGo.transform, "設定",           BtnNeutral);
            LeaveButton    = CreateButton("LeaveButton",    listGo.transform, "タイトルへ戻る", BtnDanger);

            var hint = CreateText("Hint", card.transform,
                "Esc：戻る  ・  ↑↓ + Enter：選択", 16, new Vector2(0.5f, 0f));
            hint.color = TextDim;
            hint.rectTransform.sizeDelta = new Vector2(440f, 24f);
            hint.rectTransform.anchoredPosition = new Vector2(0f, 22f);
        }

        // ── 離脱確認 ─────────────────────────────────────────────
        private void BuildConfirmCard(Transform canvas)
        {
            var card = NewImage("ConfirmCard", canvas, ConfirmBg);
            var rt = card.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(520f, 240f);
            rt.anchoredPosition = Vector2.zero;
            ConfirmCard = card.gameObject;

            var accent = NewImage("Accent", card.transform, BtnDanger);
            accent.rectTransform.anchorMin = new Vector2(0f, 1f);
            accent.rectTransform.anchorMax = new Vector2(1f, 1f);
            accent.rectTransform.pivot = new Vector2(0.5f, 1f);
            accent.rectTransform.sizeDelta = new Vector2(0f, 6f);
            accent.rectTransform.anchoredPosition = Vector2.zero;

            var title = CreateText("Title", card.transform, "タイトルへ戻りますか？", 30, new Vector2(0.5f, 1f));
            title.fontStyle = FontStyles.Bold;
            title.color = TextMain;
            title.rectTransform.sizeDelta = new Vector2(480f, 44f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -44f);

            var msg = CreateText("Message", card.transform,
                "進行中の探索の記録は失われます。", 18, new Vector2(0.5f, 1f));
            msg.color = TextDim;
            msg.rectTransform.sizeDelta = new Vector2(480f, 40f);
            msg.rectTransform.anchoredPosition = new Vector2(0f, -96f);

            // 安全側（いいえ）を右に配置して初期フォーカスにする。
            ConfirmYesButton = CreateButton("ConfirmYes", card.transform, "離脱する", BtnDanger);
            SetButtonRect(ConfirmYesButton, new Vector2(0.5f, 0f), new Vector2(-110f, 44f), new Vector2(190f, 64f));

            ConfirmNoButton = CreateButton("ConfirmNo", card.transform, "戻る", BtnNeutral);
            SetButtonRect(ConfirmNoButton, new Vector2(0.5f, 0f), new Vector2(110f, 44f), new Vector2(190f, 64f));
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
            tmp.color = TextMain;
            tmp.raycastTarget = false;
            if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            return tmp;
        }

        private static Button CreateButton(string name, Transform parent, string label, Color baseColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(360f, 64f);

            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = 64f;
            le.minHeight = 64f;

            var img = go.GetComponent<Image>();
            img.color = baseColor;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor      = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.pressedColor     = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor    = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.disabledColor    = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colors.fadeDuration     = 0.08f;
            colors.colorMultiplier  = 1f;
            btn.colors = colors;

            go.AddComponent<UiHoverSfx>();

            var labelTmp = CreateText("Label", go.transform, label, 28, new Vector2(0.5f, 0.5f));
            labelTmp.fontStyle = FontStyles.Bold;
            Stretch(labelTmp.rectTransform);

            return btn;
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
