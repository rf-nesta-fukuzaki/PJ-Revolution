/// <summary>
/// インタラクト可能なオブジェクトが実装するインターフェース。
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// インタラクトされたときに実行される処理。
    /// </summary>
    /// <param name="interactor">インタラクトを行ったプレイヤーの GameObject</param>
    void Interact(UnityEngine.GameObject interactor);

    /// <summary>
    /// プレイヤーが近づいたとき UI に表示するプロンプト文字列を返す。
    /// 例: "[E] アイテムを拾う"
    /// </summary>
    string GetPromptText();
}
