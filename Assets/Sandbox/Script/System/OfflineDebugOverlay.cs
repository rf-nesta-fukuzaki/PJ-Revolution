using System;
using Unity.Netcode;
using UnityEngine;
using Sandbox.UI;

/// <summary>
/// F10 で開閉する統合デバッグメニュー（IMGUI）。
/// 上部に「実装機能の現在値」をリアルタイム表示（<see cref="DebugStateReadout"/>）し、
/// 下部にカテゴリ別の操作ボタン（<see cref="OfflineDebugCommands"/>）を折りたたみで集約する。
/// オンライン/オフラインのセッション操作（<see cref="SandboxOnlineSessionBootstrap"/>）も併設する。
/// </summary>
public sealed class OfflineDebugOverlay : MonoBehaviour
{
    [SerializeField] private float _overlayWidth = 360f;

    private bool _visible = true;
    private GUIStyle _boxStyle;
    private GUIStyle _readoutBoxStyle;
    private GUIStyle _readoutStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _headerStyle;
    private GUIStyle _foldoutStyle;
    private float _cachedUiScale = -1f;
    private string _statusMessage = "起動中...";
    private float _messageTimer;
    private Vector2 _scroll;

    // 折りたたみ状態
    private bool _foldPlayer = true;
    private bool _foldEnv;
    private bool _foldEnemy;
    private bool _foldProgress;
    private bool _foldRelic;
    private bool _foldShortcuts;
    private bool _foldZipline;

    // リアルタイム表示のフレームキャッシュ（OnGUI は 1 フレームに複数回走るため）
    private string _readoutCache = string.Empty;
    private int _readoutFrame = -1;

    public bool Visible
    {
        get => _visible;
        set => _visible = value;
    }

    public void SetStatusMessage(string message, float durationSeconds = 3f)
    {
        _statusMessage = message ?? string.Empty;
        _messageTimer = durationSeconds;
        Debug.Log($"[OfflineBoot] {_statusMessage}");
    }

    private void Update()
    {
        if (_messageTimer > 0f)
            _messageTimer -= Time.unscaledDeltaTime;
    }

    private void OnGUI()
    {
        if (!_visible) return;

        float uiScale = Mathf.Clamp(Screen.height / 1080f, 0.9f, 1.35f);
        float panelWidth = Mathf.Clamp(_overlayWidth * uiScale, 380f, 520f);
        float panelHeight = Mathf.Min(Screen.height - 24f, 940f * uiScale);

        EnsureStyles(uiScale);

        var rect = new Rect(10f, 10f, panelWidth, panelHeight);
        GUILayout.BeginArea(rect, GUIContent.none, _boxStyle);
        _scroll = GUILayout.BeginScrollView(_scroll);

        DrawStatusSection();
        GUILayout.Space(6f);
        DrawLiveReadout();

        var session = SandboxOnlineSessionBootstrap.Instance;
        if (session != null && session.ShowSessionGui)
        {
            GUILayout.Space(8f);
            var prevLabelColor = GUI.skin.label.normal.textColor;
            GUI.skin.label.normal.textColor = UiPalette.Cream;
            session.DrawSessionGui();
            GUI.skin.label.normal.textColor = prevLabelColor;
            GUI.enabled = true;
        }

        GUILayout.Space(8f);
        DrawCommandSections();

        GUILayout.Space(6f);
        DrawMessageSection();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawStatusSection()
    {
        GUILayout.Label("== Debug Menu (F10) ==", _headerStyle);

        if (LocalCoopSettings.IsActive)
        {
            string mode = LocalCoopSettings.IsOnline ? "Online" : "Local";
            GUILayout.Label($"Co-op ({mode}): 人間 {LocalCoopSettings.HumanCount} / NPC {LocalCoopSettings.NpcFillCount} (計{LocalCoopSettings.MaxPartySize})", _labelStyle);
        }
    }

    /// <summary>実装機能の現在値をリアルタイム表示する（観測専用）。</summary>
    private void DrawLiveReadout()
    {
        if (_readoutFrame != Time.frameCount)
        {
            _readoutCache = DebugStateReadout.Build();
            _readoutFrame = Time.frameCount;
        }

        GUILayout.BeginVertical(_readoutBoxStyle);
        GUILayout.Label(_readoutCache, _readoutStyle);
        GUILayout.EndVertical();
    }

    private void DrawCommandSections()
    {
        if (Foldout(ref _foldPlayer, "プレイヤー (HP/気力/状態/移動)"))   DrawPlayerSection();
        if (Foldout(ref _foldEnv, "環境 / 天候 / 時間"))                  DrawEnvironmentSection();
        if (Foldout(ref _foldEnemy, "敵 / AI"))                          DrawEnemySection();
        if (Foldout(ref _foldProgress, "進行 / 経済 / アイテム"))         DrawProgressionSection();
        if (Foldout(ref _foldRelic, "遺物 / 遠征フロー"))                 DrawRelicSection();
        if (Foldout(ref _foldShortcuts, "F1〜F9 ショートカット"))         DrawShortcutSection();
        if (Foldout(ref _foldZipline, "ジップライン (拠点⇄CP)"))         DrawZiplineSection();
    }

    // ── プレイヤー ────────────────────────────────────────────
    private void DrawPlayerSection()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("HP全回復"))   Run(OfflineDebugCommands.HealLocalPlayerFull());
        if (GUILayout.Button("HP-25"))      Run(OfflineDebugCommands.DamageLocalPlayer(25f));
        if (GUILayout.Button("無敵切替"))   Run(OfflineDebugCommands.ToggleGodMode());
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("気力全回復")) Run(OfflineDebugCommands.RefillLocalStamina());
        if (GUILayout.Button("気力ゼロ"))   Run(OfflineDebugCommands.DrainLocalStamina());
        if (GUILayout.Button("酸素切替"))   Run(OfflineDebugCommands.ToggleOxygenTank());
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("ダウン"))     Run(OfflineDebugCommands.ForceDownLocalPlayer());
        if (GUILayout.Button("ダウン蘇生")) Run(OfflineDebugCommands.ReviveLocalFromDowned());
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("完全死亡→幽霊")) Run(OfflineDebugCommands.FinalizeLocalDeath());
        if (GUILayout.Button("復活"))          Run(OfflineDebugCommands.ReviveLocalPlayer());
        GUILayout.EndHorizontal();

        GUILayout.Label("テレポート", _labelStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("拠点"))  Run(OfflineDebugCommands.TeleportToBasecamp());
        if (GUILayout.Button("山頂"))  Run(OfflineDebugCommands.TeleportToSummit());
        if (GUILayout.Button("+50m")) Run(OfflineDebugCommands.TeleportUp(50f));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        for (int i = 0; i < 4; i++)
            if (GUILayout.Button($"CP{i + 1}")) Run(OfflineDebugCommands.TeleportToCheckpoint(i));
        GUILayout.EndHorizontal();
    }

    // ── 環境 / 天候 / 時間 ────────────────────────────────────
    private void DrawEnvironmentSection()
    {
        GUILayout.Label("天候", _labelStyle);
        GUILayout.BeginHorizontal();
        foreach (WeatherType w in Enum.GetValues(typeof(WeatherType)))
            if (GUILayout.Button(w.ToString())) Run(OfflineDebugCommands.SetWeather(w));
        GUILayout.EndHorizontal();

        GUILayout.Label("時間スケール", _labelStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("0.25x")) Run(OfflineDebugCommands.SetTimeScale(0.25f));
        if (GUILayout.Button("0.5x"))  Run(OfflineDebugCommands.SetTimeScale(0.5f));
        if (GUILayout.Button("1x"))    Run(OfflineDebugCommands.SetTimeScale(1f));
        if (GUILayout.Button("2x"))    Run(OfflineDebugCommands.SetTimeScale(2f));
        GUILayout.EndHorizontal();
    }

    // ── 敵 / AI ───────────────────────────────────────────────
    private void DrawEnemySection()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("ウェーブ生成")) Run(OfflineDebugCommands.SpawnEnemyWave());
        if (GUILayout.Button("全敵スタン"))   Run(OfflineDebugCommands.StunAllEnemies());
        if (GUILayout.Button("全敵撃破"))     Run(OfflineDebugCommands.KillAllEnemies());
        GUILayout.EndHorizontal();
    }

    // ── 進行 / 経済 / アイテム ────────────────────────────────
    private void DrawProgressionSection()
    {
        GUILayout.Label("所持金", _labelStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+100"))   Run(OfflineDebugCommands.AddMoney(100));
        if (GUILayout.Button("+1000"))  Run(OfflineDebugCommands.AddMoney(1000));
        if (GUILayout.Button("リセット")) Run(OfflineDebugCommands.ResetMoney());
        GUILayout.EndHorizontal();

        GUILayout.Label("アップグレード", _labelStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("全MAX"))   Run(OfflineDebugCommands.MaxAllUpgrades());
        if (GUILayout.Button("リセット")) Run(OfflineDebugCommands.ResetUpgrades());
        GUILayout.EndHorizontal();

        GUILayout.Label("アイテム付与", _labelStyle);
        int col = 0;
        bool rowOpen = false;
        foreach (ShopItemType t in Enum.GetValues(typeof(ShopItemType)))
        {
            if (col == 0) { GUILayout.BeginHorizontal(); rowOpen = true; }
            if (GUILayout.Button(t.ToString())) Run(OfflineDebugCommands.GiveItem(t));
            if (++col >= 3) { GUILayout.EndHorizontal(); rowOpen = false; col = 0; }
        }
        if (rowOpen) GUILayout.EndHorizontal();
    }

    // ── 遺物 / 遠征 ───────────────────────────────────────────
    private void DrawRelicSection()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("遺物ダメージ25")) Run(OfflineDebugCommands.DamageFirstRelic());
        if (GUILayout.Button("全遺物修復"))     Run(OfflineDebugCommands.RepairAllRelics());
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("帰還エリアへ")) Run(OfflineDebugCommands.MoveRelicsToReturnZone());
        if (GUILayout.Button("遠征開始"))     Run(OfflineDebugCommands.ForceStartExpedition());
        if (GUILayout.Button("強制帰還"))     Run(OfflineDebugCommands.ForceReturn());
        GUILayout.EndHorizontal();
    }

    // ── F1〜F9 ショートカット ─────────────────────────────────
    private void DrawShortcutSection()
    {
        if (GUILayout.Button("[F1] プレイヤー即死"))   Run(OfflineDebugCommands.KillLocalPlayer());
        if (GUILayout.Button("[F2] ヘリ呼び出し"))     Run(OfflineDebugCommands.CallHelicopter());
        if (GUILayout.Button("[F7] 祠で幽霊復活"))     Run(OfflineDebugCommands.ReviveGhost());
        if (GUILayout.Button("[F8] 天候切り替え"))     Run(OfflineDebugCommands.CycleWeather());
        GUILayout.Label("キー: F1〜F9 / F10=開閉", _labelStyle);
    }

    // ── ジップライン ──────────────────────────────────────────
    private void DrawZiplineSection()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("CP1")) Run(OfflineDebugCommands.OpenZipline(0));
        if (GUILayout.Button("CP2")) Run(OfflineDebugCommands.OpenZipline(1));
        if (GUILayout.Button("CP3")) Run(OfflineDebugCommands.OpenZipline(2));
        if (GUILayout.Button("CP4")) Run(OfflineDebugCommands.OpenZipline(3));
        GUILayout.EndHorizontal();
        if (GUILayout.Button("全チェックポイント解放→全開通")) Run(OfflineDebugCommands.OpenAllZiplines());

        if (LocalCoopSettings.IsActive && !LocalCoopSettings.IsOnline)
        {
            GUILayout.Label("ローカル: P1=KB+Mouse / P2-4=Gamepad", _labelStyle);
            GUILayout.Label("後入り: GP Start / 後抜け: GP Back+Start長押し", _labelStyle);
        }
    }

    private void DrawMessageSection()
    {
        if (string.IsNullOrEmpty(_statusMessage)) return;
        var prev = _labelStyle.normal.textColor;
        _labelStyle.normal.textColor = _messageTimer > 0f ? UiPalette.Amber : UiPalette.CreamDim;
        GUILayout.Label($"› {_statusMessage}", _labelStyle);
        _labelStyle.normal.textColor = prev;
    }

    /// <summary>コマンド戻り値をステータス行に流す（ボタンハンドラ共通）。</summary>
    private void Run(string result) => SetStatusMessage(result);

    /// <summary>太字ボタン1つで開閉する折りたたみ見出し。開いていれば true。</summary>
    private bool Foldout(ref bool state, string title)
    {
        if (GUILayout.Button((state ? "▼ " : "▶ ") + title, _foldoutStyle))
            state = !state;
        GUILayout.Space(2f);
        return state;
    }

    private void EnsureStyles(float uiScale)
    {
        if (_boxStyle == null)
        {
            var panelBg = UiPalette.Ink; panelBg.a = 0.92f;
            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTex(2, 2, panelBg) },
                padding = new RectOffset(10, 10, 10, 10),
            };

            var readoutBg = UiPalette.Ink; readoutBg.a = 0.55f;
            _readoutBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTex(2, 2, readoutBg) },
                padding = new RectOffset(8, 8, 6, 6),
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = UiPalette.Cream },
            };

            _readoutStyle = new GUIStyle(_labelStyle);

            _headerStyle = new GUIStyle(_labelStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = UiPalette.Amber },
            };

            _foldoutStyle = new GUIStyle(GUI.skin.button)
            {
                richText = true,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };
            _foldoutStyle.normal.textColor = UiPalette.Amber;
        }

        if (Mathf.Abs(_cachedUiScale - uiScale) < 0.01f) return;

        int fontSize = Mathf.RoundToInt(Mathf.Lerp(13f, 17f, Mathf.InverseLerp(0.9f, 1.35f, uiScale)));
        _labelStyle.fontSize = fontSize;
        _readoutStyle.fontSize = Mathf.Max(11, fontSize - 1);
        _headerStyle.fontSize = fontSize + 1;
        _foldoutStyle.fontSize = fontSize;
        _cachedUiScale = uiScale;
    }

    private static Texture2D MakeTex(int w, int h, Color col)
    {
        var pix = new Color[w * h];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        var tex = new Texture2D(w, h);
        tex.SetPixels(pix);
        tex.Apply();
        return tex;
    }
}
