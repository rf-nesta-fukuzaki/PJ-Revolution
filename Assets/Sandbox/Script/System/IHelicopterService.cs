using UnityEngine;

/// <summary>
/// ヘリコプター呼び出し・搭乗サービスのインターフェース。
/// FlareGunItem / PlayerInteraction が依存する。
///
/// 実装: HelicopterController
/// </summary>
public interface IHelicopterService
{
    /// <summary>搭乗受付フェーズ中かどうか。</summary>
    bool IsBoarding { get; }

    /// <summary>ヘリパッドのワールド座標。</summary>
    Vector3 HelipadPosition { get; }

    /// <summary>フレアガン上空発射 → ヘリを呼び出す。</summary>
    void CallHelicopter(Vector3 requestPosition);

    /// <summary>プレイヤーを搭乗させる。</summary>
    void BoardPlayer(PlayerHealthSystem player);
}
