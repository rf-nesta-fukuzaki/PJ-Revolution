using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// GDD §3.4 — 高山病の視覚効果と速度ペナルティ。
///
/// AltitudeSicknessMin 以上（ゾーン5-6）では：
///   - 移動速度 -AltitudeSpeedPenalty（デフォルト30%）
///   - 視界ぼやけ（URP Post Processing の Depth of Field / Lens Distortion）
///   - 酸素タンクで完全防止（OxygenTankItem が IsActive = true の間）
///
/// 設定値は EnvironmentHazardConfigSO に外部化されている。
/// StaminaSystem.cs が既にスタミナドレインを実装済みのため、
/// このコンポーネントは視覚効果と速度ペナルティのみ担当する。
/// </summary>
[RequireComponent(typeof(ExplorerController))]
public class AltitudeSicknessEffect : MonoBehaviour
{
    // ── データ駆動設定 ────────────────────────────────────────
    [Header("ハザード設定 (ScriptableObject)")]
    [SerializeField] private EnvironmentHazardConfigSO _hazardConfig;

    [Header("参照")]
    [SerializeField] private Volume _postProcessVolume;   // URP Global Volume（任意）

    // ── コンポーネント ─────────────────────────────────────────
    private ExplorerController _controller;
    private PlayerHealthSystem _health;
    private GhostSystem        _ghost;

    // ── 状態 ─────────────────────────────────────────────────
    private bool  _oxygenTankActive;
    private float _effectStrength;   // 0: なし → 1: 最大

    // Post Processing コンポーネント（任意）
    private DepthOfField         _dof;
    private LensDistortion       _lensDistortion;
    private ChromaticAberration  _chromAberr;

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        _controller = GetComponent<ExplorerController>();
        _health     = GetComponent<PlayerHealthSystem>();
        _ghost      = GetComponent<GhostSystem>();

        if (_postProcessVolume != null)
        {
            _postProcessVolume.profile.TryGet(out _dof);
            _postProcessVolume.profile.TryGet(out _lensDistortion);
            _postProcessVolume.profile.TryGet(out _chromAberr);
        }
    }

    private void Update()
    {
        if (_health != null && _health.IsDead) return;
        if (_ghost  != null && _ghost.IsGhost) { SetEffect(0f); return; }
        if (_oxygenTankActive)                  { SetEffect(0f); return; }

        float threshold  = _hazardConfig != null ? _hazardConfig.AltitudeSicknessMin  : 2000f;
        float lerpSpeed  = _hazardConfig != null ? _hazardConfig.AltitudeEffectLerp   : 2f;

        bool aboveThreshold = transform.position.y >= threshold;
        float targetStrength = aboveThreshold ? 1f : 0f;
        _effectStrength = Mathf.MoveTowards(_effectStrength, targetStrength,
                                            lerpSpeed * Time.deltaTime);
        SetEffect(_effectStrength);
    }

    // ── エフェクト適用 ────────────────────────────────────────
    private void SetEffect(float strength)
    {
        float penalty = _hazardConfig != null ? _hazardConfig.AltitudeSpeedPenalty : 0.30f;
        _controller?.SetAltitudePenalty(strength * penalty);

        if (_dof != null)
        {
            _dof.active = strength > 0.05f;
            if (_dof.focalLength.overrideState)
                _dof.focalLength.value = Mathf.Lerp(50f, 300f, strength);
        }

        if (_lensDistortion != null)
        {
            _lensDistortion.active = strength > 0.05f;
            if (_lensDistortion.intensity.overrideState)
                _lensDistortion.intensity.value = strength * -0.3f;
        }

        if (_chromAberr != null)
        {
            _chromAberr.active = strength > 0.05f;
            if (_chromAberr.intensity.overrideState)
                _chromAberr.intensity.value = strength * 0.6f;
        }
    }

    // ── 酸素タンク制御（OxygenTankItem から呼ぶ） ─────────────
    /// <summary>
    /// 酸素タンクの有効/無効を設定する。
    /// 前提条件: active の変更は OxygenTankItem が管理する。
    /// </summary>
    public void SetOxygenTankActive(bool active)
    {
        _oxygenTankActive = active;
        if (active) SetEffect(0f);
        Debug.Log($"[AltitudeSickness] 酸素タンク={active}");
    }

    public bool IsAffected => _effectStrength > 0.05f && !_oxygenTankActive;
}
