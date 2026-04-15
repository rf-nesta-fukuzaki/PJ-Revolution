using System.Collections;
using UnityEngine;

/// <summary>
/// GDD §5.2 — アイテム「ポータブルウインチ」
/// 機械的引き上げ。急斜面で威力を発揮。ケーブル切断リスク。
/// コスト 20pt / 重量 3 / スロット 2 / 耐久 50
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class PortableWinchItem : ItemBase
{
    // ── 定数 ────────────────────────────────────────────────
    private const float CABLE_BREAK_TENSION  = 900f;
    private const float CABLE_BREAK_CHANCE_PER_SEC = 0.02f;  // 過負荷時の毎秒切断確率

    [Header("ウインチ設定")]
    [SerializeField] private float _liftForce        = 600f;   // 引き上げ力（N）
    [SerializeField] private float _maxCableLength   = 15f;
    [SerializeField] private float _reelSpeed        = 2f;     // 巻き取り速度（m/s）
    [SerializeField] private float _overloadThreshold = 700f;  // 過負荷開始しきい値（N）

    private LineRenderer _lineRenderer;
    private bool         _isDeployed;
    private Vector3      _anchorPoint;
    private Rigidbody    _attachedRb;  // 引き上げ対象
    private float        _cableLength;
    private float        _overloadTimer;
    private bool         _isCableBroken;

    public bool IsDeployed    => _isDeployed;
    public bool IsCableBroken => _isCableBroken;

    protected override void Awake()
    {
        base.Awake();
        _itemName          = "ポータブルウインチ";
        _cost              = 20;
        _weight            = 3f;
        _slots             = 2;
        _maxDurability     = 50f;
        _currentDurability = _maxDurability;
        _impactDmgScale    = 1.5f;

        _lineRenderer = GetComponent<LineRenderer>();
    }

    /// <summary>地形または岩にウインチをアンカー設置する。</summary>
    public bool TryDeploy(Vector3 anchorWorldPos)
    {
        if (_isBroken || _isDeployed || _isCableBroken) return false;

        _anchorPoint  = anchorWorldPos;
        _cableLength  = _maxCableLength;
        _isDeployed   = true;
        _isCableBroken = false;

        if (_lineRenderer != null)
        {
            _lineRenderer.positionCount = 2;
            _lineRenderer.enabled       = true;
        }

        Debug.Log($"[Winch] アンカー設置: {anchorWorldPos}");
        return true;
    }

    /// <summary>引き上げ対象の Rigidbody を接続する。</summary>
    public bool TryAttach(Rigidbody target)
    {
        if (!_isDeployed || target == null) return false;

        _attachedRb = target;
        Debug.Log($"[Winch] {target.name} を接続");
        return true;
    }

    /// <summary>ケーブルを巻き取る（呼び出し側が FixedUpdate で呼ぶこと）。</summary>
    public void Reel(float deltaTime)
    {
        if (!_isDeployed || _isCableBroken || _attachedRb == null) return;

        _cableLength = Mathf.Max(0f, _cableLength - _reelSpeed * deltaTime);
    }

    private void FixedUpdate()
    {
        if (!_isDeployed || _isCableBroken || _attachedRb == null) return;

        ApplyLiftForce();
        CheckCableBreak();
        UpdateLineRenderer();
        ConsumeDurability(0.02f * Time.fixedDeltaTime);
    }

    private void ApplyLiftForce()
    {
        Vector3 toAnchor = _anchorPoint - _attachedRb.position;
        float   dist     = toAnchor.magnitude;

        if (dist > _cableLength + 0.1f)
        {
            // ケーブルが張っている → 力を加える
            _attachedRb.AddForce(toAnchor.normalized * _liftForce, ForceMode.Force);
        }
    }

    private void CheckCableBreak()
    {
        if (_attachedRb == null) return;

        Vector3 toAnchor  = _anchorPoint - _attachedRb.position;
        float   tension   = toAnchor.magnitude * _attachedRb.mass * 9.81f;

        if (tension > CABLE_BREAK_TENSION)
        {
            Debug.Log($"[Winch] 過負荷！張力 {tension:F0}N");
            BreakCable();
            return;
        }

        // 過負荷ゾーンでのランダム切断
        if (tension > _overloadThreshold)
        {
            _overloadTimer += Time.fixedDeltaTime;
            if (Random.value < CABLE_BREAK_CHANCE_PER_SEC * Time.fixedDeltaTime)
            {
                Debug.Log("[Winch] ランダム切断発生！");
                BreakCable();
            }
        }
        else
        {
            _overloadTimer = 0f;
        }
    }

    private void BreakCable()
    {
        _isCableBroken = true;
        _isDeployed    = false;
        _attachedRb    = null;

        if (_lineRenderer != null)
            _lineRenderer.enabled = false;

        ConsumeDurability(30f);
        Debug.Log("[Winch] ケーブル切断！！");
    }

    public void Retract()
    {
        _isDeployed = false;
        _attachedRb = null;

        if (_lineRenderer != null)
            _lineRenderer.enabled = false;

        Debug.Log("[Winch] ウインチ格納");
    }

    private void UpdateLineRenderer()
    {
        if (_lineRenderer == null || _attachedRb == null) return;

        _lineRenderer.SetPosition(0, _anchorPoint);
        _lineRenderer.SetPosition(1, _attachedRb.position);
    }

    protected override void OnItemBroken()
    {
        BreakCable();
        base.OnItemBroken();
    }
}
