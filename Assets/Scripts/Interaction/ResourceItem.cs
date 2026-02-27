using UnityEngine;

/// <summary>
/// フィールドに配置するリソースアイテム。IInteractable を実装する。
/// プレイヤーがインタラクトするとステータスを回復（または燃料補充）し、自身を Destroy する。
///
/// [使い方]
///   1. 空 GameObject にこのスクリプトを追加する。
///   2. itemType と restoreAmount を Inspector で設定する。
///   3. Collider を追加して Raycast が当たるようにする。
///
/// [FuelCanister]
///   itemType = FuelCanister のとき、TorchSystem に直接燃料補充する。
/// </summary>
public class ResourceItem : MonoBehaviour, IInteractable
{
    // ─────────────── Inspector ───────────────

    [Header("アイテム設定")]
    [Tooltip("アイテムの種類")]
    [SerializeField] private ResourceItemType itemType = ResourceItemType.Food;

    [Tooltip("回復量 / FuelCanister の場合は燃料補充量")]
    [SerializeField] private float restoreAmount = 30f;

    // ─────────────── プロンプト文字列 ───────────────

    private static readonly string[] PromptTexts =
    {
        "[E] 食料を取得",
        "[E] 酸素タンクを使用",
        "[E] 医療キットを使用",
        "[E] 燃料を補充する",
    };

    // ─────────────── IInteractable ───────────────

    public void Interact(UnityEngine.GameObject interactor)
    {
        if (itemType == ResourceItemType.FuelCanister)
        {
            var torch = interactor != null
                ? interactor.GetComponentInChildren<TorchSystem>()
                : FindFirstObjectByType<TorchSystem>();

            if (torch == null)
            {
                Debug.LogWarning($"[ResourceItem] TorchSystem が見つかりません ({gameObject.name})");
                Destroy(gameObject);
                return;
            }

            torch.RefillFuel(restoreAmount);
            Debug.Log($"[ResourceItem] {interactor?.name} が FuelCanister を使用: 燃料 +{restoreAmount}");
        }
        else
        {
            SurvivalStats stats = interactor != null
                ? interactor.GetComponent<SurvivalStats>()
                : null;

            if (stats == null)
                stats = FindFirstObjectByType<SurvivalStats>();

            if (stats == null)
            {
                Debug.LogWarning($"[ResourceItem] SurvivalStats が見つかりません ({gameObject.name})");
                Destroy(gameObject);
                return;
            }

            StatType targetStat = itemType switch
            {
                ResourceItemType.Food       => StatType.Hunger,
                ResourceItemType.OxygenTank => StatType.Oxygen,
                ResourceItemType.Medkit     => StatType.Health,
                _                           => StatType.Health,
            };

            stats.ApplyStatModification(targetStat, restoreAmount);
            Debug.Log($"[ResourceItem] {interactor?.name} が {itemType} を使用: {targetStat} +{restoreAmount}");
        }

        Destroy(gameObject);
    }

    public string GetPromptText()
    {
        int index = Mathf.Clamp((int)itemType, 0, PromptTexts.Length - 1);
        return PromptTexts[index];
    }
}
