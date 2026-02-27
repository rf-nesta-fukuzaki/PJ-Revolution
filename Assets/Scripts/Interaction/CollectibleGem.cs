using UnityEngine;

/// <summary>
/// 洞窟内に配置する収集可能な宝石オブジェクト。IInteractable を実装する。
/// プレイヤーが E キーでインタラクトすると GameManager の宝石カウントを加算し、
/// 自身を Destroy する。
/// </summary>
public class CollectibleGem : MonoBehaviour, IInteractable
{
    // ─────────────── Inspector ───────────────

    [Header("宝石設定")]
    [Tooltip("1度に収集できる宝石数")]
    [SerializeField] private int gemValue = 1;

    [Tooltip("収集時に表示するプロンプトテキスト")]
    [SerializeField] private string promptText = "[E] 宝石を収集する";

    // ─────────────── IInteractable ───────────────

    public void Interact(UnityEngine.GameObject interactor)
    {
        GameManager.Instance?.AddGem(gemValue);

        Debug.Log($"[CollectibleGem] {interactor?.name} が宝石を収集: +{gemValue} " +
                  $"(合計: {GameManager.Instance?.CollectedGems}個)");

        Destroy(gameObject);
    }

    public string GetPromptText() => promptText;
}
