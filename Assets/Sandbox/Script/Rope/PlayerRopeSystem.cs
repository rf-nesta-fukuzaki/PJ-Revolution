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
    [Header("ロープ物理")]
    [SerializeField] private int   _nodeCount         = DEFAULT_NODE_COUNT;
    [SerializeField] private float _segmentLength     = 0.3f;
    [SerializeField] private float _ropeStiffness     = 0.85f;
#pragma warning disable CS0414
    [SerializeField] private float _ropeMass          = 0.05f;  // 将来のノード質量計算用
#pragma warning restore CS0414
    [SerializeField] private float _damping           = 0.98f;
    [SerializeField] private float _windStrength      = 0.04f;

    [Header("接続設定")]
    [SerializeField] private float _maxRopeLength     = 20f;   // ショートロープ 10m / ロングロープ 25m
    [SerializeField] private float _breakForce        = 800f;  // この力を超えると切れる
    [SerializeField] private float _tensionForceScale = 300f;  // プレイヤーへの引き戻し力

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
        _positions     = new Vector3[_nodeCount];
        _prevPositions = new Vector3[_nodeCount];
        _isPinned      = new bool[_nodeCount];

        // 初期位置：原点に積み重ね
        for (int i = 0; i < _nodeCount; i++)
        {
            _positions[i]     = transform.position + Vector3.down * (i * _segmentLength);
            _prevPositions[i] = _positions[i];
        }

        _lineRenderer.positionCount = _nodeCount;
    }

    // ── 接続 API ─────────────────────────────────────────────
    /// <summary>2人のプレイヤーを繋ぐ。</summary>
    public void Connect(Rigidbody playerA, Rigidbody playerB, float ropeLength = -1)
    {
        _playerA   = playerA;
        _playerB   = playerB;
        _isConnected = true;
        _currentLength = ropeLength > 0f ? ropeLength : _maxRopeLength;

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
        for (int i = 0; i < _nodeCount; i++)
        {
            float t = (float)i / (_nodeCount - 1);
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

        for (int i = 0; i < _nodeCount; i++)
        {
            if (_isPinned[i]) continue;

            Vector3 velocity = (_positions[i] - _prevPositions[i]) * _damping;

            // 重力
            velocity += Physics.gravity * Time.fixedDeltaTime;

            // 風（Perlin Noise でランダム揺れ）
            float wx = (Mathf.PerlinNoise(_windPhase + i * WIND_NOISE_SCALE, 0f) - 0.5f) * 2f;
            float wz = (Mathf.PerlinNoise(0f, _windPhase + i * WIND_NOISE_SCALE) - 0.5f) * 2f;
            velocity += new Vector3(wx, 0f, wz) * _windStrength;

            _prevPositions[i] = _positions[i];
            _positions[i]    += velocity;
        }
    }

    private void SolveConstraints()
    {
        // 両端をプレイヤー位置にピン留め
        _positions[0]              = _playerA.position;
        _positions[_nodeCount - 1] = _playerB.position;

        for (int iter = 0; iter < CONSTRAINT_ITERATIONS; iter++)
        {
            // 両端を再ピン
            _positions[0]              = _playerA.position;
            _positions[_nodeCount - 1] = _playerB.position;

            for (int i = 0; i < _nodeCount - 1; i++)
            {
                Vector3 diff   = _positions[i + 1] - _positions[i];
                float   dist   = diff.magnitude;
                if (dist < 0.0001f) continue;

                float   error  = (dist - _segmentLength) / dist;
                Vector3 corr   = diff * error * _ropeStiffness * 0.5f;

                if (i > 0)              _positions[i]     += corr;
                if (i < _nodeCount - 2) _positions[i + 1] -= corr;
            }
        }
    }

    private void ApplyPlayerForces()
    {
        if (_playerA == null || _playerB == null) return;

        float totalLen = 0f;
        for (int i = 0; i < _nodeCount - 1; i++)
            totalLen += Vector3.Distance(_positions[i], _positions[i + 1]);

        if (totalLen <= _currentLength) return;

        // ロープが張っている → プレイヤーを引き寄せる
        Vector3 dirA = (_positions[1] - _positions[0]).normalized;
        Vector3 dirB = (_positions[_nodeCount - 2] - _positions[_nodeCount - 1]).normalized;

        float excess     = totalLen - _currentLength;
        float force      = excess * _tensionForceScale;

        _playerA.AddForce(dirA * force, ForceMode.Force);
        _playerB.AddForce(dirB * force, ForceMode.Force);
    }

    private void CheckBreak()
    {
        if (_playerA == null || _playerB == null) return;

        // 相対加速度を元に張力を推定
        Vector3 relVel    = _playerA.linearVelocity - _playerB.linearVelocity;
        float   tension   = relVel.magnitude * _playerA.mass;

        if (tension > _breakForce)
        {
            Debug.Log("[PlayerRope] 過負荷でロープ切断！");
            Disconnect();
        }
    }

    private void UpdateLineRenderer()
    {
        for (int i = 0; i < _nodeCount; i++)
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
