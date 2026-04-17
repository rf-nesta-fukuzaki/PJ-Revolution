using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GDD §3.2 — プレイヤー間ロープ物理システム。
/// Verlet 積分によるロープシミュレーション。
/// たるむ、引っかかる、絡まる。1人が滑落すると全員引っ張られる。
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class PlayerRopeSystem : MonoBehaviour
{
    // ── 定数 ────────────────────────────────────────────────
    private const int   DEFAULT_NODE_COUNT       = 20;
    private const int   CONSTRAINT_ITERATIONS    = 10;
    private const float WIND_NOISE_SCALE         = 0.5f;

    // ── Inspector ───────────────────────────────────────────
    [Header("設定 (ScriptableObject — 未設定時はデフォルト値を使用)")]
    [SerializeField] private RopeConfigSO _config;

    // パラメーターアクセサ（SO 注入 or デフォルト値）
    private int   NodeCount         => _config != null ? _config.NodeCount        : DEFAULT_NODE_COUNT;
    private float SegmentLength     => _config != null ? _config.SegmentLength    : 0.3f;
    private float RopeStiffness     => _config != null ? _config.RopeStiffness    : 0.85f;
    private float Damping           => _config != null ? _config.Damping          : 0.98f;
    private float WindStrength      => _config != null ? _config.WindStrength     : 0.04f;
    private int   ConstraintIter    => _config != null ? _config.ConstraintIter   : CONSTRAINT_ITERATIONS;
    private float MaxRopeLength     => _config != null ? _config.MaxRopeLength    : 20f;
    private float BreakForce        => _config != null ? _config.BreakForce       : 800f;
    private float TensionForceScale => _config != null ? _config.TensionForceScale: 300f;

    [Header("プレイヤー参照")]
    [SerializeField] private Rigidbody _playerA;
    [SerializeField] private Rigidbody _playerB;

    // ── ロープノード ─────────────────────────────────────────
    private Vector3[] _positions;
    private Vector3[] _prevPositions;
    private bool[]    _isPinned;

    private LineRenderer _lineRenderer;
    private bool         _isConnected;
    private float        _currentLength;
    private float        _windPhase;

    public bool IsConnected => _isConnected;

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        InitNodes();
    }

    private void InitNodes()
    {
        int n = NodeCount;
        _positions     = new Vector3[n];
        _prevPositions = new Vector3[n];
        _isPinned      = new bool[n];

        float seg = SegmentLength;
        for (int i = 0; i < n; i++)
        {
            _positions[i]     = transform.position + Vector3.down * (i * seg);
            _prevPositions[i] = _positions[i];
        }

        _lineRenderer.positionCount = n;
    }

    // ── 接続 API ─────────────────────────────────────────────
    /// <summary>2人のプレイヤーを繋ぐ。</summary>
    public void Connect(Rigidbody playerA, Rigidbody playerB, float ropeLength = -1)
    {
        _playerA   = playerA;
        _playerB   = playerB;
        _isConnected = true;
        _currentLength = ropeLength > 0f ? ropeLength : MaxRopeLength;

        // ノード位置を再配置
        ReplaceNodes(playerA.position, playerB.position);

        _lineRenderer.enabled = true;
        Debug.Log("[PlayerRope] ロープ接続完了");
    }

    /// <summary>ロープを切断する。</summary>
    public void Disconnect()
    {
        _isConnected = false;
        _playerA     = null;
        _playerB     = null;
        _lineRenderer.enabled = false;
        Debug.Log("[PlayerRope] ロープ切断！");
    }

    private void ReplaceNodes(Vector3 start, Vector3 end)
    {
        int n = _positions.Length;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / (n - 1);
            _positions[i]     = Vector3.Lerp(start, end, t);
            _prevPositions[i] = _positions[i];
        }
    }

    // ── 物理更新 ─────────────────────────────────────────────
    private void FixedUpdate()
    {
        if (!_isConnected || _playerA == null || _playerB == null) return;

        SimulateRope();
        SolveConstraints();
        ApplyPlayerForces();
        UpdateLineRenderer();
        CheckBreak();
    }

    private void SimulateRope()
    {
        _windPhase += Time.fixedDeltaTime * 0.7f;

        int nodeCount = _positions.Length;
        float damping = Damping;
        for (int i = 0; i < nodeCount; i++)
        {
            if (_isPinned[i]) continue;

            Vector3 velocity = (_positions[i] - _prevPositions[i]) * damping;

            // 重力
            velocity += Physics.gravity * Time.fixedDeltaTime;

            // 風（Perlin Noise でランダム揺れ）
            float wx = (Mathf.PerlinNoise(_windPhase + i * WIND_NOISE_SCALE, 0f) - 0.5f) * 2f;
            float wz = (Mathf.PerlinNoise(0f, _windPhase + i * WIND_NOISE_SCALE) - 0.5f) * 2f;
            velocity += new Vector3(wx, 0f, wz) * WindStrength;

            _prevPositions[i] = _positions[i];
            _positions[i]    += velocity;
        }
    }

    private void SolveConstraints()
    {
        int   n   = _positions.Length;
        float seg = SegmentLength;
        float stiffness = RopeStiffness;

        // 両端をプレイヤー位置にピン留め
        _positions[0]     = _playerA.position;
        _positions[n - 1] = _playerB.position;

        for (int iter = 0; iter < ConstraintIter; iter++)
        {
            // 両端を再ピン
            _positions[0]     = _playerA.position;
            _positions[n - 1] = _playerB.position;

            for (int i = 0; i < n - 1; i++)
            {
                Vector3 diff   = _positions[i + 1] - _positions[i];
                float   dist   = diff.magnitude;
                if (dist < 0.0001f) continue;

                float   error  = (dist - seg) / dist;
                Vector3 corr   = diff * error * stiffness * 0.5f;

                if (i > 0)      _positions[i]     += corr;
                if (i < n - 2)  _positions[i + 1] -= corr;
            }
        }
    }

    private void ApplyPlayerForces()
    {
        if (_playerA == null || _playerB == null) return;

        int   n        = _positions.Length;
        float totalLen = 0f;
        for (int i = 0; i < n - 1; i++)
            totalLen += Vector3.Distance(_positions[i], _positions[i + 1]);

        if (totalLen <= _currentLength) return;

        // ロープが張っている → プレイヤーを引き寄せる
        Vector3 dirA = (_positions[1] - _positions[0]).normalized;
        Vector3 dirB = (_positions[n - 2] - _positions[n - 1]).normalized;

        float excess = totalLen - _currentLength;
        float force  = excess * TensionForceScale;

        _playerA.AddForce(dirA * force, ForceMode.Force);
        _playerB.AddForce(dirB * force, ForceMode.Force);
    }

    private void CheckBreak()
    {
        if (_playerA == null || _playerB == null) return;

        // 相対加速度を元に張力を推定
        Vector3 relVel    = _playerA.linearVelocity - _playerB.linearVelocity;
        float   tension   = relVel.magnitude * _playerA.mass;

        if (tension > BreakForce)
        {
            Debug.Log("[PlayerRope] 過負荷でロープ切断！");
            Disconnect();
        }
    }

    private void UpdateLineRenderer()
    {
        int n = _positions.Length;
        for (int i = 0; i < n; i++)
            _lineRenderer.SetPosition(i, _positions[i]);
    }

    // ── デバッグ ─────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        if (_positions == null) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < _positions.Length - 1; i++)
            Gizmos.DrawLine(_positions[i], _positions[i + 1]);
    }
}
