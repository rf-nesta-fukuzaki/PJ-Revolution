using UnityEngine;

/// <summary>
/// 遠征フロー管理サービス（GDD §2.1）。
/// ExpeditionManager の具体型への直接依存を排除する。
/// </summary>
public interface IExpeditionService
{
    ExpeditionPhase Phase { get; }
    ExpeditionTimer Timer { get; }

    void StartExpedition();
    void ReturnToBase(bool allSurvived = true);
    void OnCheckpointReached(int checkpointIdx);
    void OnPlayerDied(PlayerHealthSystem player);
    Transform GetRespawnPoint();
    void RegisterDynamicCheckpoint(Transform checkpoint);
}
