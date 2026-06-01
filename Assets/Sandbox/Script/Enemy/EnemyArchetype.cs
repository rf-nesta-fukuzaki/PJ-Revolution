/// <summary>
/// 敵モンスターの原型（R.E.P.O. の多様な敵を再現）。
/// 感知プロファイルと挙動が異なる。
/// </summary>
public enum EnemyArchetype
{
    Brute,    // 鈍重・高威力・バランス型（視覚＋聴覚）
    Stalker,  // 俊敏・広視野・執拗な追跡型
    Listener, // 盲目に近く聴覚特化。物音（ダッシュ/着地）に強く反応する
}
