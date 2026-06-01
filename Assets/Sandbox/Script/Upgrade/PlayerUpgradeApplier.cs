using UnityEngine;

/// <summary>
/// プレイヤー生成時に <see cref="UpgradeStore"/> の累積効果を各能力システムへ適用する。
/// 適用はべき等（合計値を SET する）。PlayerHealthSystem から自動付与される。
/// </summary>
[RequireComponent(typeof(PlayerHealthSystem))]
public class PlayerUpgradeApplier : MonoBehaviour
{
    private void Start() => Apply();

    /// <summary>現在のアップグレードレベルに応じた能力ボーナスを適用する。</summary>
    public void Apply()
    {
        GetComponent<PlayerHealthSystem>()?.ApplyMaxHpBonus(UpgradeStore.GetBonus(UpgradeType.MaxHealth));
        GetComponent<StaminaSystem>()?.ApplyMaxStaminaBonus(UpgradeStore.GetBonus(UpgradeType.MaxStamina));
        GetComponent<ExplorerController>()?.ApplySprintSpeedBonus(UpgradeStore.GetBonus(UpgradeType.SprintSpeed));
    }
}
