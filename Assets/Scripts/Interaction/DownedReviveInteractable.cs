using UnityEngine;

/// <summary>
/// ダウン中のプレイヤーを救助するための IInteractable コンポーネント。
/// ダウンしているプレイヤーの PlayerPrefab（または子オブジェクト）にアタッチする。
///
/// [動作フロー]
///   1. このコンポーネントが付いたオブジェクトに PlayerInteractor が Raycast でヒット
///   2. IsDowned == true のときのみプロンプトを表示
///   3. E キーで Interact() → SurvivalStats を回復
///   4. HP 回復 → IsDowned = false → PlayerStateManager が Normal に戻す
/// </summary>
public class DownedReviveInteractable : MonoBehaviour, IInteractable
{
    // ─────────────── Inspector ───────────────

    [Header("救助設定")]
    [Tooltip("救助時に回復するHP量（0〜100）")]
    [Range(10f, 100f)]
    [SerializeField] private float reviveHpAmount = 30f;

    [Tooltip("ダウン中のプロンプトテキスト")]
    [SerializeField] private string downedPrompt = "[E] 救助する";

    // ─────────────── 内部参照 ───────────────

    private SurvivalStats _survivalStats;

    // ─────────────── Unity Lifecycle ───────────────

    private void Awake()
    {
        _survivalStats = GetComponentInParent<SurvivalStats>();

        if (_survivalStats == null)
            Debug.LogError("[DownedReviveInteractable] 親に SurvivalStats が見つかりません。");
    }

    // ─────────────── IInteractable ───────────────

    public string GetPromptText()
    {
        if (_survivalStats == null) return null;
        return _survivalStats.IsDowned ? downedPrompt : null;
    }

    public void Interact(UnityEngine.GameObject interactor)
    {
        if (_survivalStats == null)
        {
            Debug.LogWarning("[DownedReviveInteractable] SurvivalStats が null です");
            return;
        }

        if (!_survivalStats.IsDowned)
        {
            Debug.Log("[DownedReviveInteractable] 対象はダウン状態ではありません");
            return;
        }

        _survivalStats.ApplyStatModification(StatType.Health, reviveHpAmount);

        Debug.Log($"[DownedReviveInteractable] {interactor?.name} が {gameObject.transform.parent?.name} を救助: HP +{reviveHpAmount}");
    }
}
