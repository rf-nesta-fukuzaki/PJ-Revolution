using UnityEngine;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

/// <summary>
/// 落石に付与する衝突ダメージコンポーネント。
/// プレイヤーに当たると PlayerHealthSystem にダメージを与える。
/// </summary>
public class RockDamageOnCollision : MonoBehaviour
{
    public float Damage { get; set; } = 25f;

    // GDD §15.2 — rockfall_impact を岩ごとに一度だけ鳴らす
    private bool _impactPlayed;

    private void OnCollisionEnter(Collision col)
    {
        // GDD §15.2 — rockfall_impact（どの面に着弾しても一度だけ）
        if (!_impactPlayed)
        {
            _impactPlayed = true;
            PPAudioManager.Instance?.PlaySE(SoundId.RockfallImpact, transform.position);
        }

        var health = col.gameObject.GetComponent<PlayerHealthSystem>();
        if (health == null) return;

        // GDD §10.5: セーフゾーン内のプレイヤーには落石ダメージ無効
        var shelter = col.gameObject.GetComponent<ShelterOccupant>();
        if (shelter != null && shelter.IsSheltered)
        {
            Debug.Log("[Rock] セーフゾーン内のためダメージ無効");
            return;
        }

        float speed = col.relativeVelocity.magnitude;
        float dmg   = Damage * Mathf.Clamp01(speed / 10f);
        health.TakeDamage(dmg);

        Debug.Log($"[Rock] プレイヤーに {dmg:F0} ダメージ");
    }
}
