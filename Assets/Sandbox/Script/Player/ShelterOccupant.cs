using UnityEngine;

/// <summary>
/// GDD §10.5 — セーフゾーン（シェルター）占有状態トラッカー。
///
/// プレイヤーに付与し、<see cref="ShelterZone"/> への出入りを
/// OnTriggerEnter / OnTriggerExit でカウントする。複数のシェルターが
/// 重なっていても正しく機能するよう count ベースで管理する。
///
/// StaminaSystem・RockDamageOnCollision などがこのコンポーネントを参照して
/// セーフゾーン効果（スタミナ回復 2 倍・落石無効）を適用する。
///
/// FrostbiteDamage / RelicFreezeDamage は独自に ShelterZone を監視するため
/// このクラスは使わない。将来的には統合可能。
/// </summary>
public class ShelterOccupant : MonoBehaviour
{
    private int _shelterCount;

    /// <summary>現在いずれかの ShelterZone 内にいるか。</summary>
    public bool IsSheltered => _shelterCount > 0;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<ShelterZone>() != null)
            _shelterCount++;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<ShelterZone>() != null)
            _shelterCount = Mathf.Max(0, _shelterCount - 1);
    }
}
