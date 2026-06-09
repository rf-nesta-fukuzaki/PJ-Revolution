using System;
using System.Reflection;
using Sandbox.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ResultScreen の Inspector 未配線を実行時/エディタ時に補完する。
/// 旧シーン（ラベルが Panel 直下）も段階コンテナへ再配置して GDD 6 段階演出を有効化する。
/// </summary>
public static class ResultScreenRuntimeBuilder
{
    private static readonly Color RowBg = new(0.06f, 0.08f, 0.11f, 0.85f);

    private sealed class WiringResult
    {
        public GameObject Panel;
        public TextMeshProUGUI TeamScoreLabel;
        public TextMeshProUGUI RelicSummaryLabel;
        public TextMeshProUGUI ClearTimeLabel;
        public Transform PlayerRowParent;
        public GameObject PlayerRowPrefab;
        public Transform TitleRowParent;
        public GameObject TitleRowPrefab;
        public GameObject CosmeticGroup;
        public TextMeshProUGUI CosmeticLabel;
        public Button RetryButton;
        public Button ReturnBaseButton;
        public GameObject StageTeamScore;
        public GameObject StageRelics;
        public GameObject StagePlayers;
        public GameObject StageTitles;
        public GameObject StageCosmetic;
        public GameObject StageButtons;
    }

    /// <summary>不足している参照を補完する。既存の配線は可能な限り維持する。</summary>
    public static void EnsureStructure(ResultScreen screen)
    {
        if (screen == null) return;

        var wiring = BuildWiring(screen);
        ApplyWiring(screen, wiring);
        NormalizeResultLayout(wiring);
        Debug.Log("[ResultBuilder] EnsureStructure v3 applied (fontSize/rows)");
    }

    /// <summary>
    /// 段階要素を縦の別バンドへ正規化する。段階は累積表示（チームスコア→遺物→個人→称号が順に重なって残る）
    /// のため、同じ中央に置くと重なる。情報ブロックは上、個人リストは中、称号リストは下に分離する。
    /// </summary>
    private static void NormalizeResultLayout(WiringResult w)
    {
        // ステージコンテナを全画面 stretch に正規化する。素 Transform 配下で生成されたシーン由来の
        // 崩れたアンカー/オフセットが残ると、配下ラベルが中央からずれて見えるため。
        StretchStage(w.StageTeamScore);
        StretchStage(w.StageRelics);
        StretchStage(w.StagePlayers);
        StretchStage(w.StageTitles);
        StretchStage(w.StageCosmetic);
        StretchStage(w.StageButtons);

        PlaceTopCentered(w.TeamScoreLabel, new Vector2(0f, -152f), new Vector2(1100f, 80f), 56);
        PlaceTopCentered(w.RelicSummaryLabel, new Vector2(0f, -238f), new Vector2(1100f, 44f), 30);
        PlaceTopCentered(w.ClearTimeLabel, new Vector2(0f, -286f), new Vector2(1100f, 40f), 26);

        PlaceVerticalBand(w.PlayerRowParent as RectTransform, 0.44f, 0.66f);
        // 称号は最大8件超になり得るため、下半分を広く確保して溢れを防ぐ。
        PlaceVerticalBand(w.TitleRowParent as RectTransform, 0.05f, 0.41f);

        // 配線済みの行コンテナにも効くよう、ここで VLG の幅制御を保証する。
        // childControlWidth=false のままだと行が幅0に潰れて左端に重なって見えるため。
        ConfigureRowList(w.PlayerRowParent, 8f);
        ConfigureRowList(w.TitleRowParent, 6f);
    }

    private static void ConfigureRowList(Transform parent, float spacing)
    {
        if (parent == null) return;
        var vlg = parent.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) return;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = spacing;
    }

    private static void StretchStage(GameObject stage)
    {
        if (stage == null || !(stage.transform is RectTransform rt)) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.anchoredPosition = Vector2.zero;
    }

    private static void PlaceTopCentered(TextMeshProUGUI label, Vector2 pos, Vector2 size, int fontSize)
    {
        if (label == null) return;
        var rt = label.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        label.enableAutoSizing = false;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
    }

    private static void PlaceVerticalBand(RectTransform rt, float anchorMinY, float anchorMaxY)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0.12f, anchorMinY);
        rt.anchorMax = new Vector2(0.88f, anchorMaxY);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

#if UNITY_EDITOR
    /// <summary>エディタ上で配線を構築し SerializedObject 経由でシーンに保存する。</summary>
    public static void EnsureStructureAndSave(ResultScreen screen)
    {
        if (screen == null) return;
        var wiring = BuildWiring(screen);
        ApplyWiring(screen, wiring);

        var so = new SerializedObject(screen);
        SetProp(so, "_panel",             wiring.Panel);
        SetProp(so, "_teamScoreLabel",    wiring.TeamScoreLabel);
        SetProp(so, "_relicSummaryLabel", wiring.RelicSummaryLabel);
        SetProp(so, "_clearTimeLabel",    wiring.ClearTimeLabel);
        SetProp(so, "_playerRowParent",   wiring.PlayerRowParent);
        SetProp(so, "_playerRowPrefab",   wiring.PlayerRowPrefab);
        SetProp(so, "_titleRowParent",    wiring.TitleRowParent);
        SetProp(so, "_titleRowPrefab",    wiring.TitleRowPrefab);
        SetProp(so, "_cosmeticGroup",     wiring.CosmeticGroup);
        SetProp(so, "_cosmeticLabel",     wiring.CosmeticLabel);
        SetProp(so, "_retryButton",       wiring.RetryButton);
        SetProp(so, "_returnBaseButton",  wiring.ReturnBaseButton);
        SetProp(so, "_stageTeamScore",    wiring.StageTeamScore);
        SetProp(so, "_stageRelics",       wiring.StageRelics);
        SetProp(so, "_stagePlayers",      wiring.StagePlayers);
        SetProp(so, "_stageTitles",       wiring.StageTitles);
        SetProp(so, "_stageCosmetic",     wiring.StageCosmetic);
        SetProp(so, "_stageButtons",      wiring.StageButtons);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(screen);
    }

    private static void SetProp(SerializedObject so, string name, UnityEngine.Object value)
    {
        var prop = so.FindProperty(name);
        if (prop != null) prop.objectReferenceValue = value;
    }
#endif

    private static WiringResult BuildWiring(ResultScreen screen)
    {
        var w = new WiringResult();

        w.Panel = SafeGetField<GameObject>(screen, "_panel");
        if (w.Panel == null)
        {
            w.Panel = CreateFullScreenPanel(screen.transform);
        }

        EnsureCanvasGroup(w.Panel);
        PolishPanelVisuals(w.Panel);
        var panelT = w.Panel.transform;

        w.StageTeamScore = ResolveStage(panelT, "StageTeamScore");
        w.StageRelics    = ResolveStage(panelT, "StageRelics");
        w.StagePlayers   = ResolveStage(panelT, "StagePlayers");
        w.StageTitles    = ResolveStage(panelT, "StageTitles");
        w.StageCosmetic  = ResolveStage(panelT, "StageCosmetic");
        w.StageButtons   = ResolveStage(panelT, "StageButtons");

        w.TeamScoreLabel = SafeGetField<TextMeshProUGUI>(screen, "_teamScoreLabel")
                           ?? FindOrCreateLabel(w.StageTeamScore.transform, "TeamScoreLabel",
                               "EXPEDITION SCORE: 0 pt", 36, new Vector2(0.5f, 0.5f), Vector2.zero, UiPalette.Amber);
        w.RelicSummaryLabel = SafeGetField<TextMeshProUGUI>(screen, "_relicSummaryLabel")
                              ?? FindOrCreateLabel(w.StageRelics.transform, "RelicSummaryLabel",
                                  "遺物 0/0 個 無事帰還", 22, new Vector2(0.5f, 0.6f), Vector2.zero, UiPalette.Cream);
        w.ClearTimeLabel = SafeGetField<TextMeshProUGUI>(screen, "_clearTimeLabel")
                           ?? FindOrCreateLabel(w.StageRelics.transform, "ClearTimeLabel",
                               "タイム: 00:00.00", 20, new Vector2(0.5f, 0.4f), Vector2.zero, UiPalette.CreamDim);

        ReparentIfExists(panelT, "TeamScoreLabel",    w.StageTeamScore.transform);
        ReparentIfExists(panelT, "RelicSummaryLabel", w.StageRelics.transform);
        ReparentIfExists(panelT, "ClearTimeLabel",    w.StageRelics.transform);

        w.PlayerRowParent = SafeGetField<Transform>(screen, "_playerRowParent");
        if (w.PlayerRowParent == null)
        {
            w.PlayerRowParent = CreateScrollListParent(w.StagePlayers.transform, "PlayerRowParent",
                new Vector2(40f, 20f), new Vector2(-40f, -20f));
        }
        else if (w.PlayerRowParent.parent != w.StagePlayers.transform)
        {
            w.PlayerRowParent.SetParent(w.StagePlayers.transform, false);
        }

        w.PlayerRowPrefab = SafeGetField<GameObject>(screen, "_playerRowPrefab");
        if (w.PlayerRowPrefab == null)
        {
            w.PlayerRowPrefab = CreatePlayerRowPrefab();
            w.PlayerRowPrefab.SetActive(false);
            w.PlayerRowPrefab.transform.SetParent(screen.transform, false);
        }

        w.TitleRowParent = SafeGetField<Transform>(screen, "_titleRowParent");
        if (w.TitleRowParent == null)
        {
            w.TitleRowParent = CreateScrollListParent(w.StageTitles.transform, "TitleRowParent",
                new Vector2(40f, 20f), new Vector2(-40f, -20f));
        }
        else if (w.TitleRowParent.parent != w.StageTitles.transform)
        {
            w.TitleRowParent.SetParent(w.StageTitles.transform, false);
        }

        w.TitleRowPrefab = SafeGetField<GameObject>(screen, "_titleRowPrefab");
        if (w.TitleRowPrefab == null)
        {
            w.TitleRowPrefab = CreateTitleRowPrefab();
            w.TitleRowPrefab.SetActive(false);
            w.TitleRowPrefab.transform.SetParent(screen.transform, false);
        }

        w.CosmeticGroup = SafeGetField<GameObject>(screen, "_cosmeticGroup");
        if (w.CosmeticGroup == null)
        {
            w.CosmeticGroup = CreateStageContainer(w.StageCosmetic.transform, "CosmeticGroup", out var cgRt);
            Stretch(cgRt);
        }

        w.CosmeticLabel = SafeGetField<TextMeshProUGUI>(screen, "_cosmeticLabel")
                          ?? FindOrCreateLabel(w.CosmeticGroup.transform, "CosmeticLabel", "", 18,
                              new Vector2(0.5f, 0.5f), Vector2.zero, UiPalette.CreamDim);

        w.RetryButton = SafeGetField<Button>(screen, "_retryButton")
                        ?? FindButtonInHierarchy(w.StageButtons.transform, "RetryButton")
                        ?? CreateThemedActionButton(w.StageButtons.transform, "RetryButton", "もう一度",
                            new Vector2(-130f, 0f), MenuUiKit.BtnPrimary);

        w.ReturnBaseButton = SafeGetField<Button>(screen, "_returnBaseButton")
                             ?? FindButtonInHierarchy(w.StageButtons.transform, "ReturnBaseButton")
                             ?? FindButtonInHierarchy(w.StageButtons.transform, "ReturnButton")
                             ?? CreateThemedActionButton(w.StageButtons.transform, "ReturnBaseButton", "ベースに戻る",
                                 new Vector2(130f, 0f), MenuUiKit.BtnSecondary);

        ReparentIfExists(panelT, "RetryButton",       w.StageButtons.transform);
        ReparentIfExists(panelT, "ReturnButton",      w.StageButtons.transform);
        ReparentIfExists(panelT, "ReturnBaseButton",  w.StageButtons.transform);

        return w;
    }

    private static void ApplyWiring(ResultScreen screen, WiringResult w)
    {
        SetField(screen, "_panel",             w.Panel);
        SetField(screen, "_teamScoreLabel",    w.TeamScoreLabel);
        SetField(screen, "_relicSummaryLabel", w.RelicSummaryLabel);
        SetField(screen, "_clearTimeLabel",    w.ClearTimeLabel);
        SetField(screen, "_playerRowParent",   w.PlayerRowParent);
        SetField(screen, "_playerRowPrefab",   w.PlayerRowPrefab);
        SetField(screen, "_titleRowParent",    w.TitleRowParent);
        SetField(screen, "_titleRowPrefab",    w.TitleRowPrefab);
        SetField(screen, "_cosmeticGroup",     w.CosmeticGroup);
        SetField(screen, "_cosmeticLabel",     w.CosmeticLabel);
        SetField(screen, "_retryButton",       w.RetryButton);
        SetField(screen, "_returnBaseButton",  w.ReturnBaseButton);
        SetField(screen, "_stageTeamScore",    w.StageTeamScore);
        SetField(screen, "_stageRelics",       w.StageRelics);
        SetField(screen, "_stagePlayers",      w.StagePlayers);
        SetField(screen, "_stageTitles",       w.StageTitles);
        SetField(screen, "_stageCosmetic",     w.StageCosmetic);
        SetField(screen, "_stageButtons",      w.StageButtons);
    }

    private static GameObject ResolveStage(Transform panel, string stageName)
    {
        var existing = panel.Find(stageName);
        if (existing != null) return existing.gameObject;
        return CreateStageContainer(panel, stageName, out _);
    }

    private static Button FindButtonInHierarchy(Transform parent, string name)
    {
        var t = parent.Find(name);
        return t != null ? t.GetComponent<Button>() : null;
    }

    // ── 構築ヘルパー ─────────────────────────────────────────

    private static GameObject CreateFullScreenPanel(Transform parent)
    {
        var go = new GameObject("Panel");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        Stretch(rt);
        go.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.06f, 0.92f);

        FlowUiTheme.CreateSceneBackdrop(go.transform, FlowUiTheme.SceneFlavor.ResultPeak);

        var overlay = new GameObject("ResultDim");
        overlay.transform.SetParent(go.transform, false);
        Stretch(overlay.AddComponent<RectTransform>());
        overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

        go.SetActive(false);
        return go;
    }

    private static void EnsureCanvasGroup(GameObject panel)
    {
        if (panel.GetComponent<CanvasGroup>() == null)
            panel.AddComponent<CanvasGroup>();
    }

    /// <summary>
    /// 旧シーンの素パネル（単色 Image のみ）を PEAK リザルト演出へ整形する（非破壊）。
    /// </summary>
    private static void PolishPanelVisuals(GameObject panel)
    {
        if (panel == null) return;

        var t = panel.transform;
        if (t.Find("PeakSky") == null)
            FlowUiTheme.CreateSceneBackdrop(t, FlowUiTheme.SceneFlavor.ResultPeak);

        if (t.Find("ResultDim") == null)
        {
            var overlay = new GameObject("ResultDim");
            overlay.transform.SetParent(t, false);
            Stretch(overlay.AddComponent<RectTransform>());
            overlay.AddComponent<Image>().color = new Color(0.01f, 0.02f, 0.04f, 0.62f);
        }
        else
        {
            // 既存の暗幕も、夕空グローでスコア文字が washout しないよう濃いめへ更新する。
            var img = t.Find("ResultDim").GetComponent<Image>();
            if (img != null) img.color = new Color(0.01f, 0.02f, 0.04f, 0.62f);
        }

        // 描画順を明示的に正規化する。バックドロップ要素（空・太陽グロー・山影）は不透明で、
        // これらがステージより後（前面）に並ぶとスコア等のステージ要素を覆い隠してしまう。
        // 並び順: バックドロップ(最背面) → 暗幕 → ステージ/ヘッダ(コンテンツ) → ビネット/グレイン(最前面FX)。
        string[] backdropOrder = { "PeakSky", "PeakSunHalo", "PeakSunCore", "MountainSilhouette" };
        int siblingIndex = 0;
        foreach (var bn in backdropOrder)
        {
            var b = t.Find(bn);
            if (b != null) b.SetSiblingIndex(siblingIndex++);
        }
        var dimTf = t.Find("ResultDim");
        if (dimTf != null) dimTf.SetSiblingIndex(siblingIndex++);

        // ビネット/グレインはポストFXとしてコンテンツの上へ。
        var vignetteTf = t.Find("Vignette");
        if (vignetteTf != null) vignetteTf.SetAsLastSibling();
        var grainTf = t.Find("Grain");
        if (grainTf != null) grainTf.SetAsLastSibling();

        // ルート Image は背景を透過し、手続きバックドロップに任せる
        var rootImg = panel.GetComponent<Image>();
        if (rootImg != null)
            rootImg.color = new Color(0f, 0f, 0f, 0f);

        EnsureResultCanvasSortOrder(panel);
        EnsurePanelFillsScreen(panel);
        EnsureResultChrome(t);
    }

    /// <summary>
    /// Panel を独立したルート全画面 Canvas へ昇格させる。
    /// ResultScreen が素の Transform 配下だったり、共有 UIRoot Canvas の rect が画面サイズに
    /// 追従していない（例: 535×301 のまま）と、子 Panel の stretch アンカーが小領域に潰れ、
    /// スコア等のステージ要素が画面外/隅に押し込まれて見えなくなる。
    /// シーンルートへ退避し Panel 自身を ScreenSpaceOverlay のルート Canvas にすることで、
    /// 親の矩形に依存せず常に実画面サイズへ追従させる（非破壊・ResultScreen の参照は維持）。
    /// </summary>
    private static void EnsurePanelFillsScreen(GameObject panel)
    {
        if (!(panel.transform is RectTransform rt)) return;

        // 共有 UIRoot 等の親から切り離してシーンルートへ。これで Panel の Canvas がルート扱いになり、
        // Unity が RectTransform を実画面サイズへ自動ドライブする。
        if (rt.parent != null)
            rt.SetParent(null, false);

        var canvas = panel.GetComponent<Canvas>();
        if (canvas == null) canvas = panel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 5200;

        var scaler = panel.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = panel.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (panel.GetComponent<GraphicRaycaster>() == null)
            panel.AddComponent<GraphicRaycaster>();

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    /// <summary>
    /// ExpeditionHUD(sortingOrder=100) より前面に描画する。
    /// UIRoot 共有 Canvas 上だと HUD がリザルトを覆ってしまうため。
    /// </summary>
    private static void EnsureResultCanvasSortOrder(GameObject panel)
    {
        var canvas = panel.GetComponent<Canvas>();
        if (canvas == null) canvas = panel.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 5200;

        if (panel.GetComponent<GraphicRaycaster>() == null)
            panel.AddComponent<GraphicRaycaster>();
    }

    private static void EnsureResultChrome(Transform panel)
    {
        {
            var header = FindOrCreateLabel(panel, "ResultHeader", "EXPEDITION COMPLETE",
                44, new Vector2(0.5f, 1f), new Vector2(0f, -40f), FlowUiTheme.TerminalAccent);
            header.fontStyle = FontStyles.Bold;
            header.characterSpacing = 8f;
            header.enableAutoSizing = false;
            header.fontSize = 44;
            header.alignment = TextAlignmentOptions.Center;
            var hrt = header.rectTransform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.sizeDelta = new Vector2(1200f, 64f);
            hrt.anchoredPosition = new Vector2(0f, -40f);
        }

        {
            var sub = FindOrCreateLabel(panel, "ResultSubheader", "遠征リザルト — チームの成果を集計中",
                24, new Vector2(0.5f, 1f), new Vector2(0f, -96f), UiPalette.CreamDim);
            sub.enableAutoSizing = false;
            sub.fontSize = 24;
            sub.alignment = TextAlignmentOptions.Center;
            var srt = sub.rectTransform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 1f);
            srt.pivot = new Vector2(0.5f, 1f);
            srt.sizeDelta = new Vector2(1100f, 40f);
            srt.anchoredPosition = new Vector2(0f, -96f);
        }

        if (panel.Find("SkipHint") == null)
        {
            var skip = FindOrCreateLabel(panel, "SkipHint", "Space / A  —  次の段階へ",
                15, new Vector2(0.5f, 0f), new Vector2(0f, 22f), UiPalette.CreamDim);
            skip.fontStyle = FontStyles.Italic;
            var skrt = skip.rectTransform;
            skrt.pivot = new Vector2(0.5f, 0f);
            skrt.sizeDelta = new Vector2(600f, 28f);
            skip.gameObject.SetActive(false);
        }
    }

    private static GameObject CreateStageContainer(Transform parent, string name, out RectTransform rt)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        rt = go.AddComponent<RectTransform>();
        Stretch(rt);
        return go;
    }

    private static Transform CreateScrollListParent(Transform parent, string name,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        // 幅は行プレハブ側の固定幅で確定させる（VLG の幅制御は実行時生成だと再ビルドされず幅0に潰れることがある）。
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperCenter;
        return go.transform;
    }

    private static TextMeshProUGUI FindOrCreateLabel(Transform parent, string name, string text,
        int fontSize, Vector2 anchor, Vector2 pos, Color color)
    {
        var existing = parent.Find(name);
        if (existing != null)
        {
            var existingTmp = existing.GetComponent<TextMeshProUGUI>();
            if (existingTmp != null) return existingTmp;
        }

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(700f, fontSize * 2f);
        rt.anchoredPosition = pos;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        ApplyDefaultFont(tmp);
        FlowUiTheme.StyleReadable(tmp, fontSize >= 32 ? 0.2f : 0.14f);
        return tmp;
    }

    private static void StyleRowBackground(GameObject rowGo)
    {
        var img = rowGo.GetComponent<Image>();
        if (img == null) return;
        img.sprite = UiSprite.RoundedRect(12);
        img.type = Image.Type.Sliced;
        img.color = RowBg;

        if (rowGo.transform.Find("RowFrame") != null) return;
        var frame = FlowUiTheme.NewRect("RowFrame", rowGo.transform as RectTransform);
        FlowUiTheme.Stretch(frame, 1f);
        FlowUiTheme.AddSprite(frame, UiSprite.RoundedFrame(12, 1),
            new Color(FlowUiTheme.TerminalBorder.r, FlowUiTheme.TerminalBorder.g,
                FlowUiTheme.TerminalBorder.b, 0.55f)).raycastTarget = false;
    }

    private static GameObject CreatePlayerRowPrefab()
    {
        var go = new GameObject("PlayerResultRowPrefab");
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1320f, 56f);
        go.AddComponent<Image>().color = RowBg;
        StyleRowBackground(go);
        var row = go.AddComponent<PlayerResultRow>();

        var nameLabel = CreateRowLabel(go.transform, "Name", "Player", 18,
            new Vector2(0f, 0.5f), new Vector2(240f, 40f), TextAlignmentOptions.Left);
        var scoreLabel = CreateRowLabel(go.transform, "Score", "0 pt", 18,
            new Vector2(0.5f, 0.5f), new Vector2(160f, 40f), TextAlignmentOptions.Center);
        var detailLabel = CreateRowLabel(go.transform, "Detail", "", 14,
            new Vector2(1f, 0.5f), new Vector2(360f, 40f), TextAlignmentOptions.Right);
        detailLabel.color = UiPalette.CreamDim;

        BindRowFields(row, nameLabel, scoreLabel, detailLabel);
        return go;
    }

    private static GameObject CreateTitleRowPrefab()
    {
        var go = new GameObject("TitleRowPrefab");
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1320f, 40f);
        go.AddComponent<Image>().color = RowBg;
        StyleRowBackground(go);
        var entry = go.AddComponent<TitleRowEntry>();

        var playerLabel = CreateRowLabel(go.transform, "Player", "Player", 16,
            new Vector2(0f, 0.5f), new Vector2(220f, 32f), TextAlignmentOptions.Left);
        var titleLabel = CreateRowLabel(go.transform, "Title", "「称号」", 16,
            new Vector2(1f, 0.5f), new Vector2(420f, 32f), TextAlignmentOptions.Right);
        titleLabel.color = UiPalette.Amber;
        titleLabel.fontStyle = FontStyles.Bold;

        BindTitleFields(entry, playerLabel, titleLabel);
        return go;
    }

    private static TextMeshProUGUI CreateRowLabel(Transform parent, string name, string text,
        int fontSize, Vector2 anchor, Vector2 size, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        // アンカーした端にラベルを沿わせ、行内に収まるよう水平インセットする。
        // 端アンカーで pivot を中央のままにすると枠が行外へはみ出すため。
        const float insetX = 28f;
        if (anchor.x <= 0.001f)
        {
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(insetX, 0f);
        }
        else if (anchor.x >= 0.999f)
        {
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-insetX, 0f);
        }
        else
        {
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }
        rt.sizeDelta = size;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = UiPalette.Cream;
        ApplyDefaultFont(tmp);
        FlowUiTheme.StyleReadable(tmp, 0.12f);
        return tmp;
    }

    private static Button CreateThemedActionButton(Transform parent, string name, string label,
        Vector2 pos, Color fill)
    {
        var btn = MenuUiKit.CreateMenuButton(parent, name, label,
            new Vector2(0.5f, 0.5f), new Vector2(220f, 64f), fill, null);
        btn.GetComponent<RectTransform>().anchoredPosition = pos;
        return btn;
    }

    private static void ReparentIfExists(Transform panel, string childName, Transform newParent)
    {
        var child = panel.Find(childName);
        if (child != null && child.parent != newParent)
            child.SetParent(newParent, false);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void ApplyDefaultFont(TextMeshProUGUI tmp)
    {
        if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
    }

    // ── Reflection（Unity 未割当 SerializeField 対応）──────────

    private static T SafeGetField<T>(object target, string name) where T : class
    {
        try
        {
            var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) return null;
            return field.GetValue(target) as T;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void SetField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(target, value);
    }

    private static void BindRowFields(PlayerResultRow row,
        TextMeshProUGUI name, TextMeshProUGUI score, TextMeshProUGUI detail)
    {
        SetField(row, "_nameLabel", name);
        SetField(row, "_scoreLabel", score);
        SetField(row, "_detailLabel", detail);
    }

    private static void BindTitleFields(TitleRowEntry entry,
        TextMeshProUGUI player, TextMeshProUGUI title)
    {
        SetField(entry, "_playerLabel", player);
        SetField(entry, "_titleLabel", title);
    }
}
