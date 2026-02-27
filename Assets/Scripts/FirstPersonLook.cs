using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// First Person 視点操作を担当するコンポーネント。
///
/// [責務]
///   - マウス X → プレイヤー本体の Y 軸回転（体の向き）
///   - マウス Y → CameraRig の X 軸回転（視線の上下、-90〜90度クランプ）
///   - TorchPivot もカメラと同じ仰角で回転させる
///   - カーソルロック / アンロック制御
///
/// [カーソルロックの動作]
///   - 起動直後はカーソルがフリー
///   - ゲーム画面（UI 以外）を左クリックするとロックされ視点操作が有効になる
///   - ESC キーでいつでもアンロック
///   - Downed 中・フォーカス外はロックを強制解除する
/// </summary>
public class FirstPersonLook : MonoBehaviour
{
    // ─────────────── Inspector ───────────────

    [Header("感度設定")]
    [Tooltip("左右 (Y軸) 回転の感度")]
    [SerializeField] private float sensitivityX = 2f;

    [Tooltip("上下 (X軸) 回転の感度")]
    [SerializeField] private float sensitivityY = 2f;

    [Header("参照設定 (省略時は子から自動取得)")]
    [Tooltip("カメラを持つ CameraRig Transform")]
    [SerializeField] private Transform cameraRig;

    [Tooltip("ライトを持つ TorchPivot Transform")]
    [SerializeField] private Transform torchPivot;

    [Header("カーソルロック解除時のヒント表示")]
    [Tooltip("ヒントテキストのフォントサイズ")]
    [SerializeField] private int hintFontSize = 18;

    // ─────────────── 内部状態 ───────────────

    private float _pitch;
    private float _yaw;

    private PlayerStateManager _stateManager;

    private GUIStyle _hintStyle;
    private bool     _skipMouseInput;

    // ─────────────── Unity Lifecycle ───────────────

    private void Awake()
    {
        _stateManager = GetComponent<PlayerStateManager>();
        AutoFindReferences();
    }

    private void Start()
    {
        _yaw = transform.eulerAngles.y;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        AutoFindReferences();
    }

    private void Update()
    {
        HandleCursorLock();

        if (Cursor.lockState == CursorLockMode.Locked && !_skipMouseInput)
            ApplyMouseLook();

        _skipMouseInput = false;
    }

    // ─────────────── OnGUI（ヒント表示） ───────────────

    private void OnGUI()
    {
        if (Cursor.lockState == CursorLockMode.Locked) return;

        if (_hintStyle == null)
        {
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize   = hintFontSize,
                normal     = { textColor = Color.white },
            };
        }

        float w = 400f;
        float h = 36f;
        GUI.Label(
            new Rect((Screen.width - w) / 2f, Screen.height - h - 20f, w, h),
            "ゲーム画面をクリックして開始 (ESC でカーソル解除)",
            _hintStyle);
    }

    // ─────────────── 視点操作 ───────────────

    private void ApplyMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * sensitivityX;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivityY;

        _yaw += mouseX;
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

        _pitch -= mouseY;
        _pitch  = Mathf.Clamp(_pitch, -90f, 90f);

        if (cameraRig  != null) cameraRig.localRotation  = Quaternion.Euler(_pitch, 0f, 0f);
        if (torchPivot != null) torchPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    // ─────────────── カーソルロック ───────────────

    private void HandleCursorLock()
    {
        bool isDowned = _stateManager != null &&
                        _stateManager.CurrentState == PlayerState.Downed;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
            _skipMouseInput  = true;
            SyncYawFromTransform();
            return;
        }

        if (!Application.isFocused || isDowned)
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible   = true;
                SyncYawFromTransform();
            }
            return;
        }

        if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.None)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }
    }

    // ─────────────── 公開 API ───────────────

    /// <summary>マウス感度を設定する。OptionsUI から呼ばれる。</summary>
    public void SetSensitivity(float sensitivity)
    {
        sensitivityX = sensitivity;
        sensitivityY = sensitivity;
    }

    // ─────────────── 内部ユーティリティ ───────────────

    private void AutoFindReferences()
    {
        if (cameraRig  == null) cameraRig  = GetComponentInChildren<Camera>()?.transform;
        if (torchPivot == null) torchPivot = GetComponentInChildren<TorchSystem>()?.transform;
    }

    private void SyncYawFromTransform()
    {
        _yaw = transform.eulerAngles.y;
    }
}
