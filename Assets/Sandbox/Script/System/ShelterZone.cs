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

        // GDD §5.6 — セーフゾーンは SafeZone レイヤーに置く（存在する場合）。
        // Player×SafeZone は既定で有効なのでトリガー検出は維持され、PhysicsLayerPolicy の
        // Item×SafeZone 除外が意味を持つ。レイヤー未定義環境では何もしない（安全）。
        int safeLayer = LayerMask.NameToLayer("SafeZone");
        if (safeLayer >= 0) gameObject.layer = safeLayer;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        GameServices.Weather?.AddShelterOccupant(other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        GameServices.Weather?.RemoveShelterOccupant(other.gameObject);
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
