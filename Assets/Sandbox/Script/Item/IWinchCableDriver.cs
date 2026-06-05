using UnityEngine;

/// <summary>
/// ポータブルウインチが利用するケーブル物理の共通 API。
/// 簡易版（<see cref="WinchCableSystem"/>）とチェーン版（<see cref="WinchCableChain"/>）を差し替え可能にする。
/// </summary>
public interface IWinchCableDriver
{
    bool HasHook       { get; }
    bool IsAttached    { get; }
    bool IsBroken      { get; }
    Rigidbody HookBody     { get; }
    Rigidbody AttachedBody { get; }

    void Configure(Transform anchor, LineRenderer lineRenderer, float maxLength, float reelSpeed);
    bool DeployHook(Vector3 spawnPosition);
    bool TryAttachHookTo(Rigidbody target);
    void Reel(float deltaTime);
    float EstimateTension();
    void BreakCable();
    void RetractAndDestroy();
}
