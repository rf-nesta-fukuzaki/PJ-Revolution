/// <summary>
/// 敵モンスターの行動ステート（PEAK/R.E.P.O. パリティ — 脅威エンカウンター）。
/// Patrol→Investigate→Chase→Attack→Search のFSMで駆動する。
/// </summary>
public enum EnemyState
{
    Patrol,      // 巡回（ホーム周辺をうろつく）
    Investigate, // 物音の発生源を調べに行く
    Chase,       // ターゲットを発見し追跡
    Attack,      // 攻撃間合い内で攻撃
    Search,      // 見失った最終位置を捜索
    Stunned      // ひるみ（フレア等で一時停止）
}
