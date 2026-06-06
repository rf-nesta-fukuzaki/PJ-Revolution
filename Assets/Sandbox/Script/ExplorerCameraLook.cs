using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Sandbox シーン用 FPS カメラ視点制御。
/// - マウス X → Explorer 本体の Y 軸回転（体の向き）
/// - マウス Y → CameraRig の X 軸回転（視線上下、±80° クランプ）
/// - ExplorerController と組み合わせて Explorer ルートにアタッチする。
/// </summary>
[DefaultExecutionOrder(150)]
public class ExplorerCameraLook : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("Explorer の目線高さに置いた空の子オブジェクト。Main Camera を子にする。未設定時は子の CameraRig を自動検索する。")]
    [SerializeField] private Transform _cameraRig;
    [Tooltip("プレイヤーモデルのルート。未設定時は ExplorerModel を自動検索する。")]
    [SerializeField] private Transform _visualRoot;

    [Header("感度")]
    [Tooltip("水平方向の視点感度。実効感度 = InputStateReader.MouseLookScale × この値（度/ピクセル）。")]
    [SerializeField] private float _sensitivityX = 5f;
    [Tooltip("垂直方向の視点感度。")]
    [SerializeField] private float _sensitivityY = 5f;

    [Header("視点スムージング")]
    [Tooltip("入力をスムージングして細かなガタつきを抑える。")]
    [SerializeField] private bool _useLookSmoothing = true;
    [Tooltip("スムージングの追従時間（秒）。小さいほどキビキビ、大きいほど滑らか。マウス入力の体感遅延に直結するため小さめに保つ。")]
    [SerializeField] private float _lookSmoothTime = 0.02f;
    [Tooltip("1フレームで受け付ける最大入力デルタ。ウィンドウ復帰時などの異常スパイクのみを抑える上限。" +
             "小さすぎると素早いマウス旋回（フリック）まで頭打ちになり「もっさり」するため、" +
             "通常の高速旋回は許容しつつ極端なスパイクだけを切る値にする。")]
    [SerializeField] private float _maxLookDeltaPerFrame = 10f;

    [Header("設定連動 (GDD §6.3 / §14.7)")]
    [Tooltip("設定画面のマウス感度(0.5〜10.0)の基準値。この値のとき実効感度は素のまま。")]
    [SerializeField] private float _settingsSensitivityReference = 3f;

    [Header("感度ライブ調整")]
    [Tooltip("プレイ中に [ / ] キーで感度を増減する際のステップ量。")]
    [SerializeField] private float _sensitivityStep = 0.5f;
    [Tooltip("ライブ調整での感度の下限。")]
    [SerializeField] private float _minSensitivity = 0.5f;
    [Tooltip("ライブ調整での感度の上限。")]
    [SerializeField] private float _maxSensitivity = 20f;

    [Header("ピッチ制限 (度)")]
    [SerializeField] private float _minPitch = -80f;
    [SerializeField] private float _maxPitch =  80f;

    [Header("一人称ビジュアル")]
    [SerializeField] private bool _hideLocalBody = true;
    [SerializeField] private bool _preserveBodyShadows = true;

    private float _pitch;
    private float _yaw;
    private float _appliedYaw;
    private Vector2 _smoothedLookDelta;
    private Vector2 _smoothedLookVelocity;
    private bool _isLocalOwner;
    private bool _localCoopHuman;
    private int _inputSlot;
    private Rigidbody _rb;
    private ISettingsService _settings;   // GDD §6.3 — マウス感度/Y反転を設定から取得
    private readonly List<RendererState> _hiddenRenderers = new();

    private struct RendererState
    {
        public RendererState(Renderer renderer)
        {
            Renderer = renderer;
            Enabled = renderer.enabled;
            ShadowCasting = renderer.shadowCastingMode;
        }

        public Renderer Renderer { get; }
        public bool Enabled { get; }
        public ShadowCastingMode ShadowCasting { get; }
    }

    private void Awake()
    {
        // インスペクターで未アサインの場合は子の CameraRig を自動検索
        if (_cameraRig == null)
            _cameraRig = transform.Find("CameraRig");

        if (_visualRoot == null)
            _visualRoot = transform.Find("ExplorerModel");

        // モデルのルート名が "ExplorerModel" でない場合のフォールバック。
        // Explorer ルート自体をビジュアルルートとして扱い、CameraRig 配下を除く
        // 全ボディメッシュを一人称非表示の対象にする（ApplyFirstPersonLocalVisuals 参照）。
        if (_visualRoot == null)
            _visualRoot = transform;

        _rb = GetComponent<Rigidbody>();
        _inputSlot = LocalCoopPartyMember.ResolveInputSlot(this);
    }

    /// <summary>ローカル Co-op の人間プレイヤーとしてカメラ・入力を有効化する。</summary>
    public void SetLocalCoopHuman(bool enabled)
    {
        _localCoopHuman = enabled;
        RefreshLocalOwnerState();
    }

    private void Start()
    {
        RefreshLocalOwnerState();
        if (!_isLocalOwner) return;

        GameplayCursorPolicy.SetGameplayMode();
        DisableRedundantAudioListener();
        ApplyFirstPersonLocalVisuals();

        Vector3 euler = transform.rotation.eulerAngles;
        _yaw = euler.y;
        _appliedYaw = _yaw;
        _pitch = NormalizeAngle(_cameraRig != null ? _cameraRig.localEulerAngles.x : 0f);
    }

    /// <summary>
    /// CameraRig が AudioListener を持つため、シーンの MainCamera 側の
    /// AudioListener を無効にして「AudioListener が 2 つ」警告を解消する。
    /// </summary>
    private void DisableRedundantAudioListener()
    {
        var mainCamObj = GameObject.FindWithTag("MainCamera");
        if (mainCamObj == null || mainCamObj.transform.IsChildOf(transform)) return;
        var listener = mainCamObj.GetComponent<AudioListener>();
        if (listener != null)
            listener.enabled = false;
    }

    private void Update()
    {
        // 入力スロットは毎フレーム再解決（Awake 時点は未構成で -1 になり得る）。
        _inputSlot = LocalCoopPartyMember.ResolveInputSlot(this);
        RefreshLocalOwnerState();
        if (!_isLocalOwner) return;

        if (_hideLocalBody && _hiddenRenderers.Count == 0)
            ApplyFirstPersonLocalVisuals();

        GameplayCursorPolicy.Enforce();
        HandleCursorToggleWhenNoPauseMenu();
        HandleSensitivityTuning();
    }

    private void LateUpdate()
    {
        if (!_isLocalOwner) return;
        if (_cameraRig == null) return;
        if (!GameplayCursorPolicy.AllowsCameraLook) return;

        Vector2 lookDelta = InputStateReader.ReadLookDelta(_inputSlot);
        lookDelta = Vector2.ClampMagnitude(lookDelta, _maxLookDeltaPerFrame);

        if (_useLookSmoothing)
        {
            _smoothedLookDelta = Vector2.SmoothDamp(
                _smoothedLookDelta,
                lookDelta,
                ref _smoothedLookVelocity,
                Mathf.Max(0.001f, _lookSmoothTime));
            lookDelta = _smoothedLookDelta;
        }
        else
        {
            _smoothedLookDelta = lookDelta;
        }

        // GDD §6.3 / §14.7 — 設定画面のマウス感度・Y反転を反映する。
        // 基準値(3.0)のとき実効感度は素のまま（既存の手触りを保つ）。スライダーが効くようになる。
        float settingsScale = ResolveSettingsSensitivityScale();
        float invertY = ResolveInvertY() ? -1f : 1f;

        float lookX = lookDelta.x * _sensitivityX * settingsScale;
        float lookY = lookDelta.y * _sensitivityY * settingsScale * invertY;

        _yaw += lookX;
        _pitch = Mathf.Clamp(_pitch - lookY, _minPitch, _maxPitch);

        // CameraRig を X 軸回転（視線上下）
        _cameraRig.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    private void FixedUpdate()
    {
        if (!_isLocalOwner) return;

        // Rigidbody 補間と競合しないよう、水平回転は物理ステップで適用する。
        if (Mathf.Abs(Mathf.DeltaAngle(_appliedYaw, _yaw)) < 0.0001f) return;

        Quaternion targetRotation = Quaternion.Euler(0f, _yaw, 0f);
        if (_rb != null && !_rb.isKinematic)
        {
            _rb.MoveRotation(targetRotation);
        }
        else
        {
            transform.rotation = targetRotation;
        }

        _appliedYaw = _yaw;
    }

    /// <summary>
    /// PauseMenu が無いシーン（オフライン検証など）では Esc でカーソル表示を切り替える。
    /// PauseMenu がある場合は Pause/Resume 側でカーソルを制御する。
    /// </summary>
    private void HandleCursorToggleWhenNoPauseMenu()
    {
        if (PauseMenu.IsPaused)
            return;

        if (!InputStateReader.EscapePressedThisFrame())
            return;

        if (FindFirstObjectByType<PauseMenu>() != null)
            return;

        GameplayCursorPolicy.ToggleMenuMode();
    }

    /// <summary>
    /// プレイ中に [ / ] キーで視点感度をライブ調整する。
    /// 環境（トラックパッド／マウス）に合わせて手早く詰められるようにするための開発支援。
    /// </summary>
    private void HandleSensitivityTuning()
    {
        float step = 0f;
        if (InputStateReader.LookSensitivityUpPressedThisFrame()) step += _sensitivityStep;
        if (InputStateReader.LookSensitivityDownPressedThisFrame()) step -= _sensitivityStep;

        if (Mathf.Approximately(step, 0f)) return;

        _sensitivityX = Mathf.Clamp(_sensitivityX + step, _minSensitivity, _maxSensitivity);
        _sensitivityY = Mathf.Clamp(_sensitivityY + step, _minSensitivity, _maxSensitivity);
        Debug.Log($"[ExplorerCameraLook] 視点感度 = {_sensitivityX:0.##}");
    }

    /// <summary>設定画面のマウス感度(0.5〜10.0)を基準値(3.0)で正規化した倍率。設定が無ければ 1.0。</summary>
    private float ResolveSettingsSensitivityScale()
    {
        _settings ??= GameServices.Settings;
        if (_settings == null || _settingsSensitivityReference <= 0f) return 1f;
        return _settings.MouseSensitivity / _settingsSensitivityReference;
    }

    /// <summary>設定画面の Y 軸反転フラグ（GDD §14.7）。設定が無ければ false。</summary>
    private bool ResolveInvertY()
    {
        _settings ??= GameServices.Settings;
        return _settings != null && _settings.InvertY;
    }

    private void OnDisable()
    {
        _smoothedLookDelta = Vector2.zero;
        _smoothedLookVelocity = Vector2.zero;
        RestoreLocalVisuals();
    }

    private void OnEnable()
    {
        RefreshLocalOwnerState();
        if (_isLocalOwner && _hideLocalBody)
            ApplyFirstPersonLocalVisuals();
    }

    private void ApplyFirstPersonLocalVisuals()
    {
        if (!_hideLocalBody)
            return;

        _hiddenRenderers.Clear();
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (renderer.transform.IsChildOf(_cameraRig))
                continue;

            if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer)
                continue;

            bool isBodyRenderer = renderer.gameObject == gameObject
                                  || (_visualRoot != null && renderer.transform.IsChildOf(_visualRoot));
            if (!isBodyRenderer)
                continue;

            _hiddenRenderers.Add(new RendererState(renderer));
            if (_preserveBodyShadows)
            {
                renderer.enabled = true;
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            else
            {
                renderer.enabled = false;
            }
        }
    }

    private void RestoreLocalVisuals()
    {
        if (!_isLocalOwner || _hiddenRenderers.Count == 0)
            return;

        for (int i = 0; i < _hiddenRenderers.Count; i++)
        {
            RendererState state = _hiddenRenderers[i];
            if (state.Renderer == null)
                continue;

            state.Renderer.enabled = state.Enabled;
            state.Renderer.shadowCastingMode = state.ShadowCasting;
        }

        _hiddenRenderers.Clear();
    }

    private void RefreshLocalOwnerState()
    {
        if (_localCoopHuman)
        {
            _isLocalOwner = true;
            return;
        }

        var member = GetComponent<LocalCoopPartyMember>();
        if (member != null)
        {
            _isLocalOwner = member.IsHumanControlled;
            return;
        }

        var netObj = GetComponent<NetworkObject>();
        _isLocalOwner = netObj == null || netObj.IsOwner;
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}
