using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

namespace Sandbox.UI
{
    /// <summary>
    /// R.E.P.O. 風ベースキャンプ準備シーン + PEAK 天気ボード。
    /// </summary>
    public sealed class ShopSceneController : MonoBehaviour
    {
        [SerializeField] private string _header = "BASE CAMP — EXPEDITION PREP";

        private void Awake()
        {
            MenuSceneBootstrap.EnsureForActiveScene(transform);
        }

        private void Start()
        {
            EnsureCamera();
            EnsureEventSystem();
            ShopSceneDiegeticSet.Ensure(transform);
            GameplayCursorPolicy.SetMenuMode();
            BuildOverlay();
            EnsureShop();
            EnsureShopTutorial();
        }

        public void GoToTitleFromShop() => GameFlow.GoToTitle();

        private void EnsureShop()
        {
            var shop = Object.FindFirstObjectByType<BasecampShop>();
            if (shop == null)
            {
                var go = new GameObject("BasecampShop");
                go.transform.SetParent(transform, false);
                shop = go.AddComponent<BasecampShop>();
            }
            shop.ConfigureForStandaloneScene();
        }

        private void EnsureShopTutorial()
        {
            if (Object.FindFirstObjectByType<ShopTutorialOverlay>() != null) return;
            var go = new GameObject("ShopTutorialOverlay");
            go.transform.SetParent(transform, false);
            go.AddComponent<ShopTutorialOverlay>();
        }

        private void BuildOverlay()
        {
            var canvas = MenuUiKit.CreateOverlayCanvas(transform, "ShopScene_Overlay", -10);
            BuildShopOverlay(canvas.transform);

            // 左の商品パネル（幅 760px）に被らないよう、見出し・掲示板は右側へ寄せる。
            MenuUiKit.CreateTitleText(canvas.transform, "Header", _header, 46, new Vector2(0.68f, 0.93f));

            // R.E.P.O. 風掲示板 — 前回遠征 + 天気 + ルート
            var board = FlowUiTheme.CreateTerminalPanel(canvas.transform, "ExpeditionBoard",
                new Vector2(0.70f, 0.81f), new Vector2(0.70f, 0.81f),
                new Vector2(-470f, -104f), new Vector2(470f, 104f));

            string runLine = GameFlow.RunCount > 0
                ? $"RUN #{GameFlow.RunCount + 1}   ·   {GameFlowSessionState.GetLastRunSummaryLine()}"
                : "RUN #1   ·   FIRST DEPARTURE";
            MenuUiKit.CreateBulletinLine(board, "RunSummary", runLine, 26,
                new Vector2(0.5f, 0.74f), UiPalette.Amber, FontStyles.Bold).alignment =
                TextAlignmentOptions.Center;
            MenuUiKit.CreateBulletinLine(board, "WeatherInfo",
                $"WEATHER FORECAST :  {GameFlowSessionState.LastWeatherDisplay}",
                22, new Vector2(0.5f, 0.44f), FlowUiTheme.TerminalAccent).alignment =
                TextAlignmentOptions.Center;
            MenuUiKit.CreateBulletinLine(board, "RouteInfo",
                GameFlowSessionState.LastRouteSummary.ToUpperInvariant(),
                19, new Vector2(0.5f, 0.16f), new Color(UiPalette.Cream.r, UiPalette.Cream.g, UiPalette.Cream.b, 0.92f)).alignment =
                TextAlignmentOptions.Center;

            // フッターのヒント帯
            var hintBar = MenuUiKit.NewRect("HintBar", canvas.transform);
            hintBar.anchorMin = hintBar.anchorMax = new Vector2(0.68f, 0.05f);
            hintBar.sizeDelta = new Vector2(1000f, 46f);
            hintBar.anchoredPosition = Vector2.zero;
            FlowUiTheme.AddSprite(hintBar, UiSprite.RoundedRect(18), new Color(0.05f, 0.05f, 0.07f, 0.55f));
            MenuUiKit.CreateBodyText(hintBar, "Hint",
                "[B] ショップを開く    ·    [出発] 次の遠征へ    ·    予算100ptはチーム共有（R.E.P.O.式）",
                20, new Vector2(0.5f, 0.5f), UiPalette.Cream);

            var topCanvas = MenuUiKit.CreateOverlayCanvas(transform, "ShopScene_TopOverlay", 100);
            var backBtn = MenuUiKit.CreateMenuButton(topCanvas.transform, "BackToTitleButton", "← タイトル",
                new Vector2(0f, 1f), new Vector2(248f, 58f), MenuUiKit.BtnDanger, GoToTitleFromShop);
            var backRt = backBtn.GetComponent<RectTransform>();
            backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 1f);
            backRt.pivot = new Vector2(0f, 1f);
            backRt.anchoredPosition = new Vector2(20f, -20f);
        }

        /// <summary>
        /// 3D 基地を背後に見せる軽いシェード。シアン MonitorGlow は使わず、暖色グロー＋ビネットのみ。
        /// </summary>
        private static void BuildShopOverlay(Transform parent)
        {
            var shade = FlowUiTheme.NewRect("DiegeticShade", parent);
            FlowUiTheme.Stretch(shade);
            // 暗転を弱めて木材・夕景の質感を見せる（沈み込みを抑える）。
            var grad = FlowUiTheme.AddSprite(shade, UiSprite.VerticalGradient3(
                new Color(0.02f, 0.03f, 0.05f, 0.22f),
                new Color(0.02f, 0.03f, 0.05f, 0.0f),
                new Color(0.02f, 0.03f, 0.06f, 0.42f),
                0.5f), Color.white, Image.Type.Simple);
            grad.raycastTarget = false;

            var warmGlow = FlowUiTheme.NewRect("WarmGlow", parent);
            warmGlow.anchorMin = warmGlow.anchorMax = new Vector2(0.5f, 0.88f);
            warmGlow.sizeDelta = new Vector2(1100f, 420f);
            var wg = FlowUiTheme.AddSprite(warmGlow, UiSprite.RadialGlow(2.2f),
                new Color(UiPalette.Amber.r, UiPalette.Amber.g, UiPalette.Amber.b, 0.045f), Image.Type.Simple);
            wg.raycastTarget = false;

            FlowUiTheme.CreateScanlineOverlay(parent, 5, 0.04f);
            FlowUiTheme.CreateVignette(parent, 0.26f);
            FlowUiTheme.CreateGrain(parent, 0.011f);
        }

        private static void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("ShopCamera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                if (go.GetComponent<AudioListener>() == null)
                    go.AddComponent<AudioListener>();
            }

            cam.clearFlags = CameraClearFlags.SolidColor;
            // SkyBackdrop が背後を覆うが、隙間用フォールバックも暖かい夕空色にする
            cam.backgroundColor = new Color(0.40f, 0.25f, 0.27f, 1f);
            // 開放的な屋外キャンプを引きで捉える（カウンター手前・遠景の峰と夕空を背景に）
            cam.transform.position = new Vector3(0f, 2.5f, -3.6f);
            cam.transform.rotation = Quaternion.Euler(6f, 0f, 0f);
            cam.fieldOfView = 56f;
            if (cam.TryGetComponent(out UnityEngine.Rendering.Universal.UniversalAdditionalCameraData camData))
                camData.renderPostProcessing = true;

            // 暖かい夕景の環境光（黒く沈ませない・PEAK の温かみ）
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor    = new Color(0.66f, 0.56f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.44f, 0.42f);
            RenderSettings.ambientGroundColor  = new Color(0.30f, 0.23f, 0.20f);
            // 薄い夕霧で遠景に空気感（峰は霧の手前に置くため end は遠めに）
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.42f, 0.28f, 0.30f);
            RenderSettings.fogStartDistance = 24f;
            RenderSettings.fogEndDistance = 70f;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }
    }
}
