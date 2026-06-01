using UnityEngine;

/// <summary>
/// GDD §3.1 — 登攀物理パラメータを ScriptableObject で管理する。
/// </summary>
[CreateAssetMenu(fileName = "ClimbingConfig", menuName = "PeakPlunder/Player/Climbing Config")]
public sealed class ClimbingConfigSO : ScriptableObject
{
    [Header("検出")]
    [SerializeField] private float _detectionRadius = 2.5f;

    [Header("物理")]
    [SerializeField] private float _pullSpeed = 4f;
    [SerializeField] private float _holdGravityScale = 0.2f;
    [SerializeField] private float _releaseImpulse = 3f;
    [SerializeField] private float _holdHeightOffset = 0.8f;
    [SerializeField] private float _verticalInputScale = 0.5f;

    public float DetectionRadius => _detectionRadius;
    public float PullSpeed => _pullSpeed;
    public float HoldGravityScale => _holdGravityScale;
    public float ReleaseImpulse => _releaseImpulse;
    public float HoldHeightOffset => _holdHeightOffset;
    public float VerticalInputScale => _verticalInputScale;

    private void OnValidate()
    {
        _detectionRadius = Mathf.Max(0.1f, _detectionRadius);
        _pullSpeed = Mathf.Max(0.1f, _pullSpeed);
        _holdGravityScale = Mathf.Clamp01(_holdGravityScale);
        _releaseImpulse = Mathf.Max(0f, _releaseImpulse);
        _holdHeightOffset = Mathf.Max(0f, _holdHeightOffset);
    }
}
