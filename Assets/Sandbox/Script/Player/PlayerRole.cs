/// <summary>
/// プレイヤーの役割（協力プレイの差別化）。各役割は他と直交するパッシブ特典を持つ。
/// </summary>
public enum PlayerRole
{
    Scout,    // 偵察：周囲の遺物をレーダー探知
    Medic,    // 衛生兵：味方の蘇生が高速
    Vanguard, // 先鋒：被ダメージ軽減
}
