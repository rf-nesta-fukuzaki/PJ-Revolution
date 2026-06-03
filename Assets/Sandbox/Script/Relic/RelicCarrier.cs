using UnityEngine;

/// <summary>
/// プレイヤーが遺物を持ったとき・置いたときに呼ばれる仲介クラス。
/// RelicBase の OnPickedUp / OnPutDown を外部からトリガーし、
/// LastCarrierPlayerId を RelicDamageTracker に提供する。
/// </summary>
[RequireComponent(typeof(RelicBase))]
public class RelicCarrier : MonoBehaviour
{
    // GDD §16.2 — CarryType 値
    //   0=なし/1=片手/2=両手/3=背負い/4=担架
    public const int CARRY_TYPE_NONE      = 0;
    public const int CARRY_TYPE_ONE_HAND  = 1;
    public const int CARRY_TYPE_TWO_HANDS = 2;
    public const int CARRY_TYPE_ON_BACK   = 3;
    public const int CARRY_TYPE_STRETCHER = 4;

    [Header("持ち上げ設定")]
    [SerializeField] private float _holdDistance  = 1.5f;   // プレイヤー前方への距離
    [SerializeField] private float _holdSmoothing = 10f;    // 追従スムーズ係数

    [Header("アニメーター連動 (GDD §16.2)")]
    [Tooltip("この遺物を持ったときの Animator CarryType 値。0=なし/1=片手/2=両手/3=背負い/4=担架")]
    [SerializeField, Range(0, 4)] private int _carryType = CARRY_TYPE_TWO_HANDS;

    [Header("運搬による移動コスト (コアループ: 重い遺物ほど遅くなる → 持つか置くかの判断)")]
    [Tooltip("最も軽い遺物(質量 _lightMass 以下)を持ったときの移動速度低下率。")]
    [SerializeField, Range(0f, 1f)] private float _minCarryPenalty = 0.06f;
    [Tooltip("最も重い遺物(質量 _heavyMass 以上)を持ったときの移動速度低下率。")]
    [SerializeField, Range(0f, 1f)] private float _maxCarryPenalty = 0.5f;
    [Tooltip("ペナルティ最小となる質量(kg)。")]
    [SerializeField] private float _lightMass = 1f;
    [Tooltip("ペナルティ最大となる質量(kg)。")]
    [SerializeField] private float _heavyMass = 80f;

    private RelicBase  _relic;
    private Rigidbody  _rb;
    private Transform  _currentHolder;
    private Animator   _holderAnimator;
    private ExplorerController _holderController; // 運搬中の移動ペナルティ適用先
    private int        _lastCarrierPlayerId = -1;
    private Vector3    _prevHolderPos;
    // 拾い上げ前の useGravity を保存し、置いたとき復元する。FloatingSphere のように
    // useGravity=false を中核メカニクスにする遺物が、置いた後に重力で落下してしまう不具合を防ぐ。
    private bool       _useGravityBeforePickup = true;

    private static readonly int IsCarryingHash = Animator.StringToHash("IsCarrying");
    private static readonly int CarryTypeHash  = Animator.StringToHash("CarryType");

    public int  LastCarrierPlayerId => _lastCarrierPlayerId;
    public bool IsBeingCarried      => _currentHolder != null;
    public Transform CurrentHolder  => _currentHolder;

    private void Awake()
    {
        _relic = GetComponent<RelicBase>();
        _rb    = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (_currentHolder == null) return;

        // 運搬距離を ScoreTracker に通知
        float delta = Vector3.Distance(_currentHolder.position, _prevHolderPos);
        _prevHolderPos = _currentHolder.position;
        if (_lastCarrierPlayerId >= 0)
            GameServices.Score?.RecordRelicCarried(_lastCarrierPlayerId, delta);

        // 保持中：プレイヤーの前方 _holdDistance の位置に追従
        Vector3 targetPos = _currentHolder.position
                            + _currentHolder.forward * _holdDistance
                            + Vector3.up * 0.2f;

        _rb.MovePosition(Vector3.Lerp(_rb.position, targetPos,
                                       _holdSmoothing * Time.fixedDeltaTime));
    }

    // ── 公開 API ─────────────────────────────────────────────
    /// <summary>指定プレイヤーが遺物を拾い上げる。</summary>
    public void PickUp(Transform holder, int playerId)
    {
        if (IsBeingCarried) return;

        _currentHolder       = holder;
        _lastCarrierPlayerId = playerId;
        _prevHolderPos       = holder.position;
        _relic.OnPickedUp(holder);

        _rb.isKinematic = false;
        _useGravityBeforePickup = _rb.useGravity;   // 置いたときに復元するため保存
        _rb.useGravity  = false;

        // Animator: IsCarrying / CarryType (GDD §16.2)
        _holderAnimator = holder.GetComponentInChildren<Animator>();
        if (_holderAnimator != null)
        {
            _holderAnimator.SetBool(IsCarryingHash, true);
            _holderAnimator.SetInteger(CarryTypeHash, _carryType);
        }

        // 運搬コスト: 質量に応じた移動速度ペナルティを適用する。これで「重い遺物を山頂まで運ぶか、
        // 諦めて身軽に進むか」が全遺物共通の意味ある判断になる（従来は質量が速度に効いていなかった）。
        _holderController = holder.GetComponentInParent<ExplorerController>();
        if (_holderController == null) _holderController = holder.GetComponentInChildren<ExplorerController>();
        _holderController?.SetCarryPenalty(ComputeCarryPenalty());
    }

    /// <summary>遺物を置く。</summary>
    public void PutDown()
    {
        if (!IsBeingCarried) return;

        // Animator: IsCarrying / CarryType 解除 (GDD §16.2)
        if (_holderAnimator != null)
        {
            _holderAnimator.SetBool(IsCarryingHash, false);
            _holderAnimator.SetInteger(CarryTypeHash, CARRY_TYPE_NONE);
            _holderAnimator = null;
        }

        // 運搬コスト解除（置いたら身軽に戻る）
        _holderController?.SetCarryPenalty(0f);
        _holderController = null;

        _currentHolder  = null;
        _rb.useGravity  = _useGravityBeforePickup;   // 拾う前の重力設定を復元（無条件 true 化を廃止）
        _relic.OnPutDown();
    }

    /// <summary>運搬中の移動速度低下率を質量から算出する（軽い→ _minCarryPenalty / 重い→ _maxCarryPenalty）。</summary>
    private float ComputeCarryPenalty()
    {
        float mass = _rb != null ? _rb.mass : _lightMass;
        float t = Mathf.InverseLerp(_lightMass, _heavyMass, mass);
        return Mathf.Lerp(_minCarryPenalty, _maxCarryPenalty, t);
    }

    /// <summary>ドロップ（衝撃あり）。</summary>
    public void Drop(Vector3 impulse)
    {
        PutDown();
        _rb.AddForce(impulse, ForceMode.Impulse);
    }
}
