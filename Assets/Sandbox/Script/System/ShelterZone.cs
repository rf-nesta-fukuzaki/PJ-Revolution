using UnityEngine;

/// <summary>
/// 天候シェルターゾーンのマーカーコンポーネント。
/// FrostbiteDamage / RelicFreezeDamage が OnTrigger でこれを検出し
/// 凍傷・遺物凍結ダメージを無効化する。
///
/// 使用例：
///   - BivouacTentItem の展開時にテントオブジェクトへ付与
///   - セーフゾーン（洞窟・建物）の BoxCollider オブジェクトへ付与
/// </summary>
[RequireComponent(typeof(Collider))]
public class ShelterZone : MonoBehaviour
{
    private void Awake()
    {
        // 物理演算には影響しない Trigger として機能する
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.2f);
        var col = GetComponent<Collider>();
        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawSphere(transform.TransformPoint(sphere.center), sphere.radius * transform.lossyScale.x);
        }
    }
}
