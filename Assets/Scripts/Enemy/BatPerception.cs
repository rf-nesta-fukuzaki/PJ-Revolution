using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// コウモリの感知ロジックを担当するコンポーネント。
///
/// [責務]
///   - プレイヤー・たいまつとの距離計算と閾値判定を一元管理する。
///   - BatAI はこのコンポーネントを通して判定のみを行い、距離ロジックを持たない。
///   - AddTarget / RemoveTarget で参照を動的に管理できる。
///
/// [たいまつ点灯判定]
///   TorchSystem.IsLit を参照する。
///
/// [ダウンプレイヤー除外]
///   PlayerStateManager.CurrentState == PlayerState.Downed のプレイヤーは
///   GetNearestPlayer 系メソッドの候補から除外する。
/// </summary>
public class BatPerception : MonoBehaviour
{
    // ─────────────── Inspector (新規) ───────────────

    [Header("👁️ 感知範囲")]
    [Tooltip("プレイヤー検知半径（m）")]
    [Range(3f, 20f)]
    [SerializeField] private float _wakeUpRadius = 8f;

    [Tooltip("たいまつ光の検知半径（m）")]
    [Range(5f, 25f)]
    [SerializeField] private float _lightWakeUpRadius = 12f;

    [Header("🔧 デバッグ")]
    [Tooltip("現在の最近プレイヤーまでの距離")]
    [SerializeField] private float _debugNearestDistance;

    [Tooltip("現在の最近たいまつまでの距離")]
    [SerializeField] private float _debugNearestTorchDistance;

    // ─────────────── Inspector (レガシー — BatAI / Gizmos が参照する旧パラメータ) ───────────────

    [Header("感知距離（レガシー — BatAI / Gizmos が参照する旧パラメータ）")]
    [Range(2f, 20f)] public float 起床距離 = 8f;
    [Range(1f, 15f)] public float 接近起床距離 = 5f;
    [Range(1f, 20f)] public float 追尾開始距離 = 10f;
    [Range(0.5f, 5f)] public float 攻撃距離 = 2f;
    [Range(1f, 5f)] public float 退散距離 = 3f;

    [Header("視野角（レガシー）")]
    [Range(30f, 180f)] public float 視野角半分 = 90f;

    // ─────────────── プレイヤーリスト ───────────────

    private readonly List<Transform>          _players       = new();
    private readonly List<TorchSystem>        _torches       = new();
    private readonly List<SurvivalStats>      _stats         = new();
    private readonly List<PlayerStateManager> _stateManagers = new();

    // ─────────────── Unity Lifecycle ───────────────

    private void Update()
    {
        _debugNearestDistance      = GetNearestPlayerDistance();
        _debugNearestTorchDistance = GetNearestLitTorchDistance();
    }

    // ─────────────── 参照注入 API ───────────────

    /// <summary>プレイヤーリストを一括設定（スポーン時）。</summary>
    public void SetTargets(
        List<Transform>          players,
        List<TorchSystem>        torches,
        List<SurvivalStats>      stats,
        List<PlayerStateManager> stateManagers)
    {
        _players.Clear();
        _torches.Clear();
        _stats.Clear();
        _stateManagers.Clear();

        if (players == null) return;

        int count = players.Count;
        for (int i = 0; i < count; i++)
        {
            _players.Add(players[i]);
            _torches.Add(      (torches       != null && i < torches.Count)       ? torches[i]       : null);
            _stats.Add(        (stats         != null && i < stats.Count)         ? stats[i]         : null);
            _stateManagers.Add((stateManagers != null && i < stateManagers.Count) ? stateManagers[i] : null);
        }
    }

    /// <summary>プレイヤーを1人追加。</summary>
    public void AddTarget(
        Transform          player,
        TorchSystem        torch,
        SurvivalStats      stat,
        PlayerStateManager stateManager)
    {
        _players.Add(player);
        _torches.Add(torch);
        _stats.Add(stat);
        _stateManagers.Add(stateManager);
    }

    /// <summary>プレイヤーを1人除去。</summary>
    public void RemoveTarget(Transform player)
    {
        int idx = _players.IndexOf(player);
        if (idx < 0) return;

        _players.RemoveAt(idx);
        _torches.RemoveAt(idx);
        _stats.RemoveAt(idx);
        _stateManagers.RemoveAt(idx);
    }

    // ─────────────── 旧 SetTargets (単一プレイヤー向け・Obsolete) ───────────────

    [System.Obsolete("Use SetTargets(List<Transform>, ...) for multi-player support.")]
    public void SetTargets(Transform player, TorchSystem torch)
    {
        _players.Clear();
        _torches.Clear();
        _stats.Clear();
        _stateManagers.Clear();

        if (player == null) return;

        _players.Add(player);
        _torches.Add(torch);
        _stats.Add(null);
        _stateManagers.Add(null);
    }

    // ─────────────── 公開 API ───────────────

    /// <summary>感知範囲内に有効なプレイヤーがいるか。</summary>
    public bool IsPlayerDetected()
    {
        float wakeSqr  = _wakeUpRadius      * _wakeUpRadius;
        float lightSqr = _lightWakeUpRadius * _lightWakeUpRadius;

        for (int i = 0; i < _players.Count; i++)
        {
            if (_players[i] == null) continue;

            if (!IsPlayerDowned(i))
            {
                float sqr = (_players[i].position - transform.position).sqrMagnitude;
                if (sqr < wakeSqr) return true;
            }

            if (_torches[i] != null && IsTorchLit(i))
            {
                float sqr = (_torches[i].transform.position - transform.position).sqrMagnitude;
                if (sqr < lightSqr) return true;
            }
        }

        return false;
    }

    /// <summary>最も近い有効プレイヤーの Transform を返す（ダウン済みは除外）。</summary>
    public Transform GetNearestPlayer()
    {
        Transform nearest = null;
        float     minSqr  = float.MaxValue;

        for (int i = 0; i < _players.Count; i++)
        {
            if (_players[i] == null) continue;
            if (IsPlayerDowned(i))   continue;

            float sqr = (_players[i].position - transform.position).sqrMagnitude;
            if (sqr < minSqr)
            {
                minSqr  = sqr;
                nearest = _players[i];
            }
        }

        return nearest;
    }

    /// <summary>最も近い有効プレイヤーまでの距離を返す。</summary>
    public float GetNearestPlayerDistance()
    {
        float sqr = GetNearestPlayerSqrDistance();
        return sqr < float.MaxValue ? Mathf.Sqrt(sqr) : float.MaxValue;
    }

    /// <summary>最も近い有効プレイヤーの SurvivalStats を返す。</summary>
    public SurvivalStats GetNearestPlayerStats()
    {
        int   nearestIdx = -1;
        float minSqr     = float.MaxValue;

        for (int i = 0; i < _players.Count; i++)
        {
            if (_players[i] == null) continue;
            if (IsPlayerDowned(i))   continue;

            float sqr = (_players[i].position - transform.position).sqrMagnitude;
            if (sqr < minSqr)
            {
                minSqr     = sqr;
                nearestIdx = i;
            }
        }

        return nearestIdx >= 0 ? _stats[nearestIdx] : null;
    }

    /// <summary>最も近い点灯中たいまつの Transform を返す。</summary>
    public Transform GetNearestLitTorch()
    {
        Transform nearest = null;
        float     minSqr  = float.MaxValue;

        for (int i = 0; i < _torches.Count; i++)
        {
            if (_torches[i] == null) continue;
            if (!IsTorchLit(i))      continue;

            float sqr = (_torches[i].transform.position - transform.position).sqrMagnitude;
            if (sqr < minSqr)
            {
                minSqr  = sqr;
                nearest = _torches[i].transform;
            }
        }

        return nearest;
    }

    /// <summary>最も近い点灯中たいまつまでの距離を返す。</summary>
    public float GetNearestLitTorchDistance()
    {
        float sqr = GetNearestLitTorchSqrDistance();
        return sqr < float.MaxValue ? Mathf.Sqrt(sqr) : float.MaxValue;
    }

    /// <summary>有効なプレイヤーが 0 人かどうか。</summary>
    public bool NoValidTargets()
    {
        for (int i = 0; i < _players.Count; i++)
        {
            if (_players[i] == null) continue;
            if (!IsPlayerDowned(i)) return false;
        }
        return true;
    }

    // ─────────────── 旧 判定 API (BatAI 互換) ───────────────

    public bool IsPlayerInWakeRange()
    {
        float proxSqr  = 接近起床距離 * 接近起床距離;
        float torchSqr = 起床距離     * 起床距離;

        for (int i = 0; i < _players.Count; i++)
        {
            if (_players[i] == null) continue;

            if (!IsPlayerDowned(i))
            {
                float sqr = (_players[i].position - transform.position).sqrMagnitude;
                if (sqr < proxSqr) return true;
            }

            if (_torches[i] != null && IsTorchLit(i))
            {
                float sqr = (_torches[i].transform.position - transform.position).sqrMagnitude;
                if (sqr < torchSqr) return true;
            }
        }

        return false;
    }

    public bool IsPlayerInChaseRange()  => GetNearestPlayerDistance() < 追尾開始距離;
    public bool IsPlayerInAttackRange() => GetNearestPlayerDistance() < 攻撃距離;
    public bool IsTorchTooClose()       => GetNearestLitTorchDistance() < 退散距離;

    public bool IsPlayerInFieldOfView()
    {
        Transform nearest = GetNearestPlayer();
        if (nearest == null) return false;
        Vector3 dir   = (nearest.position - transform.position).normalized;
        float   angle = Vector3.Angle(transform.forward, dir);
        return angle < 視野角半分;
    }

    public Transform GetPlayerTransform() => GetNearestPlayer();

    // ─────────────── 内部ヘルパー ───────────────

    private bool IsPlayerDowned(int i)
    {
        var sm = _stateManagers[i];
        if (sm == null) return false;
        return sm.CurrentState == PlayerState.Downed;
    }

    private bool IsTorchLit(int i)
    {
        var torch = _torches[i];
        return torch != null && torch.IsLit;
    }

    private float GetNearestPlayerSqrDistance()
    {
        float minSqr = float.MaxValue;

        for (int i = 0; i < _players.Count; i++)
        {
            if (_players[i] == null) continue;
            if (IsPlayerDowned(i))   continue;

            float sqr = (_players[i].position - transform.position).sqrMagnitude;
            if (sqr < minSqr) minSqr = sqr;
        }

        return minSqr;
    }

    private float GetNearestLitTorchSqrDistance()
    {
        float minSqr = float.MaxValue;

        for (int i = 0; i < _torches.Count; i++)
        {
            if (_torches[i] == null) continue;
            if (!IsTorchLit(i))      continue;

            float sqr = (_torches[i].transform.position - transform.position).sqrMagnitude;
            if (sqr < minSqr) minSqr = sqr;
        }

        return minSqr;
    }
}
