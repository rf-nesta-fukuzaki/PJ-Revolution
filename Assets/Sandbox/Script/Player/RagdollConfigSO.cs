using UnityEngine;

/// <summary>
/// GDD §5.5 — Ragdoll システムの設定値を管理する ScriptableObject。
/// 衝突判定閾値・継続時間・落下ダメージ計算係数を外部化する。
///
/// 生成: Assets > Create > PeakPlunder > RagdollConfig
/// </summary>
[CreateAssetMenu(menuName = "PeakPlunder/RagdollConfig", fileName = "RagdollConfig")]
public class RagdollConfigSO : ScriptableObject
{
    [Header("Ragdoll 発動条件")]
    [field: SerializeField, Min(0f)] public float VelocityThreshold  { get; private set; } = 15f;   // m/s

    [Header("Ragdoll 継続時間")]
    [field: SerializeField, Min(0f)] public float Duration           { get; private set; } = 3f;    // 秒

    [Header("落下ダメージ（GDD §3.4 テーブル）")]
    [field: SerializeField, Min(0f)] public float SafeFallHeight     { get; private set; } = 3f;    // m: これ未満はダメージなし
    [field: SerializeField, Min(0f)] public float InstantKillHeight  { get; private set; } = 15f;   // m: これ以上は即死
    [field: SerializeField, Min(0f)] public float DamagePerMeter     { get; private set; } = 8f;    // ダメージ/m
}
