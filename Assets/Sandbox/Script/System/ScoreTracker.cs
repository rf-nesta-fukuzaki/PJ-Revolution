using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// GDD §9 — チーム＆個人スコアトラッカー。
/// リアルタイムで各種統計を記録し、リザルト時に ScoreData を生成する。
/// </summary>
public class ScoreTracker : MonoBehaviour
{
    public static ScoreTracker Instance { get; private set; }

    // ── 報酬設定 ─────────────────────────────────────────────
    private const int  RELIC_BASE_VALUE     = 100;
    private const int  RELIC_INTACT_BONUS   = 50;
    private const int  TEAM_SURVIVAL_BONUS  = 200;
    private const int  ROPE_PLACE_BONUS     = 5;
    private const int  RELIC_FIND_BONUS     = 30;
    private const int  RELIC_CARRY_BONUS    = 2;   // per meter

    // ── プレイヤー統計 ───────────────────────────────────────
    private class PlayerStats
    {
        public string PlayerName;
        public int    RopePlacements;
        public float  RelicCarryDistance;
        public int    RelicsFound;
        public float  RelicDamageDealt;
        public int    FallCount;
        public int    ItemsLost;
        public int    GhostPinsPlaced;
        public int    TeammateFallsCaused;
        public int    ShoutCount;           // 歌う壺ボイチャ妨害による「叫び」回数
    }

    private readonly Dictionary<int, PlayerStats> _stats = new();

    // ── 遺物リスト ────────────────────────────────────────────
    private readonly List<RelicBase> _collectedRelics = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── プレイヤー登録 ───────────────────────────────────────
    public void RegisterPlayer(int id, string name)
    {
        if (_stats.ContainsKey(id)) return;
        _stats[id] = new PlayerStats { PlayerName = name };
    }

    // ── 統計記録 ─────────────────────────────────────────────
    public void RecordRopePlacement(int playerId)
    {
        if (!_stats.TryGetValue(playerId, out var s)) return;
        s.RopePlacements++;
    }

    public void RecordRelicCarried(int playerId, float distanceDelta)
    {
        if (!_stats.TryGetValue(playerId, out var s)) return;
        s.RelicCarryDistance += distanceDelta;
    }

    public void RecordRelicFound(int playerId)
    {
        if (!_stats.TryGetValue(playerId, out var s)) return;
        s.RelicsFound++;
    }

    public void RecordRelicDamage(int playerId, float damage)
    {
        if (!_stats.TryGetValue(playerId, out var s)) return;
        s.RelicDamageDealt += damage;
    }

    public void RecordFall(int playerId)
    {
        if (!_stats.TryGetValue(playerId, out var s)) return;
        s.FallCount++;
    }

    public void RecordItemLost(int playerId)
    {
        if (!_stats.TryGetValue(playerId, out var s)) return;
        s.ItemsLost++;
    }

    public void RecordGhostPin(int playerId)
    {
        if (!_stats.TryGetValue(playerId, out var s)) return;
        s.GhostPinsPlaced++;
    }

    public void RecordTeammateFall(int causerId)
    {
        if (!_stats.TryGetValue(causerId, out var s)) return;
        s.TeammateFallsCaused++;
    }

    public void RecordShout(int playerId)
    {
        if (!_stats.TryGetValue(playerId, out var s)) return;
        s.ShoutCount++;
    }

    // ── 遺物収集 ─────────────────────────────────────────────
    public void RegisterCollectedRelic(RelicBase relic)
    {
        if (!_collectedRelics.Contains(relic))
            _collectedRelics.Add(relic);
    }

    // ── スコア計算 ────────────────────────────────────────────
    public ScoreData BuildResultData(float clearTime, bool allSurvived)
    {
        var data = new ScoreData
        {
            ClearTimeSeconds = clearTime,
            Relics           = new List<RelicBase>(_collectedRelics)
        };

        // チームスコア
        int teamScore = 0;
        foreach (var relic in _collectedRelics)
        {
            teamScore += relic.CurrentValue;
            if (relic.Condition == RelicCondition.Perfect)
                teamScore += RELIC_INTACT_BONUS;
        }
        if (allSurvived)
            teamScore += TEAM_SURVIVAL_BONUS;

        data.TeamScore = teamScore;

        // 個人スコア
        foreach (var kv in _stats)
        {
            var s  = kv.Value;
            int ps = s.RopePlacements    * ROPE_PLACE_BONUS
                   + s.RelicsFound       * RELIC_FIND_BONUS
                   + (int)(s.RelicCarryDistance * RELIC_CARRY_BONUS)
                   + s.GhostPinsPlaced   * 10;

            // ペナルティ
            ps -= (int)(s.RelicDamageDealt * 0.5f);
            ps -= s.ItemsLost * 20;
            ps  = Mathf.Max(0, ps);

            data.PlayerScores.Add(new PlayerScore
            {
                PlayerName          = s.PlayerName,
                IndividualScore     = ps,
                FallCount           = s.FallCount,
                ItemsLost           = s.ItemsLost,
                RelicDamageDealt    = s.RelicDamageDealt,
                GhostContributions  = s.GhostPinsPlaced,
                RopePlacementCount  = s.RopePlacements,
                ShoutCount          = s.ShoutCount
            });
        }

        // 個人スコアでソート
        data.PlayerScores = data.PlayerScores.OrderByDescending(p => p.IndividualScore).ToList();

        Debug.Log($"[Score] チームスコア: {data.TeamScore}pt  遺物: {_collectedRelics.Count}個  タイム: {clearTime:F0}s");
        return data;
    }
}
