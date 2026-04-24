using UnityEngine;

/// <summary>
/// GDD §6.2 — カメラモード別パラメータ制御。
///
/// 7 モード（通常/ダッシュ/クライミング/運搬/投擲エイム/幽霊/室内）に応じて
/// カメラ距離・FOV・高さオフセットを 0.3 秒 Lerp で滑らかに遷移させる。
///
/// 必要な参照:
///   - _camera:       実際の Camera コンポーネント（FOV 操作と localPosition.z = -distance）
///   - _cameraRig:    高さオフセット（localPosition.y）用のリグ Transform
///                    ExplorerCameraLook が回転に使うリグを同じものを想定。
///
/// ステート取得元:
///   - ExplorerController.IsSprinting
///   - ClimbingController.IsClimbing
///   - PlayerInteraction.IsCarryingRelic
///   - GhostSystem / PlayerStateMachine.IsGhost
///   - InputStateReader.IsSecondaryPointerHeld() → 投擲エイム
///   - 室内判定: カメラ位置から真上 Raycast で天井高 5m 以下
///
/// 優先順位（高→低）: Ghost > Aim > Climbing > Indoor > Carrying > Dash > Normal
/// </summary>
[DisallowMultipleComponent]
public class CameraModeController : MonoBehaviour
{
    public enum CameraMode
    {
        Normal,
        Dash,
        Climbing,
        Carrying,
        Aiming,
        Ghost,
        Indoor,
    }

    [System.Serializable]
    private struct ModeParams
    {
        public float distance;    // Camera.localPosition.z = -distance
        public float fov;         // Camera.fieldOfView
        public float heightOffset; // CameraRig.localPosition.y

        public ModeParams(float d, float f, float h)
        {
            distance     = d;
            fov          = f;
            heightOffset = h;
        }
    }

    // ── GDD §6.2 表から ────────────────────────────────────────
    private static readonly ModeParams NormalParams   = new(4.0f, 60f, 1.2f);
    private static readonly ModeParams DashParams     = new(5.0f, 65f, 1.4f);
    private static readonly ModeParams ClimbingParams = new(3.0f, 55f, 0.8f);
    private static readonly ModeParams CarryingParams = new(4.5f, 60f, 1.5f);
    private static readonly ModeParams AimingParams   = new(3.0f, 50f, 1.0f);
    private static readonly ModeParams GhostParams    = new(0.0f, 70f, 1.2f); // フリーフライ: 自由移動は未実装、FOV のみ GDD 準拠
    private static readonly ModeParams IndoorParams   = new(2.5f, 55f, 0.8f);

    [Header("参照（未設定時は自動解決）")]
    [SerializeField] private Camera    _camera;
    [SerializeField] private Transform _cameraRig;

    [Header("ステート参照（未設定時は自動解決）")]
    [SerializeField] private ExplorerController _explorer;
    [SerializeField] private ClimbingController _climbing;
    [SerializeField] private PlayerInteraction  _interaction;
    [SerializeField] private PlayerStateMachine _stateMachine;

    [Header("遷移")]
    [Tooltip("GDD §6.3 — モード切替時の Lerp 時間（秒）")]
    [SerializeField] private float _lerpDuration = 0.3f;

    [Header("室内判定")]
    [Tooltip("GDD §6.2 — 天井高がこの値以下なら室内モード扱い（m）")]
    [SerializeField] private float _indoorCeilingThreshold = 5f;
    [SerializeField] private LayerMask _indoorRaycastMask = ~0;

    // ── 内部 ──────────────────────────────────────────────────
    private CameraMode _currentMode = CameraMode.Normal;
    private ModeParams _currentParams;
    private ModeParams _targetParams;
    private ModeParams _startParams;   // モード切替時点のスナップショット（正しい Lerp 起点）
    private float      _lerpTime;

    /// <summary>現在アクティブなカメラモード（テスト/HUD 表示用）。</summary>
    public CameraMode CurrentMode => _currentMode;

    private void Awake()
    {
        if (_cameraRig == null) _cameraRig = transform.Find("CameraRig");
        if (_camera    == null) _camera    = GetComponentInChildren<Camera>();

        if (_explorer     == null) _explorer     = GetComponent<ExplorerController>();
        if (_climbing     == null) _climbing     = GetComponent<ClimbingController>();
        if (_interaction  == null) _interaction  = GetComponent<PlayerInteraction>();
        if (_stateMachine == null) _stateMachine = GetComponent<PlayerStateMachine>();

        _currentParams = NormalParams;
        _targetParams  = NormalParams;
        _startParams   = NormalParams;
        ApplyParams(_currentParams);
    }

    private void Update()
    {
        var newMode = ResolveMode();
        if (newMode != _currentMode)
        {
            _currentMode  = newMode;
            _startParams  = _currentParams;          // 現在値を起点として凍結
            _targetParams = GetParams(newMode);
            _lerpTime     = 0f;
        }

        if (_lerpDuration <= 0f)
        {
            _currentParams = _targetParams;
        }
        else if (_lerpTime < _lerpDuration)
        {
            _lerpTime += Time.deltaTime;
            float t        = Mathf.Clamp01(_lerpTime / _lerpDuration);
            float smoothT  = Mathf.SmoothStep(0f, 1f, t);
            _currentParams.distance     = Mathf.Lerp(_startParams.distance,     _targetParams.distance,     smoothT);
            _currentParams.fov          = Mathf.Lerp(_startParams.fov,          _targetParams.fov,          smoothT);
            _currentParams.heightOffset = Mathf.Lerp(_startParams.heightOffset, _targetParams.heightOffset, smoothT);
        }
        else
        {
            _currentParams = _targetParams;          // 完全到達時のスナップで微小誤差を除去
        }

        ApplyParams(_currentParams);
    }

    // ── モード判定 ────────────────────────────────────────────
    private CameraMode ResolveMode()
    {
        if (_stateMachine != null && _stateMachine.IsGhost) return CameraMode.Ghost;

        if (InputStateReader.IsSecondaryPointerHeld())      return CameraMode.Aiming;

        if (_climbing != null && _climbing.IsClimbing)      return CameraMode.Climbing;

        if (IsIndoor())                                     return CameraMode.Indoor;

        if (_interaction != null && _interaction.IsCarryingRelic) return CameraMode.Carrying;

        if (_explorer != null && _explorer.IsSprinting)     return CameraMode.Dash;

        return CameraMode.Normal;
    }

    private bool IsIndoor()
    {
        if (_camera == null) return false;
        var origin = _camera.transform.position;
        return Physics.Raycast(origin, Vector3.up, _indoorCeilingThreshold,
                               _indoorRaycastMask, QueryTriggerInteraction.Ignore);
    }

    private static ModeParams GetParams(CameraMode mode) => mode switch
    {
        CameraMode.Normal   => NormalParams,
        CameraMode.Dash     => DashParams,
        CameraMode.Climbing => ClimbingParams,
        CameraMode.Carrying => CarryingParams,
        CameraMode.Aiming   => AimingParams,
        CameraMode.Ghost    => GhostParams,
        CameraMode.Indoor   => IndoorParams,
        _                   => NormalParams,
    };

    // ── 適用 ─────────────────────────────────────────────────
    private void ApplyParams(ModeParams p)
    {
        if (_camera != null)
        {
            _camera.fieldOfView = p.fov;
            var localPos = _camera.transform.localPosition;
            localPos.z = -p.distance;
            _camera.transform.localPosition = localPos;
        }

        if (_cameraRig != null)
        {
            var rigPos = _cameraRig.localPosition;
            rigPos.y = p.heightOffset;
            _cameraRig.localPosition = rigPos;
        }
    }
}
