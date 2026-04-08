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
    [Tooltip("Explorer の目線高さに置いた空の子オブジェクト。Main Camera を子にする。")]
    [SerializeField] private Transform _cameraRig;

    [Header("感度")]
    [SerializeField] private float _sensitivityX = 2f;
    [SerializeField] private float _sensitivityY = 2f;

    [Header("ピッチ制限 (度)")]
    [SerializeField] private float _minPitch = -80f;
    [SerializeField] private float _maxPitch =  80f;

    private float _pitch;

    private void Start()
    {
        LockCursor();
    }

    private void Update()
    {
        HandleCursorLock();

        if (Cursor.lockState != CursorLockMode.Locked) return;

        float mouseX = Input.GetAxis("Mouse X") * _sensitivityX;
        float mouseY = Input.GetAxis("Mouse Y") * _sensitivityY;

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
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            LockCursor();

        // ESC でアンロック
        if (Input.GetKeyDown(KeyCode.Escape))
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
