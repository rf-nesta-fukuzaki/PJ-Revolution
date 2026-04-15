using UnityEngine;

/// <summary>
/// GDD §6.2 — 遺物③「儀式用の大石板」
/// 物理軸：超重い。2人でも重い。坂で加速すると止められない。
/// 難易度：★★☆  壊れやすさ：低
/// </summary>
public class GreatStoneSlabRelic : RelicBase
{
    [Header("石板設定")]
    [SerializeField] private float _slopeSlideFactor   = 3f;    // 坂道での加速倍率
    [SerializeField] private float _slopeSoundThreshold = 4f;   // この速さ以上でガリガリSE

    private bool _isSlidingOnSlope;
    private float _lastSlopeCheckTime;
    private const float SLOPE_CHECK_INTERVAL = 0.2f;

    protected override void Awake()
    {
        _relicName        = "儀式用の大石板";
        _baseValue        = 120;
        _maxHp            = 150f;   // 壊れにくい
        _damageMultiplier = 0.3f;
        _impactThreshold  = 5f;     // かなりの衝撃でないとダメージなし

        base.Awake();

        // 重い設定（base.Awake で _rb が初期化された後）
        _rb.mass          = 80f;
        _rb.linearDamping = 0.5f;
    }

    private void Update()
    {
        if (_isDestroyed || _isHeld) return;

        if (Time.time - _lastSlopeCheckTime > SLOPE_CHECK_INTERVAL)
        {
            _lastSlopeCheckTime = Time.time;
            CheckSlope();
        }
    }

    private void CheckSlope()
    {
        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2f))
        {
            _isSlidingOnSlope = false;
            return;
        }

        float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
        _isSlidingOnSlope = slopeAngle > 15f;

        if (_isSlidingOnSlope && _rb.linearVelocity.magnitude > _slopeSoundThreshold)
        {
            Debug.Log("[StoneSlab] 石板が坂を加速中！止められない！");
        }
    }

    private void FixedUpdate()
    {
        if (!_isSlidingOnSlope || _isHeld) return;

        // 坂道での追加加速
        Vector3 slideDir = Vector3.ProjectOnPlane(Vector3.down, GetSlopeNormal()).normalized;
        _rb.AddForce(slideDir * _slopeSlideFactor, ForceMode.Acceleration);
    }

    private Vector3 GetSlopeNormal()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2f))
            return hit.normal;
        return Vector3.up;
    }

    protected override Color GizmoColor => new Color(0.49f, 0.45f, 0.38f);

    protected override void BuildVisual()
    {
        var stone  = new Color(0.58f, 0.54f, 0.46f);
        var darker = new Color(0.38f, 0.34f, 0.28f);

        // メイン石板
        VizChild(PrimitiveType.Cube, "slab",
            Vector3.zero, new Vector3(1.1f, 2.2f, 0.28f),
            stone, metallic: 0.05f, smoothness: 0.15f);
        // 刻文
        VizChild(PrimitiveType.Cube, "engrave",
            new Vector3(0f, 0.15f, 0.16f), new Vector3(0.75f, 1.5f, 0.06f),
            darker, smoothness: 0.1f);
        // 上部アーチ
        VizChild(PrimitiveType.Sphere, "arch",
            new Vector3(0f, 1.05f, 0f), new Vector3(1.1f, 0.5f, 0.28f),
            stone, metallic: 0.05f, smoothness: 0.15f);
    }

    protected override void OnBroken()
    {
        base.OnBroken();
        Debug.Log("[StoneSlab] 「こんなくだらない内容のために命かけてるのか」");
    }
}
