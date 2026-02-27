using UnityEngine;

/// <summary>
/// 洞窟内に Instantiate で配置するリソースアイテム。
/// NetworkObject を持たず、通常の MonoBehaviour として動作する。
///
/// [必須セットアップ]
///   この GameObject（または子）に Collider が必要。
///   Collider がないと PlayerInteractor の Raycast が当たらず E キーが反応しない。
///
/// [FuelCanister 対応]
///   itemType = FuelCanister のとき、TorchSystem.RefillFuel() を直接呼び燃料を補充する。
/// </summary>
public class PlacedResourceItem : MonoBehaviour, IInteractable
{
    // ─── Inspector ───────────────────────────────────────────────────────

    [Header("アイテム設定")]
    [SerializeField] private ResourceItemType itemType = ResourceItemType.Food;

    [Tooltip("回復量（0〜100 の範囲で加算）／ FuelCanister の場合は燃料補充量")]
    [SerializeField] private float restoreAmount = 30f;

    // ─── プロンプト文字列 ──────────────────────────────────────────────

    private static readonly string[] PromptTexts =
    {
        "[E] 食料を取得",
        "[E] 酸素タンクを使用",
        "[E] 医療キットを使用",
        "[E] 燃料を補充する",
    };

    // ─── Collider 存在チェック ───────────────────────────────────────────

    private void Awake()
    {
        if (GetComponentInChildren<Collider>() == null)
        {
            Debug.LogWarning(
                $"[PlacedResourceItem] '{gameObject.name}' に Collider がありません。" +
                "PlayerInteractor の Raycast が当たらないため E キーが反応しません。");
        }
    }

    // ─── IInteractable ────────────────────────────────────────────────────

    public void Interact(UnityEngine.GameObject interactor)
    {
        if (itemType == ResourceItemType.FuelCanister)
        {
            TorchSystem torch = null;
            if (interactor != null)
                torch = interactor.GetComponentInChildren<TorchSystem>();
            if (torch == null)
                torch = FindFirstObjectByType<TorchSystem>();

            if (torch == null)
            {
                Debug.LogWarning($"[PlacedResourceItem] TorchSystem が見つかりません ({gameObject.name})");
                Destroy(gameObject);
                return;
            }

            torch.RefillFuel(restoreAmount);
            Debug.Log($"[PlacedResourceItem] FuelCanister 使用: 燃料 +{restoreAmount}");
            Destroy(gameObject);
            return;
        }

        SurvivalStats stats = null;

        if (interactor != null)
            stats = interactor.GetComponent<SurvivalStats>();

        if (stats == null)
            stats = FindFirstObjectByType<SurvivalStats>();

        if (stats == null)
        {
            Debug.LogWarning($"[PlacedResourceItem] SurvivalStats が見つかりません ({gameObject.name})");
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
        Debug.Log($"[PlacedResourceItem] {itemType} 使用: {targetStat} +{restoreAmount}");

        Destroy(gameObject);
    }

    public string GetPromptText()
    {
        int index = Mathf.Clamp((int)itemType, 0, PromptTexts.Length - 1);
        return PromptTexts[index];
    }
}
