using UnityEngine;

/// <summary>
/// ロープオブジェクトの管理。IInteractable を実装し、設置と登攀開始の2段階インタラクションを提供する。
///
/// [ステートマシン]
///   IsDeployed = false:  ロープアイテムを持つプレイヤーが E → 設置（IsDeployed = true）
///   IsDeployed = true:   任意のプレイヤーが E → PlayerClimbing.StartClimbing() を呼ぶ
///
/// [位置の前提]
///   transform.position = ロープ下端（崖の壁面下）
///   AnchorPoint        = ロープ上端（崖の頂点、別 Transform として設定）
///   ロープは垂直に配置すること
///
/// [必須コンポーネント]
///   LineRenderer, CapsuleCollider
/// </summary>
[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(CapsuleCollider))]
public class RopeController : MonoBehaviour, IInteractable
{
    // ─────────────── Inspector ───────────────

    [Header("ロープ設定")]
    [Tooltip("ロープ上端の Transform (崖頂部に配置した空の GameObject)")]
    [SerializeField] private Transform anchorPoint;

    [Tooltip("インタラクト・接触判定用のコライダー半径")]
    [SerializeField] private float ropeRadius = 0.35f;

    [Header("LineRenderer 設定")]
    [Tooltip("ロープの描画幅 (LineRenderer の Width)")]
    [SerializeField] private float ropeWidth = 0.05f;

    // ─────────────── 状態 ───────────────

    private bool _isDeployed;

    /// <summary>ロープが設置済みかどうか。</summary>
    public bool IsDeployed => _isDeployed;

    // ─────────────── 内部参照 ───────────────

    private LineRenderer    _lineRenderer;
    private CapsuleCollider _capsuleCollider;

    // ─────────────── Unity Lifecycle ───────────────

    private void Awake()
    {
        _lineRenderer    = GetComponent<LineRenderer>();
        _capsuleCollider = GetComponent<CapsuleCollider>();

        _lineRenderer.startWidth    = ropeWidth;
        _lineRenderer.endWidth      = ropeWidth;
        _lineRenderer.positionCount = 2;
    }

    private void Start()
    {
        UpdateVisuals(false);
    }

    // ─────────────── ビジュアル更新 ───────────────

    private void UpdateVisuals(bool deployed)
    {
        if (deployed && anchorPoint != null)
        {
            _lineRenderer.enabled = true;
            _lineRenderer.SetPosition(0, transform.position);
            _lineRenderer.SetPosition(1, anchorPoint.position);

            float height = Vector3.Distance(transform.position, anchorPoint.position);
            _capsuleCollider.direction = 1;
            _capsuleCollider.height    = height;
            _capsuleCollider.radius    = ropeRadius;
            _capsuleCollider.center    = Vector3.up * (height * 0.5f);
        }
        else
        {
            _lineRenderer.enabled = false;

            _capsuleCollider.direction = 1;
            _capsuleCollider.height    = 1f;
            _capsuleCollider.radius    = ropeRadius;
            _capsuleCollider.center    = Vector3.up * 0.5f;
        }
    }

    // ─────────────── IInteractable ───────────────

    public void Interact(UnityEngine.GameObject interactor)
    {
        var climbing = interactor.GetComponent<PlayerClimbing>();
        if (climbing == null)
        {
            Debug.LogWarning("[RopeController] 対象プレイヤーに PlayerClimbing がありません");
            return;
        }

        if (!_isDeployed)
        {
            if (!climbing.HasRope)
            {
                Debug.Log("[RopeController] ロープアイテムが必要です");
                return;
            }

            _isDeployed = true;
            UpdateVisuals(true);
            climbing.ConsumeRope();
            Debug.Log($"[RopeController] {interactor.name} がロープを設置しました");
        }
        else
        {
            if (climbing.IsClimbing)
            {
                Debug.Log("[RopeController] 既に登攀中です");
                return;
            }

            climbing.StartClimbing(this);
        }
    }

    public string GetPromptText()
    {
        return _isDeployed
            ? "[E] ロープを登る"
            : "[E] ロープを設置する（ロープアイテムが必要）";
    }

    // ─────────────── 公開プロパティ ───────────────

    /// <summary>ロープ下端のワールド座標</summary>
    public Vector3 BottomPosition => transform.position;

    /// <summary>ロープ上端のワールド座標</summary>
    public Vector3 TopPosition =>
        anchorPoint != null
            ? anchorPoint.position
            : transform.position + Vector3.up * 5f;

    // ─────────────── Gizmo ───────────────

    private void OnDrawGizmosSelected()
    {
        if (anchorPoint == null) return;
        Gizmos.color = _isDeployed ? Color.green : Color.yellow;
        Gizmos.DrawLine(transform.position, anchorPoint.position);
        Gizmos.DrawWireSphere(anchorPoint.position, 0.2f);
    }
}
