/// <summary>
/// リソースアイテムの種類。
/// インデックスは ResourceItem / PlacedResourceItem の PromptTexts 配列と対応する。
/// </summary>
public enum ResourceItemType
{
    Food        = 0,  // 空腹回復
    OxygenTank  = 1,  // 酸素回復
    Medkit      = 2,  // HP 回復
    FuelCanister = 3, // たいまつ燃料補充
}
