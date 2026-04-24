/// <summary>
/// スコアトラッキングサービスのインターフェース。
///
/// 依存側は ScoreTracker の具体型ではなくこのインターフェースに依存することで
/// テスト・差し替えが容易になる。
///
/// 実装: ScoreTracker
/// </summary>
public interface IScoreService
{
    // ── プレイヤー登録 ───────────────────────────────────────
    void RegisterPlayer(int id, string name);

    // ── 統計記録 ─────────────────────────────────────────────
    void RecordRopePlacement(int playerId);
    void RecordRelicCarried(int playerId, float distanceDelta);
    void RecordRelicFound(int playerId);
    void RecordRelicDamage(int playerId, float damage);
    void RecordFall(int playerId);
    void RecordItemLost(int playerId);
    void RecordGhostPin(int playerId);
    void RecordTeammateFall(int causerId);
    void RecordShout(int playerId);

    // ── 遺物収集 ─────────────────────────────────────────────
    void RegisterCollectedRelic(RelicBase relic);
    void RecordRelicReturned(int instanceId);

    // ── スコア計算 ────────────────────────────────────────────
    ScoreData BuildResultData(float clearTime, bool allSurvived);
}
