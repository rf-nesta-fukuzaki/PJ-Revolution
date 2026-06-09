using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sandbox.UI
{
    /// <summary>
    /// SettingsManager 用の R.E.P.O. 端末風設定 UI を実行時構築・テーマ適用する。
    /// Inspector 未配線のコントロールも各タブ内に生成する。
    /// </summary>
    public static class SettingsRuntimeUiBuilder
    {
        private static readonly string[] WindowModes = { "フルスクリーン", "ウィンドウ", "ボーダーレス" };
        private static readonly string[] QualityLevels = { "低", "中", "高", "最高" };
        private static readonly string[] FpsCaps = { "30", "60", "120", "無制限" };
        private static readonly string[] ShadowLevels = { "OFF", "低", "中", "高" };
        private static readonly string[] ParticleLevels = { "低", "中", "高" };
        private static readonly string[] GamepadPresets = { "デフォルト", "代替 A", "代替 B" };
        private static readonly string[] ColorBlindModes = { "OFF", "Protan", "Deutan", "Tritan" };

        public static void HighlightTab(SettingsManager manager, int index)
        {
            if (manager == null) return;
            RestyleTabButton(GetField<Button>(manager, "_tabGraphics"), "GRAPHICS", index == 0);
            RestyleTabButton(GetField<Button>(manager, "_tabAudio"), "AUDIO", index == 1);
            RestyleTabButton(GetField<Button>(manager, "_tabControls"), "CONTROLS", index == 2);
            RestyleTabButton(GetField<Button>(manager, "_tabAccessibility"), "ACCESS", index == 3);
        }

        public static void EnsureThemed(SettingsManager manager)
        {
            if (manager == null) return;

            var panel = GetField<GameObject>(manager, "_settingsPanel");
            if (panel == null)
                panel = CreateRootPanel(manager.transform);

            RestylePanel(panel);
            RestyleTabButton(GetField<Button>(manager, "_tabGraphics"), "GRAPHICS", true);
            RestyleTabButton(GetField<Button>(manager, "_tabAudio"), "AUDIO", false);
            RestyleTabButton(GetField<Button>(manager, "_tabControls"), "CONTROLS", false);
            RestyleTabButton(GetField<Button>(manager, "_tabAccessibility"), "ACCESS", false);
            RestyleCloseButton(GetField<Button>(manager, "_btnClose"));

            var gPanel = GetField<GameObject>(manager, "_panelGraphics");
            var aPanel = GetField<GameObject>(manager, "_panelAudio");
            var cPanel = GetField<GameObject>(manager, "_panelControls");
            var xPanel = GetField<GameObject>(manager, "_panelAccessibility");

            EnsureTabLayout(gPanel?.transform, "GRAPHICS");
            EnsureTabLayout(aPanel?.transform, "AUDIO");
            EnsureTabLayout(cPanel?.transform, "CONTROLS");
            EnsureTabLayout(xPanel?.transform, "ACCESSIBILITY");

            SetFieldIfNull(manager, "_ddWindowMode",   EnsureDropdown(gPanel?.transform, "WindowMode",   WindowModes,   GetField<TMP_Dropdown>(manager, "_ddWindowMode")));
            SetFieldIfNull(manager, "_ddQuality",      EnsureDropdown(gPanel?.transform, "Quality",      QualityLevels, GetField<TMP_Dropdown>(manager, "_ddQuality")));
            SetFieldIfNull(manager, "_ddFpsCap",       EnsureDropdown(gPanel?.transform, "FpsCap",       FpsCaps,       GetField<TMP_Dropdown>(manager, "_ddFpsCap")));
            SetFieldIfNull(manager, "_ddShadow",       EnsureDropdown(gPanel?.transform, "Shadow",       ShadowLevels,  GetField<TMP_Dropdown>(manager, "_ddShadow")));
            SetFieldIfNull(manager, "_ddParticle",     EnsureDropdown(gPanel?.transform, "Particle",     ParticleLevels,GetField<TMP_Dropdown>(manager, "_ddParticle")));
            SetFieldIfNull(manager, "_ddResolution",   EnsureDropdown(gPanel?.transform, "Resolution",   new[] { "現在の解像度" }, GetField<TMP_Dropdown>(manager, "_ddResolution")));
            SetFieldIfNull(manager, "_togVSync",       EnsureToggle(gPanel?.transform, "VSync", "垂直同期", GetField<Toggle>(manager, "_togVSync")));

            SetFieldIfNull(manager, "_slMaster",  EnsureSlider(aPanel?.transform, "Master",  "MASTER", 0, 100, GetField<Slider>(manager, "_slMaster")));
            SetFieldIfNull(manager, "_slBgm",     EnsureSlider(aPanel?.transform, "Bgm",     "BGM",    0, 100, GetField<Slider>(manager, "_slBgm")));
            SetFieldIfNull(manager, "_slSe",      EnsureSlider(aPanel?.transform, "Se",      "SE",     0, 100, GetField<Slider>(manager, "_slSe")));
            SetFieldIfNull(manager, "_slVoice",   EnsureSlider(aPanel?.transform, "Voice",   "VOICE",  0, 100, GetField<Slider>(manager, "_slVoice")));
            SetFieldIfNull(manager, "_slMicGain", EnsureSlider(aPanel?.transform, "MicGain", "MIC",    0, 200, GetField<Slider>(manager, "_slMicGain")));
            SetFieldIfNull(manager, "_btnMicTest", EnsureActionButton(aPanel?.transform, "MicTest", "マイクテスト", GetField<Button>(manager, "_btnMicTest")));

            SetFieldIfNull(manager, "_slMouseSens",     EnsureSlider(cPanel?.transform, "MouseSens", "マウス感度", 0.5f, 10f, GetField<Slider>(manager, "_slMouseSens")));
            SetFieldIfNull(manager, "_togInvertY",      EnsureToggle(cPanel?.transform, "InvertY", "Y軸反転", GetField<Toggle>(manager, "_togInvertY")));
            SetFieldIfNull(manager, "_ddGamepadPreset", EnsureDropdown(cPanel?.transform, "Gamepad", GamepadPresets, GetField<TMP_Dropdown>(manager, "_ddGamepadPreset")));

            SetFieldIfNull(manager, "_togSubtitles",    EnsureToggle(xPanel?.transform, "Subtitles", "字幕", GetField<Toggle>(manager, "_togSubtitles")));
            SetFieldIfNull(manager, "_slUiScale",       EnsureSlider(xPanel?.transform, "UiScale", "UI スケール %", 80, 150, GetField<Slider>(manager, "_slUiScale")));
            SetFieldIfNull(manager, "_ddColorBlind",    EnsureDropdown(xPanel?.transform, "ColorBlind", ColorBlindModes, GetField<TMP_Dropdown>(manager, "_ddColorBlind")));
            SetFieldIfNull(manager, "_togCameraShake",  EnsureToggle(xPanel?.transform, "CamShake", "カメラシェイク軽減", GetField<Toggle>(manager, "_togCameraShake")));
            SetFieldIfNull(manager, "_crosshairColorSwatch", EnsureSwatch(xPanel?.transform, GetField<Image>(manager, "_crosshairColorSwatch")));

            SetField(manager, "_settingsPanel", panel);
        }

#if UNITY_EDITOR
        /// <summary>エディタ上でテーマ UI を構築し SerializedObject 経由でシーンに保存する。</summary>
        public static void EnsureThemedAndSave(SettingsManager manager)
        {
            if (manager == null) return;

            EnsureThemed(manager);

            var so = new SerializedObject(manager);
            so.Update();
            SetProp(so, "_settingsPanel", GetField<GameObject>(manager, "_settingsPanel"));
            SetProp(so, "_tabGraphics", GetField<Button>(manager, "_tabGraphics"));
            SetProp(so, "_tabAudio", GetField<Button>(manager, "_tabAudio"));
            SetProp(so, "_tabControls", GetField<Button>(manager, "_tabControls"));
            SetProp(so, "_tabAccessibility", GetField<Button>(manager, "_tabAccessibility"));
            SetProp(so, "_panelGraphics", GetField<GameObject>(manager, "_panelGraphics"));
            SetProp(so, "_panelAudio", GetField<GameObject>(manager, "_panelAudio"));
            SetProp(so, "_panelControls", GetField<GameObject>(manager, "_panelControls"));
            SetProp(so, "_panelAccessibility", GetField<GameObject>(manager, "_panelAccessibility"));
            SetProp(so, "_ddResolution", GetField<TMP_Dropdown>(manager, "_ddResolution"));
            SetProp(so, "_ddWindowMode", GetField<TMP_Dropdown>(manager, "_ddWindowMode"));
            SetProp(so, "_ddQuality", GetField<TMP_Dropdown>(manager, "_ddQuality"));
            SetProp(so, "_ddFpsCap", GetField<TMP_Dropdown>(manager, "_ddFpsCap"));
            SetProp(so, "_togVSync", GetField<Toggle>(manager, "_togVSync"));
            SetProp(so, "_ddShadow", GetField<TMP_Dropdown>(manager, "_ddShadow"));
            SetProp(so, "_ddParticle", GetField<TMP_Dropdown>(manager, "_ddParticle"));
            SetProp(so, "_slMaster", GetField<Slider>(manager, "_slMaster"));
            SetProp(so, "_slBgm", GetField<Slider>(manager, "_slBgm"));
            SetProp(so, "_slSe", GetField<Slider>(manager, "_slSe"));
            SetProp(so, "_slVoice", GetField<Slider>(manager, "_slVoice"));
            SetProp(so, "_slMicGain", GetField<Slider>(manager, "_slMicGain"));
            SetProp(so, "_btnMicTest", GetField<Button>(manager, "_btnMicTest"));
            SetProp(so, "_slMouseSens", GetField<Slider>(manager, "_slMouseSens"));
            SetProp(so, "_togInvertY", GetField<Toggle>(manager, "_togInvertY"));
            SetProp(so, "_ddGamepadPreset", GetField<TMP_Dropdown>(manager, "_ddGamepadPreset"));
            SetProp(so, "_togSubtitles", GetField<Toggle>(manager, "_togSubtitles"));
            SetProp(so, "_slUiScale", GetField<Slider>(manager, "_slUiScale"));
            SetProp(so, "_ddColorBlind", GetField<TMP_Dropdown>(manager, "_ddColorBlind"));
            SetProp(so, "_togCameraShake", GetField<Toggle>(manager, "_togCameraShake"));
            SetProp(so, "_crosshairColorSwatch", GetField<Image>(manager, "_crosshairColorSwatch"));
            SetProp(so, "_btnClose", GetField<Button>(manager, "_btnClose"));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);

            var panel = GetField<GameObject>(manager, "_settingsPanel");
            if (panel != null)
                EditorUtility.SetDirty(panel);
        }

        private static void SetProp(SerializedObject so, string name, Object value)
        {
            var prop = so.FindProperty(name);
            if (prop != null) prop.objectReferenceValue = value;
        }
#endif

        private static GameObject CreateRootPanel(Transform parent)
        {
            var canvasGo = new GameObject("SettingsOverlayCanvas");
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6000;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            var dim = new GameObject("SettingsDim");
            dim.transform.SetParent(canvasGo.transform, false);
            var drt = dim.AddComponent<RectTransform>();
            FlowUiTheme.Stretch(drt);
            dim.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);

            FlowUiTheme.CreateSceneBackdrop(canvasGo.transform, FlowUiTheme.SceneFlavor.CoopRepo);

            var panelRt = FlowUiTheme.CreateTerminalPanel(canvasGo.transform, "SettingsPanel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-480f, -300f), new Vector2(480f, 300f));

            CreateHeader(panelRt, "SYSTEM CONFIG", FlowUiTheme.TerminalAccent);

            var tabRow = FlowUiTheme.NewRect("TabRow", panelRt);
            tabRow.anchorMin = new Vector2(0.04f, 1f);
            tabRow.anchorMax = new Vector2(0.96f, 1f);
            tabRow.pivot = new Vector2(0.5f, 1f);
            tabRow.sizeDelta = new Vector2(0f, 48f);
            tabRow.anchoredPosition = new Vector2(0f, -52f);
            var tabLayout = tabRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 8f;
            tabLayout.childAlignment = TextAnchor.MiddleCenter;
            tabLayout.childControlWidth = tabLayout.childControlHeight = true;
            tabLayout.childForceExpandWidth = true;

            var tabG = CreateTabButton(tabRow, "TabGraphics", "GRAPHICS");
            var tabA = CreateTabButton(tabRow, "TabAudio", "AUDIO");
            var tabC = CreateTabButton(tabRow, "TabControls", "CONTROLS");
            var tabX = CreateTabButton(tabRow, "TabAccessibility", "ACCESS");

            var content = FlowUiTheme.NewRect("ContentArea", panelRt);
            content.anchorMin = new Vector2(0.04f, 0.14f);
            content.anchorMax = new Vector2(0.96f, 0.82f);
            content.offsetMin = content.offsetMax = Vector2.zero;

            var panelGraphics = CreateTabPanel(content, "PanelGraphics");
            var panelAudio = CreateTabPanel(content, "PanelAudio");
            var panelControls = CreateTabPanel(content, "PanelControls");
            var panelAccessibility = CreateTabPanel(content, "PanelAccessibility");
            panelAudio.SetActive(false);
            panelControls.SetActive(false);
            panelAccessibility.SetActive(false);

            var close = MenuUiKit.CreateMenuButton(panelRt, "CloseButton", "閉じる",
                new Vector2(0.5f, 0f), new Vector2(220f, 52f), MenuUiKit.BtnPrimary, null);
            close.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 36f);

            var mgr = parent.GetComponent<SettingsManager>();
            if (mgr != null)
            {
                SetField(mgr, "_settingsPanel", panelRt.gameObject);
                SetField(mgr, "_tabGraphics", tabG);
                SetField(mgr, "_tabAudio", tabA);
                SetField(mgr, "_tabControls", tabC);
                SetField(mgr, "_tabAccessibility", tabX);
                SetField(mgr, "_panelGraphics", panelGraphics);
                SetField(mgr, "_panelAudio", panelAudio);
                SetField(mgr, "_panelControls", panelControls);
                SetField(mgr, "_panelAccessibility", panelAccessibility);
                SetField(mgr, "_btnClose", close);
            }

            return panelRt.gameObject;
        }

        private static void RestylePanel(GameObject panel)
        {
            if (panel == null) return;

            var parent = panel.transform.parent;
            if (parent != null && parent.Find("SettingsDim") == null)
            {
                var dim = new GameObject("SettingsDim");
                dim.transform.SetParent(parent, false);
                dim.transform.SetSiblingIndex(panel.transform.GetSiblingIndex());
                var drt = dim.AddComponent<RectTransform>();
                FlowUiTheme.Stretch(drt);
                dim.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
            }

            var outer = panel.GetComponent<Image>();
            if (outer != null) outer.color = FlowUiTheme.TerminalBorder;

            var inner = panel.transform.Find("Inner");
            if (inner == null)
            {
                var innerRt = FlowUiTheme.NewRect("Inner", panel.transform);
                innerRt.SetAsFirstSibling();
                FlowUiTheme.Stretch(innerRt, 2f);
                innerRt.gameObject.AddComponent<Image>().color = FlowUiTheme.TerminalBg;
            }

            if (panel.transform.Find("SettingsHeader") == null)
                CreateHeader(panel.transform, "SYSTEM CONFIG", FlowUiTheme.TerminalAccent);
        }

        private static void EnsureTabLayout(Transform panel, string header)
        {
            if (panel == null) return;
            var oldLabel = panel.Find("PanelLabel");
            if (oldLabel != null) DestroyObject(oldLabel.gameObject);

            if (panel.GetComponent<VerticalLayoutGroup>() == null)
            {
                var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 10f;
                layout.padding = new RectOffset(12, 12, 8, 8);
                layout.childControlWidth = true;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = true;
            }
        }

        private static Button CreateTabButton(Transform row, string name, string label)
        {
            return MenuUiKit.CreateMenuButton(row, name, label,
                new Vector2(0.5f, 0.5f), new Vector2(160f, 40f), MenuUiKit.BtnSecondary, null);
        }

        private static GameObject CreateTabPanel(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            FlowUiTheme.Stretch(rt);
            return go;
        }

        private static void CreateHeader(Transform panel, string text, Color color)
        {
            var tmp = MenuUiKit.CreateTitleText(panel, "SettingsHeader", text, 32, new Vector2(0.5f, 1f), color);
            tmp.rectTransform.anchoredPosition = new Vector2(0f, -18f);
        }

        private static void RestyleTabButton(Button btn, string label, bool active)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = active ? MenuUiKit.BtnPrimary : MenuUiKit.BtnSecondary;
            var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = label;
                tmp.fontSize = 16;
                tmp.color = UiPalette.Cream;
                FlowUiTheme.StyleReadable(tmp, 0.1f);
            }
        }

        private static void RestyleCloseButton(Button btn)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = MenuUiKit.BtnPrimary;
            var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = "閉じる";
                FlowUiTheme.StyleReadable(tmp, 0.12f);
            }
        }

        private static TMP_Dropdown EnsureDropdown(Transform panel, string id, string[] options, TMP_Dropdown existing)
        {
            if (existing != null) { StyleDropdown(existing); return existing; }
            if (panel == null) return null;
            var row = CreateRow(panel, id);
            CreateRowLabel(row, id, options[0].Length > 6 ? id : id);
            var dd = CreateDropdown(row, id + "Dropdown", options);
            StyleDropdown(dd);
            return dd;
        }

        private static Slider EnsureSlider(Transform panel, string id, string label, float min, float max, Slider existing)
        {
            if (existing != null) { StyleSlider(existing); return existing; }
            if (panel == null) return null;
            var row = CreateRow(panel, id);
            CreateRowLabel(row, id, label);
            var slider = CreateSlider(row, id + "Slider", min, max);
            StyleSlider(slider);
            return slider;
        }

        private static Toggle EnsureToggle(Transform panel, string id, string label, Toggle existing)
        {
            if (existing != null) { StyleToggle(existing); return existing; }
            if (panel == null) return null;
            var row = CreateRow(panel, id);
            CreateRowLabel(row, id, label);
            var toggle = CreateToggle(row, id + "Toggle");
            StyleToggle(toggle);
            return toggle;
        }

        private static Button EnsureActionButton(Transform panel, string id, string label, Button existing)
        {
            if (existing != null) return existing;
            if (panel == null) return null;
            return MenuUiKit.CreateMenuButton(panel, id, label,
                new Vector2(0.5f, 0.5f), new Vector2(220f, 44f), MenuUiKit.BtnSecondary, null);
        }

        private static Image EnsureSwatch(Transform panel, Image existing)
        {
            if (existing != null) return existing;
            if (panel == null) return null;
            var row = CreateRow(panel, "CrosshairSwatch");
            CreateRowLabel(row, "SwatchLabel", "クロスヘア色");
            var go = new GameObject("CrosshairSwatch");
            go.transform.SetParent(row, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 48f;
            le.preferredHeight = 28f;
            return go.AddComponent<Image>();
        }

        private static Transform CreateRow(Transform panel, string name)
        {
            var existing = panel.Find(name);
            if (existing != null) return existing;

            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(panel, false);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 44f;
            le.preferredHeight = 44f;
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 12f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = false;
            h.childForceExpandWidth = false;
            return go.transform;
        }

        private static TextMeshProUGUI CreateRowLabel(Transform row, string name, string text)
        {
            var go = new GameObject(name + "Label");
            go.transform.SetParent(row, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 200f;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 18;
            tmp.color = UiPalette.CreamDim;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            MenuUiKit.EnsureDefaultFont(tmp);
            FlowUiTheme.StyleReadable(tmp, 0.1f);
            return tmp;
        }

        private static TMP_Dropdown CreateDropdown(Transform parent, string name, string[] options)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.preferredHeight = 36f;

            var bg = go.AddComponent<Image>();
            bg.color = UiPalette.Track;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            FlowUiTheme.Stretch(lrt, 8f);
            var caption = labelGo.AddComponent<TextMeshProUGUI>();
            caption.fontSize = 16;
            caption.color = UiPalette.Cream;
            caption.alignment = TextAlignmentOptions.MidlineLeft;
            MenuUiKit.EnsureDefaultFont(caption);

            var dd = go.AddComponent<TMP_Dropdown>();
            dd.targetGraphic = bg;
            dd.captionText = caption;
            dd.options.Clear();
            foreach (var o in options)
                dd.options.Add(new TMP_Dropdown.OptionData(o));
            dd.RefreshShownValue();
            return dd;
        }

        private static Slider CreateSlider(Transform parent, string name, float min, float max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.preferredHeight = 28f;

            var bg = new GameObject("Background");
            bg.transform.SetParent(go.transform, false);
            var bgRt = bg.AddComponent<RectTransform>();
            FlowUiTheme.Stretch(bgRt);
            bg.AddComponent<Image>().color = UiPalette.Track;

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(go.transform, false);
            var faRt = fillArea.AddComponent<RectTransform>();
            FlowUiTheme.Stretch(faRt, 4f);
            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillRt = fill.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0.5f, 1f);
            fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = FlowUiTheme.TerminalAccent;

            var handle = new GameObject("Handle");
            handle.transform.SetParent(go.transform, false);
            var hRt = handle.AddComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(16f, 24f);
            handle.AddComponent<Image>().color = UiPalette.Amber;

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = hRt;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = (min + max) * 0.5f;
            return slider;
        }

        private static Toggle CreateToggle(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 48f;
            le.preferredHeight = 28f;

            var bg = go.AddComponent<Image>();
            bg.color = UiPalette.Track;

            var check = new GameObject("Checkmark");
            check.transform.SetParent(go.transform, false);
            var crt = check.AddComponent<RectTransform>();
            FlowUiTheme.Stretch(crt, 6f);
            check.AddComponent<Image>().color = FlowUiTheme.TerminalAccent;

            var toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = bg;
            toggle.graphic = check.GetComponent<Image>();
            return toggle;
        }

        private static void StyleDropdown(TMP_Dropdown dd)
        {
            if (dd == null) return;
            if (dd.captionText != null)
            {
                MenuUiKit.EnsureDefaultFont(dd.captionText);
                dd.captionText.color = UiPalette.Cream;
                FlowUiTheme.StyleReadable(dd.captionText, 0.08f);
            }
        }

        private static void StyleSlider(Slider s)
        {
            if (s == null) return;
        }

        private static void StyleToggle(Toggle t)
        {
            if (t == null) return;
        }

        private static T GetField<T>(object target, string name) where T : class
        {
            try
            {
                var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                return field?.GetValue(target) as T;
            }
            catch { return null; }
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(target, value);
        }

        private static void SetFieldIfNull(object target, string name, object value)
        {
            if (value == null) return;
            if (GetField<object>(target, name) != null) return;
            SetField(target, name, value);
        }

        private static void DestroyObject(Object obj)
        {
            if (obj == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                Object.DestroyImmediate(obj);
            else
#endif
                Object.Destroy(obj);
        }
    }
}
