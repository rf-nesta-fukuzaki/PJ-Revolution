using UnityEngine;

/// <summary>
/// GDD §3.2 — WeatherSystem の滑り係数をプレイヤーの PhysicsMaterial に反映するアダプター。
/// プレイヤーの Collider と同じ GameObject に追加する。
/// 雨(0.4) / 吹雪(0.7) の係数を dynamicFriction にマッピングして物理レイヤーと接続する。
/// </summary>
[RequireComponent(typeof(Collider))]
public class WeatherFrictionAdapter : MonoBehaviour
{
    [Header("摩擦設定")]
    [SerializeField] private float _baseFriction = 0.6f;   // 通常時の動摩擦係数
    [SerializeField] private float _minFriction  = 0.05f;  // 吹雪時の最低摩擦係数

    private const float MAX_SLIPPERINESS = 0.7f;   // IWeatherService.GetSliperiness() の最大値

    private PhysicsMaterial  _material;
    private Collider         _collider;
    private float            _weatherFriction;
    private bool             _hasHazardOverride;
    private float            _hazardFriction;
    private IWeatherService  _weather;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _weatherFriction = _baseFriction;

        // プレイヤー専用の PhysicsMaterial を生成（共有マテリアルを汚染しない）
        _material = new PhysicsMaterial("PlayerFriction")
        {
            dynamicFriction = _baseFriction,
            staticFriction  = _baseFriction * 1.2f,
            bounciness      = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum
        };
        _collider.material = _material;
        ApplyEffectiveFriction();
    }

    private void Start()
    {
        _weather = GameServices.Weather;
        if (_weather == null) return;

        _weather.OnWeatherChanged += OnWeatherChanged;
        OnWeatherChanged(_weather.CurrentWeather);
    }

    private void OnDestroy()
    {
        if (_weather != null)
            _weather.OnWeatherChanged -= OnWeatherChanged;

        if (_material != null)
            Destroy(_material);
    }

    private void OnWeatherChanged(WeatherType _)
    {
        if (_weather == null) return;

        float slipperiness = _weather.GetSliperiness();

        // slipperiness 0..MAX_SLIPPERINESS → friction _baseFriction.._minFriction
        float t = Mathf.Clamp01(slipperiness / MAX_SLIPPERINESS);
        _weatherFriction = Mathf.Lerp(_baseFriction, _minFriction, t);
        ApplyEffectiveFriction();
    }

    public void SetHazardFrictionOverride(float friction)
    {
        _hasHazardOverride = true;
        _hazardFriction = Mathf.Clamp(friction, 0f, _baseFriction);
        ApplyEffectiveFriction();
    }

    public void ClearHazardFrictionOverride()
    {
        if (!_hasHazardOverride) return;
        _hasHazardOverride = false;
        ApplyEffectiveFriction();
    }

    private void ApplyEffectiveFriction()
    {
        if (_material == null) return;

        float effective = _hasHazardOverride
            ? Mathf.Min(_weatherFriction, _hazardFriction)
            : _weatherFriction;

        _material.dynamicFriction = effective;
        _material.staticFriction  = effective * 1.2f;
    }
}
