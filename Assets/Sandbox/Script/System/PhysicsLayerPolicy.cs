using UnityEngine;

/// <summary>
/// GDD §5.6 — レイヤー衝突マトリクスの除外ペアを実行時に適用する。
///
/// DynamicsManager.asset のマトリクスを直接編集する代わりに、レイヤー名から番号を解決して
/// <see cref="Physics.IgnoreLayerCollision(int,int,bool)"/> で除外する方式。
///   - レイヤー番号が GDD と一致していなくても（本プロジェクトは歴史的にズレている）名前で正しく当たる。
///   - 片方でも未定義のレイヤーはスキップするため安全（誤って Player×Ground 等を無効化しない）。
///   - シーン横断の層再採番・再割当を伴わずに、GDD の意図する衝突挙動を実現する。
///
/// 適用する除外（GDD §5.6 衝突マトリクスのうち、対象レイヤーに実体があり安全なもの）:
///   Rope×Rope        … 自ロープの絡まり防止
///   Relic×Interactable… ロープ引き上げ時にクライミングポイントをすり抜ける
///   Hazard×Rope       … 落石等がロープに引っかからない
///   Item×Rope         … アイテムがロープに干渉しない
///   Item×Interactable … アイテムがクライミングポイントに干渉しない
///   Item×SafeZone     … アイテムはセーフゾーン判定に干渉しない
///
/// Ghost は GhostSystem が幽霊化時に全コライダーを無効化するため、ここでは扱わない。
/// </summary>
public static class PhysicsLayerPolicy
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        Ignore("Rope", "Rope");
        Ignore("Relic", "Interactable");
        Ignore("Hazard", "Rope");
        Ignore("Item", "Rope");
        Ignore("Item", "Interactable");
        Ignore("Item", "SafeZone");
    }

    private static void Ignore(string a, string b)
    {
        int la = LayerMask.NameToLayer(a);
        int lb = LayerMask.NameToLayer(b);
        if (la < 0 || lb < 0) return;              // 片方でも未定義ならスキップ（安全側）
        Physics.IgnoreLayerCollision(la, lb, true);
    }
}
