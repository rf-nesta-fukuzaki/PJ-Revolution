/// <summary>
/// プレイヤーの排他的な上位ステート。
///
/// GhostSystem / RagdollSystem / EmoteSystem など複数コンポーネントで
/// 共有される「信頼できる唯一の真実（SSOT）」として機能する。
/// 遷移規則は PlayerStateMachine.IsValidTransition() に集約。
/// </summary>
public enum PlayerState
{
    Alive,      // 通常行動可能
    Ghost,      // 幽霊（死亡後の偵察モード）
    Ragdoll,    // 高速衝突によるラグドール中
    Emoting,    // エモート再生中（移動不可）
    Boarding,   // ヘリコプター搭乗中
}
