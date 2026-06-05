using System;
using System.Collections.Generic;
using UnityEngine;
using PeakPlunder.Audio;

public enum RelicSizeCategory
{
    Small,
    Medium,
    Large
}

/// <summary>
/// GDD §6.1 — 全遺物の基底クラス。
/// 耐久: <see cref="RelicDurabilityModel"/> / ビジュアル: <see cref="RelicVisualizer"/>
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(RelicVisualizer))]
public abstract class RelicBase : MonoBehaviour
{
    [Header("データ駆動（任意）")]
    [SerializeField] private RelicDefinitionSO _definition;

    [Header("遺物設定")]
    [SerializeField] protected string _relicName  = "Unknown Relic";
    [SerializeField] protected int    _baseValue  = 100;

    [Header("耐久")]
    [SerializeField, Range(0f, 100f)] protected float _maxHp          = 100f;
    [SerializeField]                  protected float _impactThreshold = 2f;
    [SerializeField]                  protected float _damageMultiplier = 1f;

    [Header("物理")]
    [SerializeField] protected bool _isHeld = false;

    private RelicDurabilityModel _durability;
    private RelicVisualizer _visualizer;
    protected Rigidbody _rb;
    protected bool _isDestroyed;
    private RelicCondition _lastCondition;
    private readonly List<MonoBehaviour> _behaviourBuffer = new();

    // ── 配置の安定化（回収前の自損防止） ──────────────────────────
    // 設置直後の落下衝突・斜面タンブリングで脆い遺物が回収前に破壊される問題への対策。
    // 既定の遺物は「設置されたら接地して静止（kinematic）」し、最初に拾われるまで動かない。
    // 浮遊・滑落など常時物理が本質の遺物は RestsKinematicUntilHandled を false にして物理を保つ。
    private bool  _restingFrozen;       // 設置後 kinematic で静止中（拾うと解除）
    private bool  _placed;              // SettleOntoGround / 自動接地 済み
    private bool  _externallyManaged;   // conformer 等が配置を管理（自動接地を抑止）
    private float _impactImmuneUntil;   // この時刻まで衝突ダメージを免除（設置直後の微小衝突を吸収）
    private const float SETTLE_IMMUNE_SECONDS = 1.5f;
    private const float AUTO_SETTLE_DELAY     = 0.75f; // 外部未管理時に自動接地するまでの猶予
    private const float GROUND_EPSILON        = 0.02f; // 接地時に地表へめり込ませない微小浮き

    public event Action<float, float> OnDamaged;
    public event Action<RelicBase> OnRelicBroken;
    public event Action<RelicCondition, RelicCondition> OnConditionChanged;

    protected RelicDurabilityModel Durability => _durability;
    protected RelicVisualizer Visualizer => _visualizer;

    public string RelicName => _relicName;
    public float CurrentHp => _durability?.CurrentHp ?? 0f;
    public float MaxHp => _durability?.MaxHp ?? _maxHp;
    public float HpPercent => _durability?.HpPercent ?? 0f;
    public bool IsDestroyed => _isDestroyed;
    public bool IsHeld => _isHeld;
    public RelicCondition Condition => _durability?.Condition ?? RelicCondition.Destroyed;

    public float RewardMultiplier => Condition switch
    {
        RelicCondition.Perfect        => 1.0f,
        RelicCondition.Damaged        => 0.6f,
        RelicCondition.HeavilyDamaged => 0.2f,
        _                             => 0.0f
    };

    public int CurrentValue => Mathf.RoundToInt(_baseValue * RewardMultiplier);

    /// <summary>GDD §8.4 — 固定ベルト / サーマルケース対象判定。</summary>
    public virtual RelicSizeCategory SizeCategory => RelicSizeCategory.Medium;

    public bool CanSecureBeltStrap => SizeCategory == RelicSizeCategory.Small;

    /// <summary>
    /// 設置されたら接地して静止（kinematic）し、最初に拾われるまで動かないか。
    /// 既定 true（脆い遺物を回収前の落下/タンブリング破壊から守る）。浮遊・滑落など
    /// 常時物理が本質の遺物（FloatingSphere/GreatStoneSlab）は false に override する。
    /// </summary>
    protected virtual bool RestsKinematicUntilHandled => true;

    /// <summary>未だ拾われておらず、設置静止（または設置待ちの凍結）中か。NPC/AI の対象判定補助。</summary>
    public bool IsRestingFrozen => _restingFrozen;

    /// <summary>SettleOntoGround / 拾い上げ 済みか。設置前の物理ギミック（鎖張力等）抑止に使う。</summary>
    protected bool IsPlaced => _placed;

    protected virtual void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        // 配置確定まで物理を止め、authored 位置からの落下や地形未生成時の落下を防ぐ。
        // SettleOntoGround（conformer）/ 自動接地（AutoSettleRoutine）/ 拾い上げ で解除される。
        if (_rb != null) _rb.isKinematic = true;

        // RelicVisualizer は [RequireComponent] 指定だが、コンポーネント分離より前に作られた
        // 既存プレハブ（Assets/Sandbox/Prefabs/Relics/*.prefab）には未付与のまま残っている。
        // RequireComponent はランタイムの Instantiate では欠落依存を補わないため、ここで
        // 取得できなければ自前で付与し、後段 RebuildVisual() での NullReference を防ぐ。
        _visualizer = GetComponent<RelicVisualizer>();
        if (_visualizer == null)
            _visualizer = gameObject.AddComponent<RelicVisualizer>();

        if (_definition != null)
            ApplyDefinition(_definition);

        Contract.Invariant(_maxHp > 0f,
            $"RelicBase.Awake: _maxHp は 0 より大きくなければなりません (value={_maxHp}, relic={_relicName})");

        _durability = new RelicDurabilityModel(_maxHp, _impactThreshold, _damageMultiplier);
        _lastCondition = _durability.Condition;

        // 仲介コンポーネント群はビジュアル構築より前に確実に付与する。
        // 仮に RebuildVisual() がシェーダ/マテリアル等の都合で例外を投げても、拾い上げの要となる
        // RelicCarrier が欠落して「遺物を持てない／置けない」状態に陥らないようにするための順序。
        if (GetComponent<RelicDiscoveryTrigger>() == null)
            gameObject.AddComponent<RelicDiscoveryTrigger>();

        // RelicCarrier も自動付与する。OnPickedUp/OnPutDown の仲介・運搬役で、PlayerInteraction/NPCController/
        // RelicDamageTracker/TempleTraps など多くの系が relic 上の GetComponent<RelicCarrier> を前提にしている。
        // 従来どのプレハブ/シーンにも付いておらず拾い上げが成立しなかった（＝pickup SE/VFX が発火しない）配線漏れを解消。
        // [RequireComponent(typeof(RelicBase))] で安全、保持中以外は FixedUpdate が即 return で不活性。
        if (GetComponent<RelicCarrier>() == null)
            gameObject.AddComponent<RelicCarrier>();

        // RelicDamageTracker も自動付与する（GDD §9.3「遺物を一番ぶつけた人」称号のデータ供給役）。
        // 従来どのプレハブにも付いておらず OnDamaged を購読する主体が存在せず、ダメージ帰責が
        // 一切記録されていなかった配線漏れを解消する。RelicCarrier の後に付けて _carrier 取得を保証する。
        if (GetComponent<RelicDamageTracker>() == null)
            gameObject.AddComponent<RelicDamageTracker>();

        if (GetComponent<RelicGrabPoint>() == null)
            gameObject.AddComponent<RelicGrabPoint>();

        // 仲介コンポーネントを付与し終えてからビジュアルを構築する。
        RebuildVisual();

        // 外部（conformer）に管理されない遺物（runtime spawn / conformer 無しシーン）は、
        // 少し待ってから自力で直下の地表へ接地・静止する。conformer 管理下の遺物は
        // ExternalManaged() で抑止され、DistributeClimbCourse の SettleOntoGround を待つ。
        StartCoroutine(AutoSettleRoutine());
    }

    protected void ApplyDefinition(RelicDefinitionSO def)
    {
        _relicName        = def.RelicName;
        _baseValue        = def.BaseValue;
        _maxHp            = def.MaxHp;
        _impactThreshold  = def.ImpactThreshold;
        _damageMultiplier = def.DamageMultiplier;
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (_isDestroyed) return;
        // 回収前の自損を防ぐため、衝突ダメージは「世界へ接地設置（SettleOntoGround）」が済むまで
        // 全面免除する（_placed）。ネットワーク同期（NetworkRigidbody）が host spawn 時に isKinematic を
        // false へ戻して Awake 凍結を解くため、設置前は落下/タンブリング/原点スタック爆散にさらされる。
        // 設置前は無傷で受け流し、接地静止後に通常耐久へ戻す。
        //
        // 運搬中（_isHeld）も衝突ダメージを免除する。RelicCarrier が非kinematicな Rigidbody を
        // MovePosition で追従させる都合上、運搬物を壁（拠点構造物）へ押し付けると偽の高 relativeVelocity が
        // 算出され、緩やかな接触でも脆い遺物が砕けてしまうため。破損リスクはドロップ/落下/投擲（=保持解除後の
        // 自由落下衝突）と環境ダメージで成立する。鎖張力（双子像）は別途 !_isHeld で従来どおりゲートされる。
        //
        // 環境ダメージ（凍傷等）は OnCollisionEnter を経ないため、いずれの免除中も従来どおり適用される。
        if (_restingFrozen || !_placed || _isHeld || Time.time < _impactImmuneUntil) return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed < _impactThreshold) return;

        float damage = CalculateDamage(impactSpeed, collision);
        damage = ApplyDamageModifiers(damage, collision);
        if (damage <= 0f) return;
        ApplyDamage(damage, collision.gameObject);
    }

    protected virtual float CalculateDamage(float impactSpeed, Collision collision)
    {
        float excessSpeed = impactSpeed - _impactThreshold;
        return excessSpeed * _damageMultiplier * 5f;
    }

    public void ApplyDamage(float damage, GameObject source = null)
    {
        if (_isDestroyed) return;
        // 初期化(Awake)前に衝突が来ると _durability が未生成のことがあるため防御（getter 群と同様 null 安全に）。
        if (_durability == null) return;
        if (!_durability.TryApplyDamage(damage, out float applied)) return;

        OnDamaged?.Invoke(applied, _durability.CurrentHp);
        NotifyConditionChanged();
        OnDamageReceived(applied, source);

        if (_durability.IsDestroyed && !_isDestroyed)
            HandleDestruction();
    }

    public void ApplyEnvironmentalDamage(float damage) => ApplyDamage(damage, null);

    public void Repair(float amount)
    {
        if (_isDestroyed) return;
        _durability.TryRepair(amount);
    }

    protected virtual void OnDamageReceived(float damage, GameObject source) { }

    private void NotifyConditionChanged()
    {
        var newCondition = Condition;
        if (newCondition == _lastCondition) return;

        var prev = _lastCondition;
        _lastCondition = newCondition;

        Contract.Ensures(newCondition > prev || newCondition == RelicCondition.Destroyed,
            $"RelicBase: 状態は悪化方向にしか変化しません ({prev} → {newCondition})");

        OnConditionChanged?.Invoke(prev, newCondition);

        if (newCondition == RelicCondition.Damaged)
            GameServices.Audio?.PlaySE(SoundId.RelicDamageLight, transform.position);
        else if (newCondition == RelicCondition.HeavilyDamaged)
            GameServices.Audio?.PlaySE(SoundId.RelicDamageHeavy, transform.position);
    }

    private float ApplyDamageModifiers(float baseDamage, Collision collision)
    {
        if (baseDamage <= 0f) return 0f;

        _behaviourBuffer.Clear();
        GetComponents(_behaviourBuffer);

        float modifiedDamage = baseDamage;
        foreach (var behaviour in _behaviourBuffer)
        {
            if (behaviour is not IRelicDamageModifier modifier) continue;
            modifiedDamage = modifier.ModifyDamage(modifiedDamage, collision, this);
            if (modifiedDamage <= 0f) return 0f;
        }

        return modifiedDamage;
    }

    private void HandleDestruction()
    {
        _isDestroyed = true;
        GameServices.Audio?.PlaySE(SoundId.RelicDestroyed, transform.position);
        OnRelicBroken?.Invoke(this);
        OnBroken();
    }

    protected virtual void OnBroken()
    {
        Debug.Log($"[Relic] {_relicName} が破壊されました");
    }

    public virtual void OnPickedUp(Transform holder)
    {
        _isHeld = true;
        // 拾い上げたら静止凍結を解除して運搬可能にする。ただし _placed は「世界に接地設置済み」を
        // 表すフラグで、ここでは立てない。これにより、まだ一度も設置されていない遺物（原点スポーンの
        // 重複クローン等）を NPC が即座に掴んで構造物へぶつけても、設置されるまで衝突ダメージを免除し続け、
        // 回収前破壊を防ぐ。設置済みの遺物は _placed=true のままなので拾い上げ後の落下/投擲は通常どおり損傷する。
        _restingFrozen     = false;
        _rb.isKinematic = false;
        GameServices.Audio?.PlaySE(SoundId.RelicGrab, transform.position);

        // 持ち上げのポンッ（金、小さめ速め）。
        Sandbox.World.Environment.StylizedImpactFx.CollectPop(
            transform.position, new Color(1f, 0.80f, 0.25f), 0.9f, 24);
    }

    public virtual void OnPutDown()
    {
        _isHeld = false;
        // まだ一度も世界に接地設置されていない遺物（原点スポーンの重複クローンを NPC が拾った等）は、
        // 置かれた場所で接地・静止できるよう自動接地を再起動する。設置済みなら通常物理のまま。
        if (!_placed && !_isDestroyed) StartCoroutine(AutoSettleRoutine());
    }

    // ── 設置の安定化 API ──────────────────────────────────────────

    /// <summary>
    /// conformer 等の外部配置システムが配置を管理することを宣言し、自動接地を抑止する。
    /// CollectPending で呼び、DistributeClimbCourse の SettleOntoGround を唯一の権威にする。
    /// </summary>
    public void ExternalManaged() => _externallyManaged = true;

    /// <summary>
    /// 遺物を地表へ正しく接地させ、回収前の落下/タンブリング自損を防ぐ。
    /// コライダー底面を groundY に合わせ（盲目的な 0.5m 落下をしない）、速度を 0 化する。
    /// 既定遺物は kinematic で静止させ最初に拾われるまで動かさない。常時物理の遺物（override）は
    /// 物理を有効に保ったまま、設置直後の微小衝突だけ一定時間免除する。
    /// </summary>
    public void SettleOntoGround(float groundY)
    {
        if (_isDestroyed || _rb == null) return;

        // position を動かす前に物理表現を現在の transform へ同期し、bounds から正しい底面オフセットを得る
        // （autoSyncTransforms=false 環境で bounds がスタッフ＝古い位置のままになるのを防ぐ）。
        Physics.SyncTransforms();
        float bottomOffset = ComputeColliderBottomOffset();

        Vector3 p = transform.position;
        p.y = groundY + bottomOffset + GROUND_EPSILON;
        transform.position = p;

        if (!_rb.isKinematic)
        {
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        _placed = true;

        if (RestsKinematicUntilHandled)
        {
            _rb.isKinematic   = true;   // 接地して静止（拾うまで不動・無傷）
            _restingFrozen    = true;
            _impactImmuneUntil = 0f;
        }
        else
        {
            _rb.isKinematic   = false;  // 浮遊/滑落などの常時物理を許可
            _restingFrozen    = false;
            _impactImmuneUntil = Time.time + SETTLE_IMMUNE_SECONDS; // 設置直後の微小衝突を免除
        }

        Physics.SyncTransforms();
        OnSettled();
    }

    /// <summary>
    /// 接地設置（SettleOntoGround）が完了した瞬間に呼ばれる。配置位置を基準にしたい遺物
    /// （例: FloatingSphere のリーシュ基準＝配置高度）が override して捕捉する。
    /// </summary>
    protected virtual void OnSettled() { }

    /// <summary>自身/子の非トリガーコライダー群の最下点と transform.position の差（接地補正用）。</summary>
    private float ComputeColliderBottomOffset()
    {
        var cols = GetComponentsInChildren<Collider>();
        float minY = float.MaxValue;
        foreach (var c in cols)
        {
            if (c == null || c.isTrigger) continue; // 発見トリガー（RelicDiscoveryTrigger）等は無視
            float by = c.bounds.min.y;
            if (by < minY) minY = by;
        }
        if (minY == float.MaxValue) return 0f;       // 物理コライダー無し → 補正なし
        return transform.position.y - minY;          // 正なら底面は position より下にある
    }

    /// <summary>
    /// 外部に管理されない遺物（runtime spawn / conformer 無しシーン）を、少し待ってから
    /// 自力で直下の地表へ接地・静止させる。地表が見つかるまで数回リトライし、見つからなければ
    /// 凍結のまま放置して奈落落下を防ぐ。
    /// </summary>
    private System.Collections.IEnumerator AutoSettleRoutine()
    {
        float t = 0f;
        while (t < AUTO_SETTLE_DELAY)
        {
            if (_placed || _externallyManaged || _isHeld) yield break;
            t += Time.deltaTime;
            yield return null;
        }

        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (_placed || _externallyManaged || _isHeld) yield break;

            if (TryFindGroundBelow(out float groundY))
            {
                SettleOntoGround(groundY);
                yield break;
            }
            yield return new WaitForSeconds(0.5f);
        }
        // 接地できなくても凍結のまま放置（奈落落下を防ぐ）。
    }

    /// <summary>直下の最も近い静的面（地形/床）の Y を返す。自身/他の動的物体のヒットは無視する。</summary>
    private bool TryFindGroundBelow(out float groundY)
    {
        groundY = 0f;
        Vector3 from = transform.position + Vector3.up * 2f;
        var hits = Physics.RaycastAll(from, Vector3.down, 800f, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;

        float best = float.MinValue;
        bool found = false;
        foreach (var h in hits)
        {
            if (h.collider == null) continue;
            if (h.rigidbody != null) continue;              // 動的物体（他の遺物/プレイヤー）には乗せない
            if (h.collider.transform.IsChildOf(transform)) continue; // 自身のコライダーは無視
            if (h.point.y > best) { best = h.point.y; found = true; } // 直下の最上面（=最も近い地表）
        }
        if (found) groundY = best;
        return found;
    }

    protected virtual Color GizmoColor => Color.white;

    protected virtual void OnDrawGizmos()
    {
        Color c = GizmoColor;
        Gizmos.color = new Color(c.r, c.g, c.b, 0.55f);
        Gizmos.DrawSphere(transform.position + Vector3.up * 0.6f, 0.09f);
#if UNITY_EDITOR
        var style = new UnityEngine.GUIStyle { fontSize = 9 };
        style.normal.textColor = c;
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.9f,
            string.IsNullOrEmpty(_relicName) ? gameObject.name : _relicName,
            style);
#endif
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Condition switch
        {
            RelicCondition.Perfect        => Color.green,
            RelicCondition.Damaged        => Color.yellow,
            RelicCondition.HeavilyDamaged => new Color(1f, 0.5f, 0f),
            _                             => Color.red
        };
        Gizmos.DrawWireSphere(transform.position, 0.35f);
    }

    protected virtual void BuildVisual() { }

    public void RebuildVisual() => _visualizer.Rebuild(BuildVisual);

    protected GameObject VizChild(
        PrimitiveType type, string label,
        Vector3 localPos, Vector3 localScale,
        Color color, float metallic = 0f, float smoothness = 0.5f)
        => _visualizer.CreatePrimitive(type, label, localPos, localScale, color, metallic, smoothness);

    protected GameObject VizChildRot(
        PrimitiveType type, string label,
        Vector3 localPos, Quaternion localRot, Vector3 localScale,
        Color color, float metallic = 0f, float smoothness = 0.5f)
        => _visualizer.CreatePrimitiveRot(type, label, localPos, localRot, localScale, color, metallic, smoothness);
}

public enum RelicCondition
{
    Perfect,
    Damaged,
    HeavilyDamaged,
    Destroyed
}

public interface IRelicDamageModifier
{
    float ModifyDamage(float baseDamage, Collision collision, RelicBase relic);
}
