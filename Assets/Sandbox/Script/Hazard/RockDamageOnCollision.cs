using UnityEngine;

/// <summary>
/// 落石に付与する衝突ダメージコンポーネント。
/// プレイヤーに当たると PlayerHealthSystem にダメージを与える。
/// </summary>
public class RockDamageOnCollision : MonoBehaviour
{
    public float Damage { get; set; } = 25f;

    private void OnCollisionEnter(Collision col)
    {
        var health = col.gameObject.GetComponent<PlayerHealthSystem>();
        if (health == null) return;

        float speed = col.relativeVelocity.magnitude;
        float dmg   = Damage * Mathf.Clamp01(speed / 10f);
        health.TakeDamage(dmg);

        Debug.Log($"[Rock] プレイヤーに {dmg:F0} ダメージ");
    }
}
