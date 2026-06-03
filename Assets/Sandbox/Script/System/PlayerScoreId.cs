using UnityEngine;

/// <summary>
/// プレイヤーのスコア統計を ScoreTracker から一意に引くための「正準スコアID」を解決する。
///
/// 背景（不具合）: ScoreTracker は int キーで個人統計を保持するが、従来は
///   - 登録側が スロット番号(0-3) / clientId
///   - 記録側が 各コンポーネントの GetInstanceID()（しかも PlayerInteraction と
///     PlayerHealthSystem で別値）
/// と ID 体系がバラバラで、個人スコア（発見数・運搬距離・落下数 等）が常にゼロ計上されていた。
/// OwnerClientId はローカル Co-op で全ローカルプレイヤーがホスト(0)所有となり衝突するため不可。
///
/// そこで全 spawn 経路で安定・一意な「プレイヤー root（= PlayerHealthSystem を持つ GameObject）の
/// InstanceID」を正準IDとし、登録・記録の双方をこのリゾルバ経由に統一する。
/// プレイヤーは必ず PlayerHealthSystem を 1 つ持つため、それを anchor にして同じ GameObject を特定する。
///
/// 注: NPC は PlayerHealthSystem を持たない場合があり、また Manager 配下に束ねられて transform.root が
/// 衝突しうるため、NPC 系（NPCController）は従来どおり自身の GetInstanceID() を用いる（内部一貫）。
/// 本リゾルバは「人間プレイヤー」の登録・記録経路にのみ用いる。
/// </summary>
public static class PlayerScoreId
{
    /// <summary>プレイヤー配下のコンポーネント（記録側）から正準IDを解決する。</summary>
    public static int FromMember(Component member)
    {
        if (member == null) return 0;
        var health = member.GetComponentInParent<PlayerHealthSystem>();
        var go = health != null ? health.gameObject : member.transform.root.gameObject;
        return go.GetInstanceID();
    }

    /// <summary>スポーン直後のプレイヤー root GameObject（登録側）から正準IDを解決する。</summary>
    public static int FromRoot(GameObject root)
    {
        if (root == null) return 0;
        var health = root.GetComponentInChildren<PlayerHealthSystem>(true);
        var go = health != null ? health.gameObject : root;
        return go.GetInstanceID();
    }
}
