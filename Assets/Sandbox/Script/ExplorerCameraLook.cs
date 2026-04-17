using Unity.Netcode;
using UnityEngine;

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

    [Header("感度")]
    [SerializeField] private float _sensitivityX = 2f;
    [SerializeField] private float _sensitivityY = 2f;

    [Header("ピッチ制限 (度)")]
    [SerializeField] private float _minPitch = -80f;
    [SerializeField] private float _maxPitch =  80f;

    private float _pitch;

    private void Awake()
    {
        // インスペクターで未アサインの場合は子の CameraRig を自動検索
        if (_cameraRig == null)
            _cameraRig = transform.Find("CameraRig");
    }

    private void Start()
    {
        // ローカルオーナーのみカーソルロックと重複 AudioListener 解消を行う
        var netObj = GetComponent<NetworkObject>();
        bool isLocalOwner = netObj == null || netObj.IsOwner;
        if (!isLocalOwner) return;

        LockCursor();
        DisableRedundantAudioListener();
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
}
