using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

namespace Sandbox.UI
{
    /// <summary>
    /// 独立した「ショップシーン（ベースキャンプ準備）」をランタイムで構築するコントローラ。
    /// シーンにはこのコンポーネントを 1 つ置くだけでよい（外部アセット不要）。
    ///
    /// 役割:
    ///   - カメラ / AudioListener / EventSystem を保証
    ///   - <see cref="BasecampShop"/> を「独立シーンモード」で生成し、開いた状態で表示
    ///       「出発」 → 購入内容を <see cref="RunLoadout"/> に記録して <see cref="GameFlow.GoToInGame"/>
    ///   - 常時表示の「タイトルへ戻る」ボタンを用意（<see cref="GameFlow.GoToTitle"/>）
    /// </summary>
    public sealed class ShopSceneController : MonoBehaviour
    {
        [SerializeField] private string _header = "ベースキャンプ — 出発準備";

        private void Start()
        {
            EnsureCamera();
            EnsureEventSystem();
            GameplayCursorPolicy.SetMenuMode();

            BuildOverlay();
            EnsureShop();
        }

        private void EnsureShop()
        {
            var shop = Object.FindFirstObjectByType<BasecampShop>();
            if (shop == null)
            {
                var go = new GameObject("BasecampShop");
                go.transform.SetParent(transform, false);
                shop = go.AddComponent<BasecampShop>();
            }

            // Awake は AddComponent 時に同期実行されるが、Start はまだ。
            // ここで独立シーンモード（出発＝次の遠征へ）と開いた状態を設定しておく。
            shop.ConfigureForStandaloneScene();
        }

        private void BuildOverlay()
        {
            var canvasGo = new GameObject("ShopScene_Overlay");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -10; // ショップ UI より背面（背景・ヘッダ用）
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            var bg = NewRect("BG", canvas.transform);
            Stretch(bg);
            bg.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.09f, 0.13f, 1f);

            var header = CreateText("Header", canvas.transform, _header, 48, new Vector2(0.5f, 0.92f));
            header.fontStyle = FontStyles.Bold;
            header.color = new Color(1f, 0.92f, 0.6f);

            CreateText("Hint", canvas.transform,
                "B: ショップ開閉 / 「出発」で遠征へ", 24, new Vector2(0.5f, 0.05f));

            // 常時表示の「タイトルへ戻る」ボタン（最前面オーバーレイ）
            var topCanvasGo = new GameObject("ShopScene_TopOverlay");
            topCanvasGo.transform.SetParent(transform, false);
            var topCanvas = topCanvasGo.AddComponent<Canvas>();
            topCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            topCanvas.sortingOrder = 100;
            var topScaler = topCanvasGo.AddComponent<CanvasScaler>();
            topScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            topScaler.referenceResolution = new Vector2(1920f, 1080f);
            topCanvasGo.AddComponent<GraphicRaycaster>();

            CreateButton("BackToTitleButton", topCanvas.transform, "タイトルへ戻る",
                new Vector2(0f, 1f), new Vector2(150f, -54f), new Vector2(240f, 64f),
                new Color(0.45f, 0.22f, 0.22f), GameFlow.GoToTitle);
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null) return;
            var go = new GameObject("ShopCamera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.07f, 0.11f, 1f);
            if (go.GetComponent<AudioListener>() == null)
                go.AddComponent<AudioListener>();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        // ── UI helpers ──
        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string text, int size, Vector2 anchor)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1600f, size * 1.6f);
            rt.anchoredPosition = Vector2.zero;
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size; tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            return tmp;
        }

        private static void CreateButton(string name, Transform parent, string label,
            Vector2 anchor, Vector2 anchoredPos, Vector2 size, Color color,
            UnityEngine.Events.UnityAction onClick)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            var tRt = NewRect("Text", rt);
            Stretch(tRt);
            var tmp = tRt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = 28; tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
            if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
        }
    }
}
