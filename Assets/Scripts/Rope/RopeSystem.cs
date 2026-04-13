using UnityEngine;

/// <summary>
/// Verlet 積分による物理ロープシミュレーション。
/// スイング（振り子）と引っ張りの 2 モードをサポート。
/// </summary>
public class RopeSystem : MonoBehaviour
{
    [Header("Rope Physics")]
    [SerializeField] private int ropeNodeCount = 20;
    [SerializeField] private float segmentLength = 0.5f;
    [SerializeField] private float ropeStiffness = 0.6f;
    [SerializeField] private int constraintIterations = 10;
    [SerializeField] private float ropeMass = 0.1f;
    [SerializeField] private float damping = 0.98f;
    [SerializeField] private float windStrength = 0.05f;

    [Header("Swing")]
    [SerializeField] private float maxRopeLength = 50f;
    [SerializeField] private float reelSpeed = 3f;
    [SerializeField] private float swingForce = 25f;

    [Header("Pull")]
    [SerializeField] private float pullForce = 800f;
    [SerializeField] private float maxPullDistance = 25f;

    [Header("References")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Transform ropeStartPoint;

    public bool IsAttached => CurrentMode != RopeMode.None;
    public RopeMode CurrentMode { get; private set; } = RopeMode.None;

    private Vector3[] _nodes;
    private Vector3[] _prevNodes;
    private Vector3 _anchorPoint;
    private Rigidbody _pullTarget;
    private Rigidbody _playerRb;
    private PlayerStateManager _stateManager;
    private float _currentRopeLength;

    private void Awake()
    {
        _playerRb = GetComponentInParent<Rigidbody>();
        _stateManager = GetComponentInParent<PlayerStateManager>();

        InitNodes();
        SetupLineRenderer();
    }

    private void InitNodes()
    {
        _nodes = new Vector3[ropeNodeCount];
        _prevNodes = new Vector3[ropeNodeCount];
        Vector3 start = ropeStartPoint != null ? ropeStartPoint.position : transform.position;
        for (int i = 0; i < ropeNodeCount; i++)
        {
            _nodes[i] = start;
            _prevNodes[i] = start;
        }
    }

    private void SetupLineRenderer()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.startWidth = 0.04f;
            lineRenderer.endWidth = 0.01f;
            // URP 対応マテリアル
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.6f, 0.4f, 0.2f);
            lineRenderer.material = mat;
        }
        lineRenderer.positionCount = ropeNodeCount;
        lineRenderer.enabled = false;
    }

    private void FixedUpdate()
    {
        if (!IsAttached) return;

        SimulateRope();
        SolveConstraints();
        ApplyPlayerForce();
        ApplyPullForces();
        UpdateLineRenderer();
    }

    private void SimulateRope()
    {
        Vector3 gravity = Physics.gravity * ropeMass;
        Vector3 startPos = ropeStartPoint != null ? ropeStartPoint.position : transform.position;

        for (int i = 0; i < ropeNodeCount; i++)
        {
            // アンカーノードは固定
            if (i == 0 && CurrentMode == RopeMode.Swing)
            {
                _nodes[0] = _anchorPoint;
                _prevNodes[0] = _anchorPoint;
                continue;
            }
            // 末尾ノードはプレイヤー位置
            if (i == ropeNodeCount - 1)
            {
                _nodes[i] = startPos;
                _prevNodes[i] = startPos;
                continue;
            }

            Vector3 vel = (_nodes[i] - _prevNodes[i]) * damping;
            // 風による揺れ
            Vector3 wind = new Vector3(
                Mathf.PerlinNoise(Time.time * 0.5f + i * 0.1f, 0f) - 0.5f,
                0f,
                Mathf.PerlinNoise(0f, Time.time * 0.5f + i * 0.1f) - 0.5f
            ) * windStrength;

            _prevNodes[i] = _nodes[i];
            _nodes[i] += vel + (gravity + wind) * Time.fixedDeltaTime;
        }
    }

    private void SolveConstraints()
    {
        for (int iter = 0; iter < constraintIterations; iter++)
        {
            for (int i = 0; i < ropeNodeCount - 1; i++)
            {
                Vector3 diff = _nodes[i + 1] - _nodes[i];
                float dist = diff.magnitude;
                if (dist < 0.001f) continue;

                float error = dist - segmentLength;
                Vector3 correction = diff.normalized * error * ropeStiffness;

                bool isAnchor = (i == 0 && CurrentMode == RopeMode.Swing);
                bool isPlayer = (i + 1 == ropeNodeCount - 1);

                if (!isAnchor) _nodes[i] += correction * 0.5f;
                if (!isPlayer) _nodes[i + 1] -= correction * 0.5f;
            }
        }
    }

    private void ApplyPlayerForce()
    {
        if (_playerRb == null || CurrentMode != RopeMode.Swing) return;

        Vector3 startPos = ropeStartPoint != null ? ropeStartPoint.position : transform.position;
        Vector3 toAnchor = _anchorPoint - startPos;
        float dist = toAnchor.magnitude;

        if (dist > _currentRopeLength)
        {
            // ロープが張った: 張力を加える
            Vector3 tension = toAnchor.normalized * swingForce;
            _playerRb.AddForce(tension, ForceMode.Force);
        }
    }

    private void UpdateLineRenderer()
    {
        if (lineRenderer == null) return;
        for (int i = 0; i < ropeNodeCount; i++)
            lineRenderer.SetPosition(i, _nodes[i]);
    }

    private void ApplyPullForces()
    {
        if (CurrentMode != RopeMode.Pull || _pullTarget == null || _playerRb == null) return;

        Vector3 startPos = ropeStartPoint != null ? ropeStartPoint.position : transform.position;
        Vector3 toTarget = _pullTarget.position - startPos;
        if (toTarget.sqrMagnitude > maxPullDistance * maxPullDistance) return;

        Vector3 pullDirection = toTarget.normalized;
        _playerRb.AddForce(pullDirection * pullForce, ForceMode.Force);
        _pullTarget.AddForce(-pullDirection * pullForce, ForceMode.Force);
    }

    // ─── 公開 API ───

    public void AttachSwing(Vector3 anchorPoint)
    {
        _anchorPoint = anchorPoint;
        CurrentMode = RopeMode.Swing;
        _currentRopeLength = Vector3.Distance(
            ropeStartPoint != null ? ropeStartPoint.position : transform.position,
            anchorPoint);
        _currentRopeLength = Mathf.Clamp(_currentRopeLength, 1f, maxRopeLength);

        // ノードをアンカーとプレイヤーの間に初期化
        InitRopeNodes(anchorPoint, ropeStartPoint != null ? ropeStartPoint.position : transform.position);

        if (lineRenderer != null) lineRenderer.enabled = true;
        _stateManager?.SetState(PlayerState.Swinging);

        AudioManager.Instance?.PlaySE("rope_attach");
    }

    public void AttachPull(Rigidbody target)
    {
        _pullTarget = target;
        CurrentMode = RopeMode.Pull;

        Vector3 startPos = ropeStartPoint != null ? ropeStartPoint.position : transform.position;
        InitRopeNodes(startPos, target.position);

        if (lineRenderer != null) lineRenderer.enabled = true;
        AudioManager.Instance?.PlaySE("rope_attach");
    }

    public void Release()
    {
        CurrentMode = RopeMode.None;
        _pullTarget = null;
        if (lineRenderer != null) lineRenderer.enabled = false;

        if (_stateManager != null && _stateManager.CurrentState == PlayerState.Swinging)
            _stateManager.SetState(PlayerState.Normal);
    }

    public void ReelIn(float amount)
    {
        if (CurrentMode != RopeMode.Swing) return;
        _currentRopeLength = Mathf.Max(1f, _currentRopeLength - amount * Time.deltaTime);
    }

    private void InitRopeNodes(Vector3 from, Vector3 to)
    {
        for (int i = 0; i < ropeNodeCount; i++)
        {
            float t = (float)i / (ropeNodeCount - 1);
            _nodes[i] = Vector3.Lerp(from, to, t);
            _prevNodes[i] = _nodes[i];
        }
    }

    private void Update()
    {
        if (!IsAttached) return;

        // E キー: ロープ巻き取り
        if (InputStateReader.ReelPressed())
            ReelIn(reelSpeed);
    }
}

public enum RopeMode { None, Swing, Pull }
