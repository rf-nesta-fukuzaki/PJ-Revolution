using UnityEngine;

/// <summary>ショート/ロングロープ共通の接続 API。</summary>
public interface IShopRopeItem
{
    bool   IsConnected  { get; }
    bool   IsRelicMode  { get; }
    float  RopeLength   { get; }
    float  BreakForce   { get; }

    bool TryConnectToPlayer(int playerIdA, int playerIdB);
    bool TryAttachToRelic(RelicBase relic, int playerId, Vector3 fromPosition);
    bool TryConnectToAnchor(Transform anchor, int playerId, Vector3 fromPosition);
    void CutRope();
    void ApplyPlayerConnectState(int playerIdA, int playerIdB);
    void ApplyRelicAttachState(RelicBase relic, int playerId);
    void ApplyAnchorConnectState(Transform anchor, int playerId);
    void CutRopeLocalOnly();
}
