using UnityEngine;

/// <summary>
/// 恒久アップグレード 1 種類の定義。ScriptableObject で作成し UpgradeSystem に登録する。
///
/// [使い方]
///   Assets メニュー → Create → PJ-Revolution → UpgradeDefinition で生成する。
///   GemCostPerLevel の配列長は MaxLevel と一致させること（MaxLevel=5 なら要素 5 個）。
/// </summary>
[CreateAssetMenu(fileName = "NewUpgrade", menuName = "PJ-Revolution/UpgradeDefinition")]
public class UpgradeDefinition : ScriptableObject
{
    [Tooltip("ユニーク ID（PlayerPrefs のキーに使用）。変更不可。")]
    public string UpgradeId;

    [Tooltip("UI 表示名")]
    public string DisplayName;

    [Tooltip("UI 説明文")]
    [TextArea(2, 4)]
    public string Description;

    [Tooltip("最大レベル（GemCostPerLevel の配列長と一致させること）")]
    [Range(1, 10)]
    public int MaxLevel = 5;

    [Tooltip("各レベルに必要な宝石数。配列長 = MaxLevel。")]
    public int[] GemCostPerLevel;

    [Tooltip("このアップグレードが変化させるパラメータ種別")]
    public UpgradeType Type;
}

/// <summary>
/// アップグレードが影響するパラメータ種別。
/// UpgradeSystem.ApplyAllUpgrades() で各システムのセッターに振り分ける。
/// </summary>
public enum UpgradeType
{
    /// <summary>SurvivalStats の最大 HP</summary>
    MaxHealth,

    /// <summary>SurvivalStats の最大酸素</summary>
    MaxOxygen,

    /// <summary>SurvivalStats の最大空腹</summary>
    MaxHunger,

    /// <summary>PlayerMovement の移動速度</summary>
    MoveSpeed,

    /// <summary>PlayerMovement のジャンプ力</summary>
    JumpForce,

    /// <summary>TorchSystem の最大燃料</summary>
    TorchFuel,

    /// <summary>InventorySystem の最大重量</summary>
    CarryWeight,
}
