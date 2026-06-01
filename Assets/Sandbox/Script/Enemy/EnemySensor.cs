using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敵モンスターの感知（視覚＋聴覚）。
/// ターゲット候補は <see cref="PlayerHealthSystem.RegisteredPlayers"/>（＝プレイヤー）。
/// 視覚: 距離・視野角・視線(LOS) を満たすと発見。しゃがみで視認距離が縮む。
/// 聴覚: ダッシュ中プレイヤー＋ NoiseEvent を捉えて最終物音位置を更新する。
/// </summary>
public class EnemySensor
{
    private readonly Transform _eye;
    private readonly EnemyConfigSO _config;
    private readonly LayerMask _losBlockers;

    private Vector3 _lastHeardPos;
    private bool _hasHeard;

    public Vector3 LastHeardPosition => _lastHeardPos;
    public bool HasHeardNoise => _hasHeard;

    public EnemySensor(Transform eye, EnemyConfigSO config, LayerMask losBlockers)
    {
        _eye = eye;
        _config = config;
        _losBlockers = losBlockers;
        NoiseEvent.OnNoise += OnNoise;
    }

    public void Dispose() => NoiseEvent.OnNoise -= OnNoise;

    public void ClearHeard() => _hasHeard = false;

    private void OnNoise(Vector3 position, float radius)
    {
        if (Vector3.Distance(_eye.position, position) > radius) return;
        _lastHeardPos = position;
        _hasHeard = true;
    }

    /// <summary>
    /// 現在見えている／聞こえている最良のターゲットを返す。
    /// 見つからなければ null。聴覚のみのヒットは <paramref name="seen"/>=false で位置のみ更新。
    /// </summary>
    public PlayerHealthSystem FindTarget(out bool seen)
    {
        seen = false;
        IReadOnlyList<PlayerHealthSystem> players = PlayerHealthSystem.RegisteredPlayers;

        PlayerHealthSystem best = null;
        float bestDist = Mathf.Infinity;

        for (int i = 0; i < players.Count; i++)
        {
            PlayerHealthSystem p = players[i];
            if (p == null || p.IsDead || p.IsDowned) continue; // ダウン中の味方は狙わない

            Vector3 toTarget = p.transform.position - _eye.position;
            float dist = toTarget.magnitude;

            // 聴覚: ダッシュ中なら視野外でも物音位置を更新（ただし発見扱いにはしない）
            if (dist <= _config.HearingRadius && IsLoud(p))
            {
                _lastHeardPos = p.transform.position;
                _hasHeard = true;
            }

            float visionRange = _config.VisionRange * (IsCrouching(p) ? _config.CrouchVisionFactor : 1f);
            if (dist > visionRange) continue;

            float angle = Vector3.Angle(_eye.forward, toTarget.normalized);
            if (angle > _config.VisionFov * 0.5f) continue;

            if (!HasLineOfSight(p.transform, dist)) continue;

            if (dist < bestDist)
            {
                bestDist = dist;
                best = p;
            }
        }

        if (best != null)
        {
            seen = true;
            _lastHeardPos = best.transform.position;
            _hasHeard = true;
        }
        return best;
    }

    private bool HasLineOfSight(Transform target, float dist)
    {
        Vector3 origin = _eye.position + Vector3.up * 0.2f;
        Vector3 dir = (target.position + Vector3.up * 0.8f) - origin;
        if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dist + 1f, _losBlockers, QueryTriggerInteraction.Ignore))
        {
            // 遮蔽物がターゲットより手前にあれば視線は通らない
            if (hit.distance < dist - 1.2f && hit.transform != target && !hit.transform.IsChildOf(target))
                return false;
        }
        return true;
    }

    private static bool IsLoud(PlayerHealthSystem p)
    {
        var ctrl = p.GetComponent<ExplorerController>();
        return ctrl != null && ctrl.IsSprinting && !ctrl.IsCrouching;
    }

    private static bool IsCrouching(PlayerHealthSystem p)
    {
        var ctrl = p.GetComponent<ExplorerController>();
        return ctrl != null && ctrl.IsCrouching;
    }
}
