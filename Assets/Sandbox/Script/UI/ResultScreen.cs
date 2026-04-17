using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GDD §2.2 / §9 — リザルト画面。
/// チームスコア + 個人スコア + コメディ称号を表示。
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

    [Header("ボタン")]
    [SerializeField] private Button          _retryButton;
    [SerializeField] private Button          _returnBaseButton;

    private static readonly string[] COMEDY_TITLES = new[]
    {
        "崖から落ちた回数1位",
        "装備を一番失くした人",
        "遺物を一番ぶつけた人",
        "一番叫んだ人",
        "影のMVP",
        "チームを一番引っ張った人（物理）",
        "一番役に立たなかった人",
        "最初にロープを切った人"
    };

    private void Start()
    {
        if (_panel != null) _panel.SetActive(false);

        _retryButton?.onClick.AddListener(OnRetry);
        _returnBaseButton?.onClick.AddListener(OnReturnBase);
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

        // リザルトに基づいてコスメティックを解放
        GameServices.Cosmetics?.ProcessResultRewards(score);

        StartCoroutine(AnimateIn());
    }

    private void PopulateTeamScore(ScoreData score)
    {
        if (_teamScoreLabel != null)
            _teamScoreLabel.text = $"チームスコア: {score.TeamScore} pt";

        if (_relicSummaryLabel != null)
        {
            int intact = 0;
            foreach (var r in score.Relics)
                if (r.Condition != RelicCondition.Destroyed) intact++;
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

        // 各プレイヤーにコメディ称号を割り当て
        var titles = AssignTitles(score);
        foreach (var t in titles)
        {
            var go  = Instantiate(_titleRowPrefab, _titleRowParent);
            var row = go.GetComponent<TitleRowEntry>();
            row?.Set(t.playerName, t.title);
        }
    }

    // ── コメディ称号割り当て ──────────────────────────────────
    private List<(string playerName, string title)> AssignTitles(ScoreData score)
    {
        var result  = new List<(string, string)>();
        var players = score.PlayerScores;
        if (players == null || players.Count == 0) return result;

        // LINQ を使わず for ループで最大値プレイヤーを探す（ホットパス回避）
        var topFall  = FindMax(players, p => p.FallCount);
        var topLost  = FindMax(players, p => p.ItemsLost);
        var topDmg   = FindMax(players, p => (int)p.RelicDamageDealt);
        var topShout = FindMax(players, p => p.ShoutCount);

        // 落下1位
        result.Add((topFall.PlayerName, COMEDY_TITLES[0]));

        // 装備喪失1位（落下1位と別人の場合のみ）
        if (topLost != topFall)
            result.Add((topLost.PlayerName, COMEDY_TITLES[1]));

        // 遺物破損1位
        if (players.Count > 1)
            result.Add((topDmg.PlayerName, COMEDY_TITLES[2]));

        // 幽霊コントリビューション（1回以上ピンを打った最初の人）
        foreach (var ps in players)
        {
            if (ps.GhostContributions > 0)
            {
                result.Add((ps.PlayerName, COMEDY_TITLES[4]));
                break;
            }
        }

        // 一番叫んだ人（歌う壺ボイチャ妨害回数ベース）
        if (topShout.ShoutCount > 0)
            result.Add((topShout.PlayerName, COMEDY_TITLES[3]));

        return result;
    }

    private static PlayerScore FindMax(List<PlayerScore> list, System.Func<PlayerScore, int> selector)
    {
        PlayerScore best = list[0];
        int         bestVal = selector(best);
        for (int i = 1; i < list.Count; i++)
        {
            int v = selector(list[i]);
            if (v > bestVal) { bestVal = v; best = list[i]; }
        }
        return best;
    }

    // ── アニメーション ────────────────────────────────────────
    private IEnumerator AnimateIn()
    {
        var cg = _panel?.GetComponent<CanvasGroup>();
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
