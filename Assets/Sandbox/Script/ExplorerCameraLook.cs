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
public class ExplorerCameraLook : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("Explorer の目線高さに置いた空の子オブジェクト。Main Camera を子にする。未設定時は子の CameraRig を自動検索する。")]
    [SerializeField] private Transform _cameraRig;
    [Tooltip("プレイヤーモデルのルート。未設定時は ExplorerModel を自動検索する。")]
    [SerializeField] private Transform _visualRoot;

    [Header("感度")]
    [SerializeField] private float _sensitivityX = 2f;
    [SerializeField] private float _sensitivityY = 2f;

    [Header("ピッチ制限 (度)")]
    [SerializeField] private float _minPitch = -80f;
    [SerializeField] private float _maxPitch =  80f;

    [Header("一人称ビジュアル")]
    [SerializeField] private bool _hideLocalBody = true;
    [SerializeField] private bool _preserveBodyShadows = true;

    private float _pitch;
    private bool _isLocalOwner;
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
    }

    private void Start()
    {
        RefreshLocalOwnerState();
        if (!_isLocalOwner) return;

        LockCursor();
        DisableRedundantAudioListener();
        ApplyFirstPersonLocalVisuals();
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
        if (_isLocalOwner && _hideLocalBody && _hiddenRenderers.Count == 0)
            ApplyFirstPersonLocalVisuals();

        HandleCursorLock();

        if (_cameraRig == null) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 lookDelta = InputStateReader.ReadLookDelta();
        float mouseX = lookDelta.x * _sensitivityX;
        float mouseY = lookDelta.y * _sensitivityY;

        // Explorer 本体を Y 軸回転（体の向き = カメラの水平向き）
        transform.Rotate(Vector3.up, mouseX, Space.World);

        // CameraRig を X 軸回転（視線上下）
        _pitch -= mouseY;
        _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
        _cameraRig.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    private void HandleCursorLock()
    {
        // 左クリックでロック
        if (InputStateReader.PrimaryPointerPressedThisFrame() && Cursor.lockState != CursorLockMode.Locked)
            LockCursor();

        // ESC でアンロック
        if (InputStateReader.EscapePressedThisFrame())
            UnlockCursor();
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDisable()
    {
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
        var netObj = GetComponent<NetworkObject>();
        _isLocalOwner = netObj == null || netObj.IsOwner;
    }
}
