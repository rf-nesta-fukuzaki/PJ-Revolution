using UnityEngine;

/// <summary>
/// Sandbox シーン用 Explorer WASD 移動コントローラー（Blend Tree 対応版）。
/// - Animator に float 型パラメーター "Speed"（0.0〜1.0）を SmoothDamp で滑らかに渡す。
/// - ExplorerCameraLook と組み合わせて Explorer ルートにアタッチする。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ExplorerController : MonoBehaviour
{
    [Header("移動")]
    [SerializeField] private float _moveSpeed = 5f;

    [Header("アニメーション")]
    [Tooltip("Speed パラメーターが 0⇔1 に変化するまでの時間（秒）。小さいほど俊敏。")]
    [SerializeField] private float _animSmoothTime = 0.1f;

    private Rigidbody _rb;
    private Animator  _animator;
    private Vector3   _moveInput;

    private float _currentSpeed;   // Animator に渡す現在値（SmoothDamp で補間）
    private float _speedVelocity;  // SmoothDamp 内部ステート

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;

        // FBX モデル子階層の Animator を自動取得
        _animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        float h = Input.GetAxisRaw("Horizontal"); // A/D
        float v = Input.GetAxisRaw("Vertical");   // W/S

        // Explorer 本体の向きに合わせた相対移動ベクトル（XZ 平面）
        _moveInput = (transform.right * h + transform.forward * v).normalized;

        // 目標 Speed: 入力があれば 1.0、なければ 0.0
        float targetSpeed = _moveInput.sqrMagnitude > 0.01f ? 1f : 0f;

        // SmoothDamp で滑らかに補間して Animator へ渡す
        _currentSpeed = Mathf.SmoothDamp(
            _currentSpeed, targetSpeed, ref _speedVelocity, _animSmoothTime);

        _animator?.SetFloat(SpeedHash, _currentSpeed);
    }

    private void FixedUpdate()
    {
        if (_moveInput.sqrMagnitude < 0.01f) return;

        Vector3 target = _rb.position + _moveInput * (_moveSpeed * Time.fixedDeltaTime);
        _rb.MovePosition(target);
    }
}
