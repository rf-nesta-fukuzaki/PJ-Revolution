using UnityEngine;

namespace Sandbox.World.Integration
{
    /// <summary>
    /// プレイヤーカメラに「加算」でシェイク offset を載せる非破壊コンポーネント。
    /// ExplorerCameraLook が LateUpdate で毎フレーム base にリセットする想定で、その後（order 200）で
    /// offset を足すだけにすれば累積しない。位置シェイクのみ採用（回転はカメラ制御側と競合するため不使用）。
    ///
    /// L 退役後の自動トリガーは Rigidbody のみで検出:
    ///  - 着地: Y 速度が大きな負→0 付近に戻った瞬間、直前の落下速度に比例して trauma を加算
    ///  - 落下死: プレイヤー Y が <see cref="fallDeathShakeY"/> 未満で強 trauma（地上復帰で再武装）
    /// ロープ接続シェイクは Step 3.2 以降で P 側ロープと統合する際に再追加予定。
    /// 外部からは AddTrauma(amount) で任意に揺らせる（Director の山頂祝祭シェイク等）。
    /// </summary>
    [DefaultExecutionOrder(200)] // カメラ制御の LateUpdate より後に加算
    public sealed class SandboxCameraShake : MonoBehaviour
    {
        [Header("Shake")]
        [SerializeField] private float traumaDecay = 2.2f;   // trauma /sec
        [SerializeField] private float maxOffset = 0.35f;    // m（trauma=1 時の最大変位）
        [SerializeField] private float frequency = 22f;      // Perlin 進行速度

        [Header("Auto Triggers")]
        [Tooltip("これ以上の落下速度[m/s]で着地シェイク開始。")]
        [SerializeField] private float landMinFallSpeed = 6f;
        [Tooltip("この落下速度[m/s]で trauma=1。")]
        [SerializeField] private float landMaxFallSpeed = 28f;
        [Tooltip("Y 速度がこの値より上に戻ったら『着地した』とみなす。")]
        [SerializeField] private float landDetectVy = -1f;
        [Tooltip("プレイヤー Y がこれ未満になったら落下死シェイク（強）。")]
        [SerializeField] private float fallDeathShakeY = -15f;
        [SerializeField] private float fallDeathTrauma = 1f;

        private Rigidbody _rb;
        private Transform _playerRoot;
        private float _trauma;
        private bool _wasFalling;
        private float _lastFallSpeed;
        private bool _fallShakeFired;
        private float _seedX, _seedY;

        private void Awake()
        {
            _rb = GetComponentInParent<Rigidbody>();
            _playerRoot = _rb != null ? _rb.transform : transform;
            _seedX = Random.value * 100f;
            _seedY = Random.value * 100f + 50f;
        }

        public void AddTrauma(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

        private void Update()
        {
            // 着地検出: Y 速度が大きな負→0 付近に戻った瞬間に trauma 加算
            if (_rb != null)
            {
                float vy = _rb.linearVelocity.y;
                bool falling = vy < landDetectVy;
                if (falling) _lastFallSpeed = -vy;
                if (!falling && _wasFalling && _lastFallSpeed > 0f)
                {
                    float s = Mathf.InverseLerp(landMinFallSpeed, landMaxFallSpeed, _lastFallSpeed);
                    if (s > 0f) AddTrauma(s);
                    _lastFallSpeed = 0f;
                }
                _wasFalling = falling;
            }

            // 落下死シェイク（しきい値 Y を一度下回ったら強 trauma。地上復帰で再武装）
            if (_playerRoot != null)
            {
                float y = _playerRoot.position.y;
                if (!_fallShakeFired && y < fallDeathShakeY)
                {
                    AddTrauma(fallDeathTrauma);
                    _fallShakeFired = true;
                }
                else if (_fallShakeFired && y > fallDeathShakeY + 10f)
                {
                    _fallShakeFired = false; // リスポーン後に再武装
                }
            }
        }

        private void LateUpdate()
        {
            if (_trauma <= 0f) return;
            float shake = _trauma * _trauma; // 二乗で自然な減衰
            float t = Time.time * frequency;
            float ox = (Mathf.PerlinNoise(_seedX, t) - 0.5f) * 2f;
            float oy = (Mathf.PerlinNoise(_seedY, t) - 0.5f) * 2f;
            transform.localPosition += new Vector3(ox, oy, 0f) * (maxOffset * shake);
            _trauma = Mathf.Max(0f, _trauma - traumaDecay * Time.deltaTime);
        }
    }
}
