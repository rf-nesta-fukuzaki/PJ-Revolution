using UnityEngine;

/// <summary>
/// GDD §5.2 — アイテム「折りたたみ担架」
/// 大型遺物運搬用。2人で安定、1人で引きずり。
/// 担架の両端の高さが違うと遺物が滑り落ちる（コメディ演出）。
///
/// 2人操作フロー:
///   1人目が E キーで端A を掴む → TryAttach() で割り当て
///   2人目が E キーで端B を掴む → TryAttach() で割り当て
///   各キャリアは FixedUpdate で担架端位置にスナップ追従
///   E キー再押しで離脱 → Detach()
///
/// コスト 10pt / 重量 3 / スロット 3 / 耐久 70
/// </summary>
public class StretcherItem : ItemBase
{
    [Header("担架エンド")]
    [SerializeField] private Transform _endA;         // 担架の片端（1人目が持つ）
    [SerializeField] private Transform _endB;         // 担架の反対端（2人目が持つ）
    [SerializeField] private Transform _relicMount;   // 遺物を置く中央プラットフォーム

    [Header("設定")]
    [SerializeField] private float _slideTiltAngle = 15f;  // この角度以上で滑り始める
    [SerializeField] private float _slideForce     = 8f;
    [SerializeField] private float _snapRadius     = 2.0f; // 端への吸着判定距離
    [SerializeField] private float _snapSpeed      = 10f;  // 追従スピード（m/s Lerp係数）

    private Transform         _carrierA;
    private Transform         _carrierB;
    private PlayerInteraction _driverA;    // 端Aを操作するプレイヤー
    private PlayerInteraction _driverB;    // 端Bを操作するプレイヤー
    private RelicBase         _mountedRelic;
    private bool              _relicSlidOff;

    public bool IsCarriedByTwo => _carrierA != null && _carrierB != null;
    public bool IsEndAFree     => _driverA == null;
    public bool IsEndBFree     => _driverB == null;

    protected override void Awake()
    {
        base.Awake();
        _itemName          = "折りたたみ担架";
        _cost              = 10;
        _weight            = 3f;
        _slots             = 3;
        _maxDurability     = 70f;
        _currentDurability = _maxDurability;
    }

    private void FixedUpdate()
    {
        SnapCarriersToEnds();

        if (_mountedRelic == null || _relicSlidOff) return;
        float tilt = GetStretcherTiltAngle();
        if (tilt > _slideTiltAngle)
            SlideRelicOff(tilt);
    }

    // ── 2人操作 API ──────────────────────────────────────────────
    /// <summary>
    /// プレイヤーが担架に乗り込む。空いている端（A優先、次にB）を割り当てる。
    /// </summary>
    /// <param name="player">操作するプレイヤーの PlayerInteraction</param>
    /// <param name="attachPoint">割り当てられた端の Transform（追従先）</param>
    /// <returns>乗り込み成功なら true</returns>
    public bool TryAttach(PlayerInteraction player, out Transform attachPoint)
    {
        attachPoint = null;
        if (player == null) return false;

        if (_driverA == null)
        {
            _driverA    = player;
            _carrierA   = player.transform;
            attachPoint = _endA;
            Debug.Log($"[Stretcher] {player.name} が端Aを掴んだ");
            return true;
        }

        if (_driverB == null && _driverA != player)
        {
            _driverB    = player;
            _carrierB   = player.transform;
            attachPoint = _endB;
            Debug.Log($"[Stretcher] {player.name} が端Bを掴んだ → 2人担架スタート");
            return true;
        }

        return false;
    }

    /// <summary>プレイヤーが担架を離す。</summary>
    public void Detach(PlayerInteraction player)
    {
        if (player == null) return;

        if (_driverA == player)
        {
            _driverA  = null;
            _carrierA = null;
            Debug.Log("[Stretcher] 端Aが離れた！遺物が不安定に！");
        }
        else if (_driverB == player)
        {
            _driverB  = null;
            _carrierB = null;
            Debug.Log("[Stretcher] 端Bが離れた！");
        }
    }

    /// <summary>指定プレイヤーが既に乗り込んでいるか確認。</summary>
    public bool IsAttachedBy(PlayerInteraction player) =>
        _driverA == player || _driverB == player;

    /// <summary>指定 Transform に最も近い担架端を返す。</summary>
    public Transform GetNearestEnd(Transform from)
    {
        if (_endA == null && _endB == null) return transform;
        if (_endA == null) return _endB;
        if (_endB == null) return _endA;

        float dA = Vector3.Distance(from.position, _endA.position);
        float dB = Vector3.Distance(from.position, _endB.position);
        return dA <= dB ? _endA : _endB;
    }

    // ── キャリア追従 ─────────────────────────────────────────────
    private void SnapCarriersToEnds()
    {
        if (_carrierA != null && _endA != null)
            _carrierA.position = Vector3.Lerp(
                _carrierA.position, _endA.position, Time.fixedDeltaTime * _snapSpeed);

        if (_carrierB != null && _endB != null)
            _carrierB.position = Vector3.Lerp(
                _carrierB.position, _endB.position, Time.fixedDeltaTime * _snapSpeed);
    }

    // ── 遺物マウント ─────────────────────────────────────────────
    public bool MountRelic(RelicBase relic)
    {
        if (_mountedRelic != null) return false;

        _mountedRelic = relic;
        _relicSlidOff = false;

        if (_relicMount != null)
        {
            relic.transform.SetParent(_relicMount);
            relic.transform.localPosition = Vector3.zero;
        }

        var carrier = relic.GetComponent<RelicCarrier>();
        carrier?.PickUp(_relicMount, -1);
        return true;
    }

    public void UnmountRelic()
    {
        if (_mountedRelic == null) return;
        _mountedRelic.transform.SetParent(null);
        _mountedRelic = null;
    }

    // ── 傾き計算 ─────────────────────────────────────────────────
    private float GetStretcherTiltAngle()
    {
        if (_endA == null || _endB == null) return 0f;

        float heightDiff = Mathf.Abs(_endA.position.y - _endB.position.y);
        float length     = Vector3.Distance(_endA.position, _endB.position);
        if (length < 0.001f) return 0f;

        return Mathf.Atan2(heightDiff, length) * Mathf.Rad2Deg;
    }

    private void SlideRelicOff(float tiltAngle)
    {
        _relicSlidOff = true;

        Vector3 slideDir = _endA.position.y < _endB.position.y
            ? (_endA.position - _endB.position).normalized
            : (_endB.position - _endA.position).normalized;

        var rb = _mountedRelic.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity  = true;
            rb.AddForce(slideDir * _slideForce + Vector3.up * 1f, ForceMode.Impulse);
        }

        _mountedRelic.transform.SetParent(null);
        _mountedRelic = null;

        Debug.Log($"[Stretcher] 傾き {tiltAngle:F1}° — 遺物が滑り落ちた！「担架の両端の高さ合わせろ！」");
    }

    // ── デバッグ Gizmos ──────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        if (_endA != null)
        {
            Gizmos.color = _driverA != null ? Color.green : Color.white;
            Gizmos.DrawWireSphere(_endA.position, 0.3f);
        }
        if (_endB != null)
        {
            Gizmos.color = _driverB != null ? Color.green : Color.white;
            Gizmos.DrawWireSphere(_endB.position, 0.3f);
        }
        if (_snapRadius > 0f)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, _snapRadius);
        }
    }
}
