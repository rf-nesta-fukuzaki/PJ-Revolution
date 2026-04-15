using UnityEngine;

/// <summary>
/// データ駆動型の遺物パラメータ定義。
/// RelicBase の SerializeField フィールドを外出しにすることで
/// プレハブを触らずにバランス調整できる。
/// </summary>
[CreateAssetMenu(fileName = "RelicDef_NewRelic", menuName = "PeakIdiots/Relic Definition")]
public class RelicDefinitionSO : ScriptableObject
{
    [Header("識別")]
    public string RelicName       = "Unknown Relic";

    [Header("スコア")]
    public int    BaseValue       = 100;

    [Header("耐久")]
    [Range(1f, 500f)]
    public float  MaxHp           = 100f;
    public float  ImpactThreshold = 2f;   // m/s — これ未満の衝突は無視
    public float  DamageMultiplier = 1f;   // 壊れやすさ係数
}
