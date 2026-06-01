using UnityEngine;

/// <summary>
/// チェックポイント / リスポーン設定を ScriptableObject で管理する。
/// </summary>
[CreateAssetMenu(fileName = "CheckpointConfig", menuName = "PeakPlunder/World/Checkpoint Config")]
public sealed class CheckpointConfigSO : ScriptableObject
{
    [Header("Respawn")]
    [SerializeField] private float _respawnDelay = 1.5f;
    [SerializeField] private float _fallDeathY = -20f;
    [SerializeField] private float _respawnHeightOffset = 2f;
    [SerializeField] private float _respawnGroundProbeHeight = 120f;
    [SerializeField] private float _respawnGroundProbeDistance = 300f;

    public float RespawnDelay => _respawnDelay;
    public float FallDeathY => _fallDeathY;
    public float RespawnHeightOffset => _respawnHeightOffset;
    public float RespawnGroundProbeHeight => _respawnGroundProbeHeight;
    public float RespawnGroundProbeDistance => _respawnGroundProbeDistance;
}
