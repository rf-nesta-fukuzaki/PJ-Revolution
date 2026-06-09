using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandbox.UI;

/// <summary>
/// GDD §2.1 / §9 — 遠征中の HUD。
/// 左上に体力・スタミナのバイタルバー（R.E.P.O.風）、ロープ状態・遺物リスト・警告を表示。
/// タイマーは非表示（経過時間の計測はリザルト用に内部で継続）。
/// </summary>
public class ExpeditionHUD : MonoBehaviour
{
    public static ExpeditionHUD Instance { get; private set; }

    [Header("タイマー")]
    [SerializeField] private TextMeshProUGUI _timerLabel;

    [Header("チェックポイント")]
    [SerializeField] private TextMeshProUGUI _checkpointLabel;

    [Header("旧スタミナ Slider（シーン互換・実行時は隠す）")]
    [SerializeField] private Slider _staminaBar;

    private static Sprite s_ringSprite;

    [Header("バイタルゲージ（左上 / R.E.P.O.風）")]
    // 体力（上段）: 満タン=緑 / 残量わずか=赤。低残量ほど赤へ寄りパルスする。
    [SerializeField] private Color _healthFullColor = new Color(0.42f, 0.84f, 0.40f, 1f);
    [SerializeField] private Color _healthLowColor  = new Color(0.93f, 0.27f, 0.24f, 1f);
    // 気力（下段）: 満タン=アンバー / 残量わずか=オレンジレッド。
    [SerializeField] private Color _staminaBarFullColor = new Color(0.98f, 0.80f, 0.32f, 1f);
    [SerializeField] private Color _staminaBarLowColor  = new Color(0.95f, 0.45f, 0.22f, 1f);
    private VitalsBar _healthVitals;
    private VitalsBar _staminaVitals;

    [Header("ロープ状態")]
    [SerializeField] private Image           _ropeIndicator;
    [SerializeField] private TextMeshProUGUI _ropeLabel;
    [SerializeField] private Color           _ropeConnectedColor = new Color(1f, 0.55f, 0.18f, 1f);
    [SerializeField] private Color           _ropeIdleColor      = new Color(0.55f, 0.57f, 0.62f, 0.7f);

    [Header("遺物リスト")]
    [SerializeField] private Transform       _relicListParent;
    [SerializeField] private GameObject      _relicListItemPrefab;

    [Header("警告")]
    [SerializeField] private TextMeshProUGUI _warningLabel;
    [SerializeField] private float           _warningDisplayTime = 3f;

    [Header("プレイヤー参照")]
    [SerializeField] private StaminaSystem      _localPlayerStamina;
    [SerializeField] private PlayerHealthSystem _localPlayerHealth;

    [Header("表示制御")]
    [SerializeField] private CanvasGroup     _hudCanvasGroup;

    [Header("描画順")]
    // HUD を最前面に固定するソート順。一人称で間近に描画されるプレイヤー自身のモデルや
    // WorldSpace UI（看板等）より常に前へ出すための値。
    [SerializeField] private int _hudSortingOrder = 100;

    // ── 内部状態 ─────────────────────────────────────────────
    private readonly ExpeditionTimer _fallbackTimer = new();
    private ExpeditionTimer _subscribedTimer;
    private string                       _displayTime = "00:00.00";
    private int                              _currentCheckpoint;
    private int                              _totalCheckpoints = 4;
    private float                            _warningTimer;

    private readonly List<RelicHudEntry>     _relicEntries = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        EnsureFullScreenRoot();
        EnforceTopMostOverlay();
        GameplayUiPolish.ApplyExpeditionHudCleanup(transform);
        HideLegacyStaminaWidgets();
        EnsureVitalsBars();
        EnsureRopeIndicator();
        EnsureWarningLabel();
        HideTimerLabel();

        if (_hudCanvasGroup == null)
            _hudCanvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
    }

    /// <summary>
    /// 旧スタミナ表示（左上スライダー・中央リング）を隠す。スタミナは左上のバーへ一本化する。
    /// 中央集約ミニマルの CP ラベルも隠す（通過時は警告トーストで一瞬だけ出る）。
    /// シーンアセットは書き換えず実行時に隠すだけ（非破壊）。
    /// </summary>
    private void HideLegacyStaminaWidgets()
    {
        if (_staminaBar != null) _staminaBar.gameObject.SetActive(false);
        if (_checkpointLabel != null) _checkpointLabel.gameObject.SetActive(false);

        // シーン直保存版（SandboxOfflineCombined 等）はレガシーウィジェットの参照が
        // 未配線、かつアンカーが中央寄りのため、Play 時に画面中央へ灰色のバーや
        // 白い四角として残ってしまう。バイタルは左上 VitalsBar、方角は MiniCompass、
        // タイマー/CP はトーストへ一本化済みなので、名前一致する旧ウィジェットを
        // 参照の有無に依らず実行時に確実に隠す（非破壊・シーンは書き換えない）。
        HideChildByName("StaminaBar");
        HideChildByName("TimerLabel");
        HideChildByName("CheckpointLabel");

        // 旧バージョンが生成した中央リングが残っていれば名前で探して隠す（再生成は行わない）。
        var ring  = transform.Find("StaminaRing");
        if (ring  != null) ring.gameObject.SetActive(false);
        var track = transform.Find("StaminaRingTrack");
        if (track != null) track.gameObject.SetActive(false);
    }

    /// <summary>直下の子を名前一致で非表示にする（存在しなければ無視）。非破壊。</summary>
    private void HideChildByName(string childName)
    {
        var child = transform.Find(childName);
        if (child != null && child.gameObject.activeSelf)
            child.gameObject.SetActive(false);
    }

    /// <summary>
    /// 左上（旧タイマー位置）に体力（上段）と気力（下段）の R.E.P.O. 風バイタルゲージを積む。
    /// 描画・アニメ（残像・グロス・目盛・低残量パルス）は VitalsBar に委譲し、ここは配置のみ（非破壊）。
    /// </summary>
    private void EnsureVitalsBars()
    {
        if (_healthVitals != null && _staminaVitals != null) return;

        const float originX = 24f;   // 左マージン
        const float originY = -18f;  // 上マージン
        const float width   = 236f;  // 大ぶりだった旧 300 から引き締めて画面占有を抑える
        const float barH    = 18f;   // 旧 24 から細くしてミニマルに
        const float gap     = 8f;
        const float row     = barH + 4f; // VitalsBar の行高（アイコン分を含む）

        _healthVitals  = VitalsBar.Create(transform, new Vector2(originX, originY),
            width, barH, VitalIcon.Heart, _healthFullColor, _healthLowColor, showNumber: true);
        _staminaVitals = VitalsBar.Create(transform, new Vector2(originX, originY - row - gap),
            width, barH, VitalIcon.Bolt, _staminaBarFullColor, _staminaBarLowColor, showNumber: true);
    }

    /// <summary>ソフトな環状（ドーナツ）スプライトを手続き生成する（アンチエイリアス付き）。</summary>
    private static Sprite GetRingSprite()
    {
        if (s_ringSprite != null) return s_ringSprite;

        const int size = 128;
        const float thickness = 13f;
        const float aa = 1.5f;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (size - 1) * 0.5f;
        float rOuter = c - 2f;
        float rInner = rOuter - thickness;

        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Min(Mathf.Clamp01((rOuter - d) / aa), Mathf.Clamp01((d - rInner) / aa));
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();

        s_ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return s_ringSprite;
    }

    /// <summary>
    /// HUD を 3D より確実に前面へ固定する（描画順ポリシー）。
    /// ルート Canvas を ScreenSpaceOverlay に固定すると、全カメラ描画後に合成されるため
    /// 一人称で間近に映るプレイヤー自身のモデルや地形・WorldSpace UI に絶対被らない。
    /// さらに高ソート順を与え、他のスクリーン/ワールド Canvas より前に出す。
    /// シーンや将来の改変で ScreenSpaceCamera 等にされても起動時に自己修復する（非破壊）。
    /// </summary>
    private void EnforceTopMostOverlay()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        var root = canvas.rootCanvas;
        if (root.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            // ScreenSpaceCamera/WorldSpace だと 3D に被られ得るため、必ず Overlay へ。
            root.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        if (root.sortingOrder < _hudSortingOrder)
            root.sortingOrder = _hudSortingOrder;

        RefreshRootCanvasScaler(root);
    }

    /// <summary>
    /// ルート Canvas の CanvasScaler が起動時に適用されず、RectTransform がシーン保存値
    /// （例: 535×301）のまま縮退する事象を自己修復する。スケーラを一度トグルして論理解像度
    /// （ref 1600×900 等）へ再計算させると、配下の全 HUD 要素（標高ピル・コンパス・遺物等）が
    /// アンカー相対で正位置へ流れる。シーンアセットは書き換えない（非破壊）。
    /// </summary>
    private static void RefreshRootCanvasScaler(Canvas root)
    {
        if (root == null) return;
        var scaler = root.GetComponent<CanvasScaler>();
        if (scaler == null || !scaler.enabled) return;

        var rt = root.transform as RectTransform;
        // 想定論理幅より明らかに小さい（縮退）場合のみ再適用する。
        if (rt != null && rt.rect.width >= scaler.referenceResolution.x * 0.5f) return;

        scaler.enabled = false;
        scaler.enabled = true;
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// ルートが素の Transform だと、子の四隅アンカー（タイマー=左上 / 遺物=右上 / 警告=上中央）が
    /// 0×0 の原点矩形（= Canvas 中央）基準で解決され、HUD 全体が画面中央（照準域）へ寄ってしまう。
    /// ルートを全画面ストレッチの RectTransform にして、子要素を本来の四隅へ正しく配置する。
    /// シーンアセットは書き換えず実行時に補正する（非破壊）。
    /// </summary>
    private void EnsureFullScreenRoot()
    {
        var rt = transform as RectTransform;
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localPosition = Vector3.zero;
    }

    /// <summary>
    /// タイマー表示は不要になったため非表示にする。経過時間の計測自体は
    /// リザルト（クリアタイム）用に内部で継続する（_timerLabel が null でも UpdateTimerUI は無害）。
    /// シーンの TimerLabel が残っていれば隠すだけ（非破壊）。
    /// </summary>
    private void HideTimerLabel()
    {
        if (_timerLabel == null)
        {
            var existing = transform.Find("TimerLabel");
            if (existing != null) _timerLabel = existing.GetComponent<TextMeshProUGUI>();
        }
        if (_timerLabel != null) _timerLabel.gameObject.SetActive(false);
    }

    /// <summary>
    /// ロープ状態インジケーターを自己修復する。OfflineSceneCreator はスプライト未設定・
    /// 白色の Image を置くため「白い四角」に見える。リングスプライトを割り当て、
    /// idle/接続で配色を変える小アイコン＋ラベルへ整える（非破壊）。
    /// </summary>
    private void EnsureRopeIndicator()
    {
        if (_ropeIndicator == null)
        {
            var existing = transform.Find("RopeIndicator");
            if (existing != null) _ropeIndicator = existing.GetComponent<Image>();
        }
        if (_ropeIndicator == null)
        {
            var go = new GameObject("RopeIndicator", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            _ropeIndicator = go.GetComponent<Image>();
        }

        _ropeIndicator.sprite = GetRingSprite();
        _ropeIndicator.type = Image.Type.Simple;
        _ropeIndicator.preserveAspect = true;
        _ropeIndicator.raycastTarget = false;
        _ropeIndicator.color = _ropeIdleColor;

        var rt = _ropeIndicator.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(24f, 24f);
        // バイタルバー縮小（barH18/row22/gap8）に合わせて真下へ寄せ、中央トーストとの重なりを避ける。
        rt.anchoredPosition = new Vector2(26f, -80f);

        if (_ropeLabel == null)
        {
            var existing = _ropeIndicator.transform.Find("RopeLabel");
            if (existing != null)
            {
                _ropeLabel = existing.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                var lgo = new GameObject("RopeLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
                lgo.transform.SetParent(_ropeIndicator.transform, false);
                _ropeLabel = lgo.GetComponent<TextMeshProUGUI>();
                _ropeLabel.text = "ロープ";
            }
        }

        if (_ropeLabel != null)
        {
            var lrt = _ropeLabel.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 0.5f);
            lrt.pivot = new Vector2(0f, 0.5f);
            lrt.sizeDelta = new Vector2(120f, 26f);
            lrt.anchoredPosition = new Vector2(34f, 0f);
            _ropeLabel.fontSize  = 16f;
            _ropeLabel.alignment = TextAlignmentOptions.Left;
            _ropeLabel.color = UiPalette.CreamDim;
            _ropeLabel.raycastTarget = false;
            UiReadability.MakeReadable(_ropeLabel);
        }
    }

    /// <summary>
    /// 中央上の警告/通知ラベルを自己修復する。シーンの参照配線が無い（Stage01 等）場合でも
    /// 「チェックポイント通過」「ジップライン開通」等のトーストを確実に表示できるようにする（非破壊）。
    /// </summary>
    private void EnsureWarningLabel()
    {
        if (_warningLabel == null)
        {
            var existing = transform.Find("WarningLabel");
            if (existing != null) _warningLabel = existing.GetComponent<TextMeshProUGUI>();
        }
        if (_warningLabel == null)
        {
            var go = new GameObject("WarningLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            _warningLabel = go.GetComponent<TextMeshProUGUI>();
        }

        var rt = _warningLabel.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(720f, 48f);
        rt.anchoredPosition = new Vector2(0f, -110f);

        _warningLabel.fontSize  = 28f;
        _warningLabel.fontStyle = FontStyles.Bold;
        _warningLabel.alignment = TextAlignmentOptions.Center;
        _warningLabel.color = UiPalette.Amber;
        _warningLabel.raycastTarget = false;
        UiReadability.MakeReadable(_warningLabel);
        _warningLabel.enabled = false;
    }

    private void OnEnable()
    {
        ExpeditionEvents.OnExpeditionStarted   += StartTimer;
        ExpeditionEvents.OnExpeditionEnded     += StopTimer;
        ExpeditionEvents.OnCheckpointReached   += SetCheckpoint;
    }

    private void OnDisable()
    {
        ExpeditionEvents.OnExpeditionStarted   -= StartTimer;
        ExpeditionEvents.OnExpeditionEnded     -= StopTimer;
        ExpeditionEvents.OnCheckpointReached   -= SetCheckpoint;
        UnsubscribeTimer();
    }

    private ExpeditionTimer ResolveTimer()
    {
        return GameServices.Expedition != null ? GameServices.Expedition.Timer : _fallbackTimer;
    }

    private void SubscribeTimer(ExpeditionTimer timer)
    {
        if (_subscribedTimer == timer) return;
        UnsubscribeTimer();
        _subscribedTimer = timer;
        _subscribedTimer.OnFormattedTimeChanged += HandleTimerFormatted;
    }

    private void UnsubscribeTimer()
    {
        if (_subscribedTimer == null) return;
        _subscribedTimer.OnFormattedTimeChanged -= HandleTimerFormatted;
        _subscribedTimer = null;
    }

    private void HandleTimerFormatted(string formatted)
    {
        _displayTime = formatted;
        UpdateTimerUI();
    }

    private void Start()
    {
        // タイマー開始は ExpeditionEvents.OnExpeditionStarted イベント経由に変更。
        // テストシーン（ExpeditionManager なし）での互換性のためフォールバックを残す。
        if (GameServices.Expedition == null)
        {
            StartTimer();
        }
        else
        {
            StopTimer();
        }

        SetWarning("");
    }

    private void Update()
    {
        var timer = ResolveTimer();
        if (ReferenceEquals(timer, _fallbackTimer) && timer.IsRunning)
            timer.Tick(Time.deltaTime);

        ResolveLocalPlayerRefsIfNeeded();
        UpdateVitals();
        UpdateRopeUI();
        UpdateWarning();
    }

    /// <summary>
    /// _localPlayerStamina / _localPlayerHealth は Inspector 未設定（combined ではプレイヤーが実行時生成）。
    /// Player タグから StaminaSystem / PlayerHealthSystem を一度だけ解決し、
    /// 左上のバイタルバーが実際の残量を反映するようにする。
    /// </summary>
    private void ResolveLocalPlayerRefsIfNeeded()
    {
        if (_localPlayerStamina != null && _localPlayerHealth != null) return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        if (_localPlayerStamina == null)
            _localPlayerStamina = player.GetComponentInChildren<StaminaSystem>();
        if (_localPlayerHealth == null)
            _localPlayerHealth = player.GetComponentInChildren<PlayerHealthSystem>();
    }

    // ── タイマー ─────────────────────────────────────────────
    public void StartTimer()
    {
        var timer = ResolveTimer();
        SubscribeTimer(timer);
        timer.Start();
        SetHudVisible(true);
        UpdateTimerUI();
    }

    public void StopTimer()
    {
        ResolveTimer().Stop();
        SetHudVisible(false);
        SetWarning("");
    }

    private void UpdateTimerUI()
    {
        if (_timerLabel == null) return;
        _timerLabel.text = _displayTime;
    }

    public float GetElapsedTime() => ResolveTimer().ElapsedSeconds;

    // ── チェックポイント ──────────────────────────────────────
    // シグネチャは ExpeditionEvents.OnCheckpointReached: Action<int, int> に合わせる
    public void SetCheckpoint(int current, int total)
    {
        _currentCheckpoint = current;
        _totalCheckpoints  = total;

        if (_checkpointLabel != null)
            _checkpointLabel.text = $"チェックポイント {current}/{total}";

        ShowWarning($"チェックポイント {current} 通過！");
    }

    // ── バイタル（体力・気力） ────────────────────────────────
    // 値は VitalsBar に渡すだけ。補間・ダメージ残像・低残量パルスは VitalsBar 側が毎フレーム処理する。
    private void UpdateVitals()
    {
        if (_healthVitals != null)
        {
            _healthVitals.SetTarget(_localPlayerHealth != null ? _localPlayerHealth.HpPercent : 1f);
            _healthVitals.SetNumber(_localPlayerHealth != null ? _localPlayerHealth.CurrentHp : 100f);
        }
        if (_staminaVitals != null)
        {
            _staminaVitals.SetTarget(_localPlayerStamina != null ? _localPlayerStamina.StaminaPercent : 1f);
            _staminaVitals.SetNumber(_localPlayerStamina != null ? _localPlayerStamina.CurrentStamina : 100f);
        }
    }

    // ── ロープ状態 ────────────────────────────────────────────
    private int _lastRopeConnected = -1; // -1=未初期化, 0=未接続, 1=接続

    private void UpdateRopeUI()
    {
        if (_ropeIndicator == null) return;

        bool connected = GameServices.Ropes != null && GameServices.Ropes.HasAnyRope;
        int connectedFlag = connected ? 1 : 0;
        if (connectedFlag == _lastRopeConnected) return;
        _lastRopeConnected = connectedFlag;

        _ropeIndicator.color = connected ? _ropeConnectedColor : _ropeIdleColor;
        if (_ropeLabel != null)
            _ropeLabel.color = connected ? _ropeConnectedColor : UiPalette.CreamDim;
    }

    // ── 遺物リスト ────────────────────────────────────────────
    public void RegisterRelic(RelicBase relic)
    {
        if (_relicListParent == null || _relicListItemPrefab == null) return;

        var go    = Instantiate(_relicListItemPrefab, _relicListParent);
        var entry = go.GetComponent<RelicHudEntry>();
        if (entry != null)
        {
            entry.Initialize(relic);
            _relicEntries.Add(entry);
        }
    }

    // ── 警告 ─────────────────────────────────────────────────
    public void ShowWarning(string message)
    {
        if (_warningLabel == null) return;
        _warningLabel.text    = message;
        _warningLabel.enabled = true;
        _warningTimer         = _warningDisplayTime;
    }

    private void SetWarning(string message)
    {
        if (_warningLabel == null) return;
        _warningLabel.text    = message;
        _warningLabel.enabled = !string.IsNullOrEmpty(message);
    }

    private void UpdateWarning()
    {
        if (_warningTimer <= 0f) return;
        _warningTimer -= Time.deltaTime;
        if (_warningTimer <= 0f)
            SetWarning("");
    }

    private void SetHudVisible(bool visible)
    {
        if (_hudCanvasGroup == null) return;
        _hudCanvasGroup.alpha = visible ? 1f : 0f;
        _hudCanvasGroup.interactable = visible;
        _hudCanvasGroup.blocksRaycasts = visible;
    }
}
