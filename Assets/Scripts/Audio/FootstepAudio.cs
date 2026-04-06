using UnityEngine;

/// <summary>
/// プレイヤーの移動に連動して足音 SE を再生する。
/// AudioManager.PlaySE を使用する。
/// </summary>
public class FootstepAudio : MonoBehaviour
{
    [Header("再生設定")]
    [SerializeField] private float stepInterval = 0.4f;
    [SerializeField] private float stepThreshold = 0.3f;
    [SerializeField] private float runSpeedThreshold = 4f;

    private PlayerMovement _movement;
    private float _stepTimer;
    private bool _wasGrounded;
    private Vector3 _lastPosition;

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        if (_movement == null) return;

        bool isGrounded = _movement.IsGrounded;
        float speed = CalcHorizontalSpeed();

        // 着地検出
        if (!_wasGrounded && isGrounded)
            AudioManager.Instance?.PlaySE("land");
        _wasGrounded = isGrounded;

        // 足音
        if (isGrounded && speed >= stepThreshold)
        {
            _stepTimer += Time.deltaTime;
            float interval = stepInterval * Mathf.Clamp(3f / Mathf.Max(speed, 1f), 0.2f, 1f);
            if (_stepTimer >= interval)
            {
                _stepTimer = 0f;
                AudioManager.Instance?.PlaySE("footstep");
            }
        }
        else
        {
            _stepTimer = 0f;
        }

        _lastPosition = transform.position;
    }

    private float CalcHorizontalSpeed()
    {
        Vector3 delta = transform.position - _lastPosition;
        delta.y = 0f;
        return delta.magnitude / Mathf.Max(Time.deltaTime, 0.001f);
    }
}
