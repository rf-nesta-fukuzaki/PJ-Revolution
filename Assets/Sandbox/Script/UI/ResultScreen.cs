using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

/// <summary>
/// GDD §2.2 / §9 / §14.6 — リザルト画面。
/// 6 段階の自動演出でチームスコア→遺物→個人スコア→称号→コスメ解放→メニュー戻りを順に見せる。
/// 各段階は時間経過で自動進行。Space / Aボタンで次の段階へスキップできる。
/// </summary>
public class ResultScreen : MonoBehaviour
{
    [Header("パネル")]
    [SerializeField] private GameObject _panel;

    [Header("チームスコア")]
    [SerializeField] private TextMeshProUGUI _teamScoreLabel;
    [SerializeField] private TextMeshProUGUI _relicSummaryLabel;
    [SerializeField] private TextMeshProUGUI _clearTimeLabel;

    [Header("個人スコア行 (PlayerResultRow × 4)")]
    [SerializeField] private Transform       _playerRowParent;
    [SerializeField] private GameObject      _playerRowPrefab;

    [Header("称号エリア")]
    [SerializeField] private Transform       _titleRowParent;
    [SerializeField] private GameObject      _titleRowPrefab;

    [Header("コスメ解放表示（任意）")]
    [SerializeField] private GameObject      _cosmeticGroup;
    [SerializeField] private TextMeshProUGUI _cosmeticLabel;

    [Header("ボタン")]
    [SerializeField] private Button          _retryButton;
    [SerializeField] private Button          _returnBaseButton;

    [Header("ステージコンテナ (任意: 段階ごとの可視性制御)")]
    [SerializeField] private GameObject _stageTeamScore;
    [SerializeField] private GameObject _stageRelics;
    [SerializeField] private GameObject _stagePlayers;
    [SerializeField] private GameObject _stageTitles;
    [SerializeField] private GameObject _stageCosmetic;
    [SerializeField] private GameObject _stageButtons;

    [Header("演出タイミング (秒)")]
    [SerializeField] private float _stageTeamDuration     = 3f;
    [SerializeField] private float _stageRelicsDuration   = 5f;
    [SerializeField] private float _stagePlayersDuration  = 5f;
    [SerializeField] private float _stageTitlesDuration   = 10f;
    [SerializeField] private float _stageCosmeticDuration = 3f;

    // GDD §12.5 コメディ称号（全 8 種 + フォールバック）
    private const string TITLE_GRAVITY     = "重力の友";          // 落下最多
    private const string TITLE_EQUIPMENT   = "装備マイスター";    // 装備喪失最多
    private const string TITLE_CRUSHER     = "遺物クラッシャー";  // 遺物ダメージ最多
    private const string TITLE_VOLUME_KING = "声量キング";        // ボイチャ/叫び累積最多
    private const string TITLE_SHADOW_MVP  = "影のMVP";           // 幽霊貢献（ピン数）最多
    private const string TITLE_IRON_HUNTER = "鉄人ハンター";      // 落下ゼロ & 生還
    private const string TITLE_RELIC_MASTER= "遺物マスター";      // 遺物発見/運搬最多
    private const string TITLE_ROPE_MASTER = "ロープの達人";      // ロープ設置最多
    private const string TITLE_FALLBACK    = "参加賞：お疲れ山でした";

    private readonly List<string> _unlockedThisResult = new();
    private Coroutine             _sequenceRoutine;
    private bool                  _skipRequested;
    private bool                  _subscribedToUnlock;

    private void Start()
    {
        if (_panel != null) _panel.SetActive(false);

        _retryButton?.onClick.AddListener(OnRetry);
        _returnBaseButton?.onClick.AddListener(OnReturnBase);
    }

    private void OnDestroy()
    {
        UnsubscribeFromCosmetics();
    }

    private void Update()
    {
        // GDD §14.6: Space / A でスキップ。段階シーケンス実行中のみ有効。
        if (_sequenceRoutine == null) return;

        if (Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.JoystickButton0))
        {
            _skipRequested = true;
        }
    }

    // ── 表示 ─────────────────────────────────────────────────
    public void Show(ScoreData score, bool allSurvived = false)
    {
        Debug.Assert(score != null, "[Contract] ResultScreen.Show: score が null です");
        if (_panel != null) _panel.SetActive(true);

        PopulateTeamScore(score);
        PopulatePlayerRows(score);
        PopulateTitles(score);

        // プロフィールに遠征結果を保存（GameServices 経由）
        GameServices.Save?.UpdateFromResult(score, allSurvived);

        // コスメ解放イベントを購読してから ProcessResultRewards を呼ぶ。
        // 新規解放分だけを Stage 5 で表示したいので順序が重要。
        _unlockedThisResult.Clear();
        SubscribeToCosmetics();
        GameServices.Cosmetics?.ProcessResultRewards(score);

        if (_sequenceRoutine != null) StopCoroutine(_sequenceRoutine);
        _sequenceRoutine = StartCoroutine(PlayResultSequence(score));
    }

    // ── 6段階シーケンス (GDD §14.6) ───────────────────────────
    private IEnumerator PlayResultSequence(ScoreData score)
    {
        yield return FadePanelIn();

        // 初期状態: 全ステージ非表示
        SetStageVisible(_stageTeamScore, false);
        SetStageVisible(_stageRelics,    false);
        SetStageVisible(_stagePlayers,   false);
        SetStageVisible(_stageTitles,    false);
        SetStageVisible(_stageCosmetic,  false);
        SetStageVisible(_stageButtons,   false);

        // Stage 1: チームスコア（0→TeamScore カウントアップ）
        SetStageVisible(_stageTeamScore, true);
        yield return CountUpTeamScore(score.TeamScore, _stageTeamDuration);

        // Stage 2: 遺物一覧
        SetStageVisible(_stageRelics, true);
        yield return WaitSkippable(_stageRelicsDuration);

        // Stage 3: 個人スコアボード
        SetStageVisible(_stagePlayers, true);
        yield return WaitSkippable(_stagePlayersDuration);

        // Stage 4: 称号授与
        SetStageVisible(_stageTitles, true);
        // GDD §15.2 — result_title（称号ステージ開始のファンファーレ）
        PPAudioManager.Instance?.PlaySE2D(SoundId.ResultTitle);
        yield return WaitSkippable(_stageTitlesDuration);

        // Stage 5: コスメ解放（アンロックがあれば表示、なければスキップ）
        if (_unlockedThisResult.Count > 0)
        {
            SetStageVisible(_stageCosmetic, true);
            RefreshCosmeticLabel();
            yield return WaitSkippable(_stageCosmeticDuration);
        }

        // Stage 6: ボタン表示
        SetStageVisible(_stageButtons, true);
        UnsubscribeFromCosmetics();
        _sequenceRoutine = null;
    }

    /// <summary>
    /// 指定秒数待機するが、_skipRequested が立った時点で即座に抜ける。
    /// Time.unscaledDeltaTime を使うため、Pause(Time.timeScale=0) 中でも正しく流れる。
    /// </summary>
    private IEnumerator WaitSkippable(float duration)
    {
        float t = 0f;
        _skipRequested = false;
        while (t < duration && !_skipRequested)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        _skipRequested = false;
    }

    /// <summary>
    /// チームスコアを 0 から target まで duration 秒でカウントアップ表示する。
    /// スキップ時は最終値にスナップする。
    /// </summary>
    private IEnumerator CountUpTeamScore(int target, float duration)
    {
        if (_teamScoreLabel == null) { yield return WaitSkippable(duration); yield break; }

        // GDD §15.2 — result_count（カウントアップ開始）
        PPAudioManager.Instance?.PlaySE2D(SoundId.ResultCount);

        _skipRequested = false;
        float t = 0f;
        while (t < duration && !_skipRequested)
        {
            t += Time.unscaledDeltaTime;
            int shown = Mathf.RoundToInt(Mathf.Lerp(0f, target, Mathf.Clamp01(t / duration)));
            _teamScoreLabel.text = $"チームスコア: {shown} pt";
            yield return null;
        }
        _teamScoreLabel.text = $"チームスコア: {target} pt";
        _skipRequested = false;
    }

    // ── 各セクションの内容埋め ─────────────────────────────────
    private void PopulateTeamScore(ScoreData score)
    {
        // TeamScoreLabel はシーケンスでカウントアップするため初期値は空。
        if (_teamScoreLabel != null)
            _teamScoreLabel.text = "チームスコア: 0 pt";

        if (_relicSummaryLabel != null)
        {
            int intact = 0;
            foreach (var r in score.Relics)
                if (r != null && r.Condition != RelicCondition.Destroyed) intact++;
            _relicSummaryLabel.text = $"遺物 {intact}/{score.Relics.Count} 個 無事帰還";
        }

        if (_clearTimeLabel != null)
        {
            int   min = (int)score.ClearTimeSeconds / 60;
            float sec = score.ClearTimeSeconds % 60f;
            _clearTimeLabel.text = $"タイム: {min:00}:{sec:00.00}";
        }
    }

    private void PopulatePlayerRows(ScoreData score)
    {
        if (_playerRowParent == null || _playerRowPrefab == null) return;

        foreach (Transform child in _playerRowParent)
            Destroy(child.gameObject);

        foreach (var ps in score.PlayerScores)
        {
            var go  = Instantiate(_playerRowPrefab, _playerRowParent);
            var row = go.GetComponent<PlayerResultRow>();
            row?.Populate(ps);
        }
    }

    private void PopulateTitles(ScoreData score)
    {
        if (_titleRowParent == null || _titleRowPrefab == null) return;

        foreach (Transform child in _titleRowParent)
            Destroy(child.gameObject);

        var titles = AssignTitles(score);
        foreach (var t in titles)
        {
            var go  = Instantiate(_titleRowPrefab, _titleRowParent);
            var row = go.GetComponent<TitleRowEntry>();
            row?.Set(t.playerName, t.title);
        }
    }

    // ── コメディ称号割り当て（GDD §12.5）──────────────────────
    /// <summary>
    /// 全8種の称号を各メトリクス最大値プレイヤーに付与する。
    /// タイブレーク: 同値トップが複数いれば全員に付与。
    /// 最大値が 0 の称号はスキップ（該当プレイヤーなし）。
    /// 「鉄人ハンター」は Survived==true 全員に付与。
    /// どの称号も得られなかったプレイヤーには「参加賞」フォールバックを付与。
    /// </summary>
    private List<(string playerName, string title)> AssignTitles(ScoreData score)
    {
        var result  = new List<(string, string)>();
        var players = score.PlayerScores;
        if (players == null || players.Count == 0) return result;

        var titled = new HashSet<string>();

        AwardTopInt(players, p => p.FallCount,          TITLE_GRAVITY,      result, titled);
        AwardTopInt(players, p => p.ItemsLost,          TITLE_EQUIPMENT,    result, titled);
        AwardTopFloat(players, p => p.RelicDamageDealt, TITLE_CRUSHER,      result, titled);
        AwardTopInt(players, p => p.ShoutCount,         TITLE_VOLUME_KING,  result, titled);
        AwardTopInt(players, p => p.GhostContributions, TITLE_SHADOW_MVP,   result, titled);
        AwardTopInt(players, p => p.RelicsFoundCount,   TITLE_RELIC_MASTER, result, titled);
        AwardTopInt(players, p => p.RopePlacementCount, TITLE_ROPE_MASTER,  result, titled);

        foreach (var ps in players)
        {
            if (ps.Survived)
            {
                result.Add((ps.PlayerName, TITLE_IRON_HUNTER));
                titled.Add(ps.PlayerName);
            }
        }

        foreach (var ps in players)
        {
            if (!titled.Contains(ps.PlayerName))
                result.Add((ps.PlayerName, TITLE_FALLBACK));
        }

        return result;
    }

    /// <summary>
    /// int メトリクスで最大値を持つプレイヤー全員に称号を付与する。
    /// 最大値が 0 の場合は誰にも付与しない（GDD §12.5 スキップ規則）。
    /// </summary>
    private static void AwardTopInt(
        List<PlayerScore> players,
        System.Func<PlayerScore, int> selector,
        string title,
        List<(string, string)> result,
        HashSet<string> titled)
    {
        int bestVal = 0;
        for (int i = 0; i < players.Count; i++)
        {
            int v = selector(players[i]);
            if (v > bestVal) bestVal = v;
        }
        if (bestVal <= 0) return;

        for (int i = 0; i < players.Count; i++)
        {
            if (selector(players[i]) == bestVal)
            {
                result.Add((players[i].PlayerName, title));
                titled.Add(players[i].PlayerName);
            }
        }
    }

    /// <summary>
    /// float メトリクス用の最大値付与バリアント。浮動小数点の比較には小さい許容誤差を使う。
    /// </summary>
    private static void AwardTopFloat(
        List<PlayerScore> players,
        System.Func<PlayerScore, float> selector,
        string title,
        List<(string, string)> result,
        HashSet<string> titled)
    {
        float bestVal = 0f;
        for (int i = 0; i < players.Count; i++)
        {
            float v = selector(players[i]);
            if (v > bestVal) bestVal = v;
        }
        if (bestVal <= 0f) return;

        const float epsilon = 0.0001f;
        for (int i = 0; i < players.Count; i++)
        {
            if (Mathf.Abs(selector(players[i]) - bestVal) <= epsilon)
            {
                result.Add((players[i].PlayerName, title));
                titled.Add(players[i].PlayerName);
            }
        }
    }

    // ── コスメ解放購読 ───────────────────────────────────────
    private void SubscribeToCosmetics()
    {
        if (_subscribedToUnlock) return;
        var cm = GameServices.Cosmetics;
        if (cm == null) return;
        cm.OnCosmeticUnlocked += OnCosmeticUnlocked;
        _subscribedToUnlock = true;
    }

    private void UnsubscribeFromCosmetics()
    {
        if (!_subscribedToUnlock) return;
        var cm = GameServices.Cosmetics;
        if (cm != null) cm.OnCosmeticUnlocked -= OnCosmeticUnlocked;
        _subscribedToUnlock = false;
    }

    private void OnCosmeticUnlocked(string id)
    {
        if (!_unlockedThisResult.Contains(id))
            _unlockedThisResult.Add(id);
    }

    private void RefreshCosmeticLabel()
    {
        if (_cosmeticLabel == null) return;
        if (_unlockedThisResult.Count == 0) { _cosmeticLabel.text = string.Empty; return; }

        var sb = new System.Text.StringBuilder();
        sb.Append("NEW! 新しいコスメが解放されました:\n");
        for (int i = 0; i < _unlockedThisResult.Count; i++)
            sb.Append("・").Append(_unlockedThisResult[i]).Append('\n');
        _cosmeticLabel.text = sb.ToString();
    }

    // ── 可視性ヘルパー ────────────────────────────────────────
    private static void SetStageVisible(GameObject stage, bool visible)
    {
        if (stage != null) stage.SetActive(visible);
    }

    // ── フェードイン ─────────────────────────────────────────
    private IEnumerator FadePanelIn()
    {
        var cg = _panel != null ? _panel.GetComponent<CanvasGroup>() : null;
        if (cg == null) yield break;

        cg.alpha = 0f;
        float t = 0f;
        while (t < 1f)
        {
            t         += Time.unscaledDeltaTime * 2f;
            cg.alpha   = t;
            yield return null;
        }
        cg.alpha = 1f;
    }

    // ── ボタン ────────────────────────────────────────────────
    private void OnRetry()       => UnityEngine.SceneManagement.SceneManager.LoadScene(
                                       UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    private void OnReturnBase()  => UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
}

// ScoreData と PlayerScore は Assets/Sandbox/Script/System/ScoreData.cs に移動済み
