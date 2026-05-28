using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// GDD §16.3 — クライミング時のハンド IK バインダー。
///
/// <see cref="ClimbingController"/> の掴み状態と連動し、
/// TwoBoneIKConstraint の target / weight を制御する。
///
///   掴み中: target = GrabPoint.transform, weight 1.0（0.2 秒で Lerp）
///   未掴み: weight 0.0（0.2 秒で Lerp）
///
/// セットアップ:
///   1. プレイヤー Avatar に RigBuilder + TwoBoneIKConstraint（右手/左手）を配置
///   2. 本コンポーネントを同じ GameObject（またはルート）にアタッチ
///   3. _rightHandIK / _leftHandIK に該当コンストレイントを設定
///   4. _weightLerpDuration は Inspector 調整可（既定 0.2 秒）
///
/// 注意:
///   Animation Rigging の target は Transform のため、毎フレーム位置が追従する。
///   片手掴みの場合、GDD §16.3 「利き手のみ Weight 1.0」に合わせて、
///   _dominantHand を設定する（Right が既定）。
/// </summary>
[DisallowMultipleComponent]
public class ClimbingIKBinder : MonoBehaviour
{
    private const float DEFAULT_WEIGHT_LERP_DURATION = 0.2f;

    public enum Handedness { Right, Left, Both }

    [Header("IK コンストレイント")]
    [SerializeField] private Component _rightHandIK;
    [SerializeField] private Component _leftHandIK;

    [Header("参照")]
    [SerializeField] private ClimbingController _climbing;

    [Header("挙動")]
    [SerializeField] private Handedness _dominantHand = Handedness.Right;
    [SerializeField] private float _weightLerpDuration = DEFAULT_WEIGHT_LERP_DURATION;

    private bool      _isEngaged;
    private GrabPoint _lastTargetPoint;
    private Coroutine _activeLerp;

    /// <summary>最後にエンゲージした GrabPoint（デバッグ / 外部トランジション用）。</summary>
    public GrabPoint LastEngagedPoint => _lastTargetPoint;

    private void Awake()
    {
        if (_climbing == null) _climbing = GetComponent<ClimbingController>();
        SetWeightImmediate(_rightHandIK, 0f);
        SetWeightImmediate(_leftHandIK,  0f);
    }

    private void LateUpdate()
    {
        if (_climbing == null) return;

        bool nowHolding = _climbing.IsClimbing && _climbing.HeldPoint != null;
        if (nowHolding && !_isEngaged)
            Engage(_climbing.HeldPoint);
        else if (!nowHolding && _isEngaged)
            Disengage();
    }

    // ── 公開 API ─────────────────────────────────────────────
    /// <summary>IK を掴み位置にセットしてウェイトを 1.0 に引き上げる。</summary>
    public void Engage(GrabPoint point)
    {
        if (point == null) return;

        // 同一ポイントを再エンゲージする場合、冗長な target 代入（struct コピー）と
        // Lerp 再開を避ける。既にエンゲージ済みなら早期リターン。
        if (_isEngaged && _lastTargetPoint == point) return;

        _isEngaged       = true;
        _lastTargetPoint = point;

        AssignTarget(_rightHandIK, point.transform);
        AssignTarget(_leftHandIK,  point.transform);

        StartLerp(targetWeight: 1f);
    }

    /// <summary>ウェイトを 0.0 に落として IK を解除する。</summary>
    public void Disengage()
    {
        _isEngaged = false;
        StartLerp(targetWeight: 0f);
    }

    // ── 内部処理 ─────────────────────────────────────────────
    private void StartLerp(float targetWeight)
    {
        if (_activeLerp != null) StopCoroutine(_activeLerp);
        _activeLerp = StartCoroutine(LerpWeights(targetWeight));
    }

    private IEnumerator LerpWeights(float target)
    {
        float duration = Mathf.Max(0.01f, _weightLerpDuration);
        float t = 0f;

        float rStart = Weight(_rightHandIK);
        float lStart = Weight(_leftHandIK);

        bool useRight = _dominantHand != Handedness.Left;
        bool useLeft  = _dominantHand != Handedness.Right;

        float rTarget = useRight ? target : 0f;
        float lTarget = useLeft  ? target : 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            ApplyWeight(_rightHandIK, Mathf.Lerp(rStart, rTarget, k));
            ApplyWeight(_leftHandIK,  Mathf.Lerp(lStart, lTarget, k));
            yield return null;
        }

        ApplyWeight(_rightHandIK, rTarget);
        ApplyWeight(_leftHandIK,  lTarget);
    }

    private static void AssignTarget(Component c, Transform target)
    {
        if (c == null) return;

        var dataProperty = c.GetType().GetProperty("data", BindingFlags.Instance | BindingFlags.Public);
        if (dataProperty == null || !dataProperty.CanRead || !dataProperty.CanWrite)
            return;

        object data = dataProperty.GetValue(c);
        if (data == null) return;

        var dataType = data.GetType();
        var targetField = dataType.GetField("target", BindingFlags.Instance | BindingFlags.Public);
        if (targetField != null)
        {
            targetField.SetValue(data, target);
            dataProperty.SetValue(c, data);
            return;
        }

        var targetProperty = dataType.GetProperty("target", BindingFlags.Instance | BindingFlags.Public);
        if (targetProperty != null && targetProperty.CanWrite)
        {
            targetProperty.SetValue(data, target);
            dataProperty.SetValue(c, data);
        }
    }

    private static float Weight(Component c)
    {
        if (c == null) return 0f;

        var weightProperty = c.GetType().GetProperty("weight", BindingFlags.Instance | BindingFlags.Public);
        if (weightProperty == null || !weightProperty.CanRead) return 0f;

        object value = weightProperty.GetValue(c);
        return value is float weight ? weight : 0f;
    }

    private static void ApplyWeight(Component c, float w)
    {
        if (c == null) return;

        var weightProperty = c.GetType().GetProperty("weight", BindingFlags.Instance | BindingFlags.Public);
        if (weightProperty != null && weightProperty.CanWrite)
            weightProperty.SetValue(c, w);
    }

    private static void SetWeightImmediate(Component c, float w)
    {
        if (c == null) return;
        ApplyWeight(c, w);
    }
}
