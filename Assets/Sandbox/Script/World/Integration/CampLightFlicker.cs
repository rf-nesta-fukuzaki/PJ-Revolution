using UnityEngine;

namespace Sandbox.World.Integration
{
    /// <summary>
    /// 焚き火・ランタンの点光源と炎メッシュに揺らぎを与える軽量コンポーネント。
    /// Perlin ノイズで強度を脈動させ、任意で炎トランスフォームを上下＋スケールで揺らす。
    /// 影は焼かない（PointLight 側で shadows=None）ため負荷は無視できる。
    /// </summary>
    internal sealed class CampLightFlicker : MonoBehaviour
    {
        private Light _light;
        private Transform _flame;
        private float _baseIntensity;
        private Vector3 _flameBaseScale;
        private float _seed;
        private float _speed;
        private float _amount;

        public void Init(Light light, Transform flame, float amount = 0.35f, float speed = 7f)
        {
            _light = light;
            _baseIntensity = light != null ? light.intensity : 0f;
            _flame = flame;
            _flameBaseScale = flame != null ? flame.localScale : Vector3.one;
            _amount = amount;
            _speed = speed;
            _seed = Random.value * 100f;
        }

        private void Update()
        {
            float t = Time.time * _speed + _seed;
            float n = Mathf.PerlinNoise(t, _seed) * 2f - 1f;          // -1..1
            float n2 = Mathf.PerlinNoise(_seed, t * 0.7f) * 2f - 1f;

            if (_light != null)
                _light.intensity = _baseIntensity * (1f + n * _amount);

            if (_flame != null)
            {
                float s = 1f + n2 * _amount * 0.4f;
                _flame.localScale = new Vector3(_flameBaseScale.x, _flameBaseScale.y * s, _flameBaseScale.z);
            }
        }
    }
}
