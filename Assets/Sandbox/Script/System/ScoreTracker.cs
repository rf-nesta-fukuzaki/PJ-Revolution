using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// GDD §9 — チーム＆個人スコアトラッカー。
/// リアルタイムで各種統計を記録し、リザルト時に ScoreData を生成する。
///
/// スコア設定は ScoreConfigSO に外部化されており、Inspector から調整可能。
/// ScoreConfigSO が未設定の場合、デフォルト値を持つ内部インスタンスを使用する。
/// </summary>
public class ScoreTracker : MonoBehaviour, IScoreService
{
    public static ScoreTracker Instance { get; private set; }

    // ── データ駆動設定 ────────────────────────────────────────
    [Header("スコア設定（ScriptableObject）")]
    [SerializeField] private ScoreConfigSO _config;

    /// <summary>_config が未設定の場合のデフォルト値フォールバック。</summary>
    private ScoreConfigSO Config
    {
        get
        {
            if (_config != null) return _config;

            Debug.LogWarning("[ScoreTracker] ScoreConfigSO が未設定です。デフォルト値を使用します。");
            _config = ScriptableObject.CreateInstance<ScoreConfigSO>();
            return _config;
        }
    }

    // ── プレイヤー統計 ───────────────────────────────────────
    private sealed class PlayerStats
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

    private readonly Dictionary<int, PlayerStats> _stats          = new();
    private readonly List<RelicBase>              _collectedRelics = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── プレイヤー登録 ───────────────────────────────────────
    /// <summary>
    /// プレイヤーをスコアトラッカーに登録する。
    /// 前提条件: name は null/空でないこと。
    /// </summary>
    public void RegisterPlayer(int id, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Debug.LogError($"[Contract] ScoreTracker.RegisterPlayer: name が null または空です (id={id})");
            return;
        }

        if (_stats.ContainsKey(id)) return;
        _stats[id] = new PlayerStats { PlayerName = name };
    }

    // ── 統計記録 ─────────────────────────────────────────────
    public void RecordRopePlacement(int playerId)
    {
        if (!TryGetStats(playerId, out var s)) return;
        s.RopePlacements++;
    }

    public void RecordRelicCarried(int playerId, float distanceDelta)
    {
        if (distanceDelta < 0f)
        {
            Debug.LogWarning($"[Contract] RecordRelicCarried: distanceDelta が負の値 ({distanceDelta})。無視します。");
            return;
        }

        if (!TryGetStats(playerId, out var s)) return;
        s.RelicCarryDistance += distanceDelta;
    }

    public void RecordRelicFound(int playerId)
    {
        if (!TryGetStats(playerId, out var s)) return;
        s.RelicsFound++;
    }

    public void RecordRelicDamage(int playerId, float damage)
    {
        if (damage < 0f)
        {
            Debug.LogError($"[Contract] RecordRelicDamage: damage が負の値 ({damage})");
            return;
        }

        if (!TryGetStats(playerId, out var s)) return;
        s.RelicDamageDealt += damage;
    }

    public void RecordFall(int playerId)
    {
        if (!TryGetStats(playerId, out var s)) return;
        s.FallCount++;
    }

    public void RecordItemLost(int playerId)
    {
        if (!TryGetStats(playerId, out var s)) return;
        s.ItemsLost++;
    }

    public void RecordGhostPin(int playerId)
    {
        if (!TryGetStats(playerId, out var s)) return;
        s.GhostPinsPlaced++;
    }

    public void RecordTeammateFall(int causerId)
    {
        if (!TryGetStats(causerId, out var s)) return;
        s.TeammateFallsCaused++;
    }

    public void RecordShout(int playerId)
    {
        if (!TryGetStats(playerId, out var s)) return;
        s.ShoutCount++;
    }

    // ── 遺物収集 ─────────────────────────────────────────────
    public void RegisterCollectedRelic(RelicBase relic)
    {
        if (relic == null)
        {
            Debug.LogError("[Contract] RegisterCollectedRelic: relic が null です");
            return;
        }

        if (!_collectedRelics.Contains(relic))
            _collectedRelics.Add(relic);
    }

    /// <summary>遺物が帰還エリアに持ち込まれたことを記録する（ReturnZone から呼ぶ）。</summary>
    public void RecordRelicReturned(int instanceId)
    {
        // 帰還遺物は RegisterCollectedRelic() で既にスコア対象に登録済み。
        // ここでは統計ログのみ（将来のクリアボーナス計算に使用可能）。
        Debug.Log($"[ScoreTracker] 遺物 (ID {instanceId}) 帰還確定");
    }

    // ── スコア計算 ────────────────────────────────────────────
    /// <summary>
    /// 現在の統計からリザルトデータを構築する。
    /// 前提条件: clearTime >= 0
    /// </summary>
    public ScoreData BuildResultData(float clearTime, bool allSurvived)
    {
        if (clearTime < 0f)
        {
            Debug.LogError($"[Contract] BuildResultData: clearTime が負の値 ({clearTime})");
            clearTime = 0f;
        }

        var cfg  = Config;
        var data = new ScoreData
        {
            ClearTimeSeconds = clearTime,
            Relics           = new List<RelicBase>(_collectedRelics)
        };

        // チームスコア（ScoreConfigSO の値を使用）
        int teamScore = 0;
        foreach (var relic in _collectedRelics)
        {
            teamScore += relic.CurrentValue;
            if (relic.Condition == RelicCondition.Perfect)
                teamScore += cfg.RelicIntactBonus;
        }
        if (allSurvived)
            teamScore += cfg.TeamSurvivalBonus;

        data.TeamScore = teamScore;

        // 個人スコア
        foreach (var kv in _stats)
        {
            var s  = kv.Value;
            int ps = s.RopePlacements    * cfg.RopePlaceBonus
                   + s.RelicsFound       * cfg.RelicFindBonus
                   + (int)(s.RelicCarryDistance * cfg.RelicCarryBonus)
                   + s.GhostPinsPlaced   * cfg.GhostPinBonus;

            // ペナルティ
            ps -= (int)(s.RelicDamageDealt * cfg.RelicDamagePenaltyRate);
            ps -= s.ItemsLost * cfg.ItemLostPenalty;
            ps  = Mathf.Max(0, ps);

            data.PlayerScores.Add(new PlayerScore
            {
                PlayerName         = s.PlayerName,
                IndividualScore    = ps,
                FallCount          = s.FallCount,
                ItemsLost          = s.ItemsLost,
                RelicDamageDealt   = s.RelicDamageDealt,
                GhostContributions = s.GhostPinsPlaced,
                RopePlacementCount = s.RopePlacements,
                ShoutCount         = s.ShoutCount
            });
        }

        data.PlayerScores = data.PlayerScores.OrderByDescending(p => p.IndividualScore).ToList();

        Debug.Log($"[Score] チームスコア: {data.TeamScore}pt  遺物: {_collectedRelics.Count}個  タイム: {clearTime:F0}s");
        return data;
    }

    // ── ヘルパー ─────────────────────────────────────────────
    private bool TryGetStats(int playerId, out PlayerStats stats)
    {
        if (_stats.TryGetValue(playerId, out stats)) return true;

        Debug.LogWarning($"[ScoreTracker] playerId={playerId} は未登録です。RegisterPlayer() を先に呼んでください。");
        return false;
    }
}
