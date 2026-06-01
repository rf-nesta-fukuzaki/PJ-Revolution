using UnityEngine;

/// <summary>
/// 敵モンスターのチューニング値（ScriptableObject）。
/// 未設定時は EnemyController の Inspector デフォルトを使用する。
/// </summary>
[CreateAssetMenu(fileName = "EnemyConfig", menuName = "PeakPlunder/Enemy/Enemy Config")]
public class EnemyConfigSO : ScriptableObject
{
    [Header("原型")]
    public EnemyArchetype Archetype = EnemyArchetype.Brute;

    [Header("感知")]
    [Tooltip("視覚で捉えられる最大距離 (m)")]
    public float VisionRange = 22f;
    [Tooltip("視野角 (度) — この範囲内かつ視線が通れば発見")]
    [Range(20f, 180f)] public float VisionFov = 110f;
    [Tooltip("聴覚で物音を捉えられる半径 (m)")]
    public float HearingRadius = 18f;
    [Tooltip("しゃがみ中のプレイヤーは視覚距離がこの倍率に縮む")]
    [Range(0.1f, 1f)] public float CrouchVisionFactor = 0.45f;

    [Header("移動")]
    public float PatrolSpeed = 2.2f;
    public float ChaseSpeed  = 5.4f;
    public float TurnSpeed   = 9f;
    public float WaypointTolerance = 1.4f;
    public float PatrolRadius = 18f;

    [Header("攻撃")]
    public float AttackRange    = 2.1f;
    public float AttackDamage   = 26f;
    public float AttackCooldown = 1.4f;
    [Tooltip("攻撃時にターゲットが運搬中の遺物を叩き落とす衝撃 (N)")]
    public float LootKnockImpulse = 6f;

    [Header("状態遷移")]
    [Tooltip("視界から外れてから Search に移るまでの猶予 (s)")]
    public float LoseTargetGrace = 1.5f;
    [Tooltip("Search で最終位置を捜索する時間 (s)")]
    public float SearchDuration = 6f;
    [Tooltip("Investigate で物音源にとどまる時間 (s)")]
    public float InvestigateDuration = 4f;
    [Tooltip("ひるみ時間 (s)")]
    public float StunDuration = 2.5f;

    [Header("接地・ジャンプ")]
    public float JumpVelocity = 6f;
    public float JumpCooldown = 1.2f;
}
