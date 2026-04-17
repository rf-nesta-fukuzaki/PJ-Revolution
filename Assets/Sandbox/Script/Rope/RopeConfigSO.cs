using UnityEngine;

/// <summary>
/// GDD §3.2 — ロープ物理パラメーターの ScriptableObject。
/// PlayerRopeSystem の全定数を外部化する。
/// </summary>
[CreateAssetMenu(menuName = "PeakPlunder/RopeConfig", fileName = "RopeConfig")]
public class RopeConfigSO : ScriptableObject
{
    [Header("物理シミュレーション")]
    public int   NodeCount          = 20;
    public float SegmentLength      = 0.3f;
    public float RopeStiffness      = 0.85f;
    public float Damping            = 0.98f;
    public float WindStrength       = 0.04f;
    public int   ConstraintIter     = 10;

    [Header("接続・破断")]
    public float MaxRopeLength      = 20f;
    public float BreakForce         = 800f;
    public float TensionForceScale  = 300f;
}
