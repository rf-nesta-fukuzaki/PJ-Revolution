using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 一人称視点操作。
/// Update → LateUpdate に変更し、SmoothStepOffset でカメラ Y を補正する。
/// </summary>
public class FirstPersonLook : MonoBehaviour
{
    [Header("感度設定")]
    [SerializeField] private float sensitivityX = 2f;
    [SerializeField] private float sensitivityY = 2f;

    [Header("参照設定（省略時は子から自動取得）")]
    [SerializeField] private Transform cameraRig;

    [Header("ヒント表示")]
    [SerializeField] private int hintFontSize = 18;

    private float _pitch;
    private float _yaw;

    private PlayerStateManager _stateManager;
    private PlayerMovement _playerMovement;
    private float _cameraYOffset;
    private float _cameraBaseY;

    private GUIStyle _hintStyle;
    private bool _skipMouseInput;

    private void Awake()
    {
        _stateManager = GetComponent<PlayerStateManager>();
        _playerMovement = GetComponent<PlayerMovement>();
        AutoFindReferences();
    }

    private void Start()
    {
        _yaw = transform.eulerAngles.y;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (cameraRig != null)
            _cameraBaseY = cameraRig.localPosition.y;
    }

    // Update → LateUpdate に変更（CLAUDE.md 仕様）
    private void LateUpdate()
    {
        HandleCursorLock();

        if (Cursor.lockState == CursorLockMode.Locked && !_skipMouseInput)
            ApplyMouseLook();

        _skipMouseInput = false;

        // SmoothStepOffset でカメラ Y を補正
        if (_playerMovement != null)
        {
            float stepOffset = _playerMovement.SmoothStepOffset;
            _cameraYOffset = Mathf.Lerp(_cameraYOffset, 0f, Time.deltaTime * 15f);

            if (cameraRig != null)
                cameraRig.localPosition = new Vector3(0f, _cameraBaseY + _cameraYOffset + stepOffset, 0f);
        }
    }

    private void OnGUI()
    {
        if (Cursor.lockState == CursorLockMode.Locked) return;

        if (_hintStyle == null)
        {
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = hintFontSize,
                normal = { textColor = Color.white },
            };
        }

        float w = 400f, h = 36f;
        GUI.Label(
            new Rect((Screen.width - w) / 2f, Screen.height - h - 20f, w, h),
            "ゲーム画面をクリックして開始 (ESC でカーソル解除)",
            _hintStyle);
    }

    private void ApplyMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * sensitivityX;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivityY;

        _yaw += mouseX;
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

        _pitch -= mouseY;
        _pitch = Mathf.Clamp(_pitch, -90f, 90f);

        if (cameraRig != null)
            cameraRig.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    private void HandleCursorLock()
    {
        bool isSwinging = _stateManager != null &&
                          _stateManager.CurrentState == PlayerState.Swinging;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _skipMouseInput = true;
            _yaw = transform.eulerAngles.y;
            return;
        }

        if (!Application.isFocused)
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _yaw = transform.eulerAngles.y;
            }
            return;
        }

        if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.None)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void SetSensitivity(float s)
    {
        sensitivityX = s;
        sensitivityY = s;
    }

    private void AutoFindReferences()
    {
        if (cameraRig == null)
            cameraRig = GetComponentInChildren<Camera>()?.transform;
    }
}
