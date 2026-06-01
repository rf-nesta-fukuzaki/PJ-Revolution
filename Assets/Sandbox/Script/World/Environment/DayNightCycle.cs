using UnityEngine;

namespace Sandbox.World.Environment
{
    /// <summary>
    /// TimeOfDay を実時間で進行させ、AtmosphericProfileController に反映する。
    /// - enableCycle = false なら停止（Step 4 までと同じ静止挙動）
    /// - dayLengthSeconds で 1 日の長さを指定（既定 600 秒 = 10 分）
    /// - startTimeOfDay で開始時刻を指定（0=夜明け, 0.5=正午, 1=夜）
    /// - loop なら 1.0 で 0.0 に戻る
    /// </summary>
    [DefaultExecutionOrder(-24)] // AtmosphericProfileController(-25) の後
    [RequireComponent(typeof(AtmosphericProfileController))]
    public sealed class DayNightCycle : MonoBehaviour
    {
        [SerializeField] private bool enableCycle = true;
        [Tooltip("0=夜明け 0.5=正午 1=夜")]
        [Range(0f, 1f)] [SerializeField] private float startTimeOfDay = 0.46f;
        [Tooltip("1 日 (TimeOfDay 0→1) を何秒かけて進むか。登攀1ランが明るい時間に収まるよう長め。")]
        [SerializeField] private float dayLengthSeconds = 1800f;
        [SerializeField] private bool loop = true;
        // PEAK 風の「常に明るく映える」見た目を保つため、夜まで沈む full loop ではなく
        // 昼間の帯(朝〜黄金の午後)を往復させる。loop でフルサイクルすると TimeOfDay が 0.85+ の夜帯へ入り、
        // 太陽強度 0.05・空ほぼ黒のスクショに使えない画になる（実測 time=33s で夜）。pingPong 既定 ON で回避。
        [Tooltip("狭い帯を往復（昼間だけ往復）。PEAK 風に常時明るく保つため既定 ON。enableCycle が前提。")]
        [SerializeField] private bool pingPong = true;
        [Tooltip("往復下限。0.30=やわらかい朝（夜・夜明け前の暗さに入らない下限）。")]
        [Range(0f, 1f)] [SerializeField] private float pingPongMin = 0.30f;
        [Tooltip("往復上限。0.62=黄金の午後（夕暮れ手前で、空も地形も鮮やかに保てる上限）。")]
        [Range(0f, 1f)] [SerializeField] private float pingPongMax = 0.62f;

        private AtmosphericProfileController _atmos;
        private float _t;
        private int _dir = 1;

        public float TimeOfDay => _t;

        private void Awake()
        {
            _atmos = GetComponent<AtmosphericProfileController>();
            _t = startTimeOfDay;
            if (_atmos != null) _atmos.TimeOfDay = _t;
        }

        private void Update()
        {
            if (!enableCycle || _atmos == null || dayLengthSeconds <= 0f) return;
            float dt = Time.deltaTime / dayLengthSeconds;
            if (pingPong)
            {
                _t += dt * _dir;
                if (_t >= pingPongMax) { _t = pingPongMax; _dir = -1; }
                else if (_t <= pingPongMin) { _t = pingPongMin; _dir = +1; }
            }
            else
            {
                _t += dt;
                if (_t >= 1f) _t = loop ? _t - 1f : 1f;
            }
            _atmos.TimeOfDay = _t;
        }
    }
}
