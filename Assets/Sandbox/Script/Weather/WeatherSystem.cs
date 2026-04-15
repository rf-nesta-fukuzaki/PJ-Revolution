using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// GDD §3.2 / §7.1 L4 — 天候システム。
/// 晴/曇/霧/雨/吹雪サイクル + 風向き・風速をランダムに変化させる。
/// Rigidbody コンポーネントを持つ全オブジェクトに風力を適用する。
/// </summary>
public class WeatherSystem : MonoBehaviour
{
    public static WeatherSystem Instance { get; private set; }

    // ── Inspector ───────────────────────────────────────────
    [Header("天候サイクル")]
    [SerializeField] private float _minWeatherDuration = 60f;   // 最短継続時間（秒）
    [SerializeField] private float _maxWeatherDuration = 180f;

    [Header("風")]
    [SerializeField] private float _baseWindSpeed     = 3f;
    [SerializeField] private float _maxWindSpeed      = 20f;
    [SerializeField] private float _windChangeSpeed   = 0.5f;   // 風向きの変化速度

    [Header("視界")]
    [SerializeField] private float _baseFogDensity    = 0.005f;
    [SerializeField] private float _blizzardFogDensity = 0.06f;

    [Header("Particle Systems")]
    [SerializeField] private ParticleSystem _rainParticles;
    [SerializeField] private ParticleSystem _snowParticles;

    // ── 状態 ────────────────────────────────────────────────
    private WeatherType  _currentWeather;
    private float        _weatherTimer;
    private Vector3      _currentWindDir;
    private float        _currentWindSpeed;
    private float        _targetWindSpeed;
    private float        _windPhase;

    // 風向きをランダムに変化させるためのノイズフェーズ
    private float _windDirPhaseX;
    private float _windDirPhaseZ;

    public WeatherType  CurrentWeather  => _currentWeather;
    public Vector3      CurrentWind     => _currentWindDir * _currentWindSpeed;
    public float        WindSpeed       => _currentWindSpeed;
    public bool         IsBlizzard      => _currentWeather == WeatherType.Blizzard;
    public bool         IsRainy         => _currentWeather == WeatherType.Rain;

    public event Action<WeatherType> OnWeatherChanged;

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        ChangeWeather(WeatherType.Sunny);
        _currentWindDir   = Random.insideUnitSphere.normalized;
        _currentWindDir.y = 0f;
        _currentWindSpeed = _baseWindSpeed;
        _targetWindSpeed  = _baseWindSpeed;
    }

    private void Update()
    {
        // 天候タイマー
        _weatherTimer -= Time.deltaTime;
        if (_weatherTimer <= 0f)
            ChangeWeather(PickNextWeather());

        // 風の更新
        UpdateWind();

        // フォグ更新
        UpdateFog();
    }

    private void FixedUpdate()
    {
        ApplyWindToRigidbodies();
    }

    // ── 天候遷移 ─────────────────────────────────────────────
    private void ChangeWeather(WeatherType next)
    {
        _currentWeather = next;
        _weatherTimer   = Random.Range(_minWeatherDuration, _maxWeatherDuration);

        _targetWindSpeed = next switch
        {
            WeatherType.Sunny   => _baseWindSpeed * Random.Range(0.5f, 1f),
            WeatherType.Cloudy  => _baseWindSpeed * Random.Range(1f,   1.5f),
            WeatherType.Fog     => _baseWindSpeed * Random.Range(0.3f, 0.8f),
            WeatherType.Rain    => _baseWindSpeed * Random.Range(1.5f, 2.5f),
            WeatherType.Blizzard => _maxWindSpeed * Random.Range(0.7f, 1f),
            _                   => _baseWindSpeed
        };

        // Particle Systems の切り替え（Inspector 未設定時は安全にスキップ）
        if (_rainParticles != null) _rainParticles.gameObject.SetActive(next == WeatherType.Rain);
        if (_snowParticles != null) _snowParticles.gameObject.SetActive(next == WeatherType.Blizzard);

        OnWeatherChanged?.Invoke(next);
        Debug.Log($"[Weather] 天候変化: {next}  風速目標: {_targetWindSpeed:F1} m/s");
    }

    private WeatherType PickNextWeather()
    {
        // 重み付き抽選
        float r = Random.value;
        return r switch
        {
            < 0.3f => WeatherType.Sunny,
            < 0.5f => WeatherType.Cloudy,
            < 0.65f => WeatherType.Fog,
            < 0.80f => WeatherType.Rain,
            _       => WeatherType.Blizzard
        };
    }

    // ── 風の更新 ─────────────────────────────────────────────
    private void UpdateWind()
    {
        // 風速を目標値に向けてスムーズに変化
        _currentWindSpeed = Mathf.Lerp(_currentWindSpeed, _targetWindSpeed,
                                        Time.deltaTime * _windChangeSpeed);

        // 風向きをゆっくり変化（Perlin Noise で滑らか）
        _windDirPhaseX += Time.deltaTime * 0.08f;
        _windDirPhaseZ += Time.deltaTime * 0.06f;

        float dx = (Mathf.PerlinNoise(_windDirPhaseX, 0f) - 0.5f) * 2f;
        float dz = (Mathf.PerlinNoise(0f, _windDirPhaseZ) - 0.5f) * 2f;

        Vector3 targetDir = new Vector3(dx, 0f, dz).normalized;
        _currentWindDir = Vector3.Slerp(_currentWindDir, targetDir,
                                         Time.deltaTime * _windChangeSpeed * 0.5f);
    }

    // ── フォグ ────────────────────────────────────────────────
    private void UpdateFog()
    {
        float targetDensity = _currentWeather switch
        {
            WeatherType.Fog      => _baseFogDensity * 6f,
            WeatherType.Blizzard => _blizzardFogDensity,
            WeatherType.Rain     => _baseFogDensity * 2f,
            WeatherType.Cloudy   => _baseFogDensity * 1.5f,
            _                    => _baseFogDensity
        };

        RenderSettings.fogDensity = Mathf.Lerp(
            RenderSettings.fogDensity, targetDensity, Time.deltaTime * 0.5f);
        RenderSettings.fog = true;
    }

    // ── 風力適用 ─────────────────────────────────────────────
    private Rigidbody[]      _cachedRigidbodies;
    private CrystalCupRelic[] _cachedCrystalCups;
    private float             _rbCacheTimer;
    private const float       RB_CACHE_INTERVAL    = 3f;
    private const float       CRYSTAL_CUP_MIN_WIND = 5f; // この風速以上でクリスタルを揺さぶる

    private void ApplyWindToRigidbodies()
    {
        // キャッシュ更新（3 秒ごと）
        _rbCacheTimer -= Time.fixedDeltaTime;
        if (_rbCacheTimer <= 0f)
        {
            _cachedRigidbodies = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
            _cachedCrystalCups = FindObjectsByType<CrystalCupRelic>(FindObjectsSortMode.None);
            _rbCacheTimer      = RB_CACHE_INTERVAL;
        }

        if (_cachedRigidbodies == null) return;

        Vector3 wind = CurrentWind;

        foreach (var rb in _cachedRigidbodies)
        {
            if (rb == null || rb.isKinematic) continue;

            // シェルター（ビバークテント等）保護下のオブジェクトは風の影響を受けない
            if (_shelterOccupants.Contains(rb.transform.root.gameObject)) continue;

            // 軽いオブジェクトほど影響大
            float windFactor = 1f / Mathf.Max(rb.mass, 0.5f);
            rb.AddForce(wind * windFactor * 0.1f, ForceMode.Acceleration);
        }

        // クリスタルの杯：保持中に強風で手から飛びそうになる（GDD §6.2）
        if (_currentWindSpeed >= CRYSTAL_CUP_MIN_WIND && _cachedCrystalCups != null)
        {
            Vector3 windDir = CurrentWind.normalized;
            foreach (var cup in _cachedCrystalCups)
            {
                if (cup == null) continue;
                cup.ApplyWindKnock(windDir);
            }
        }
    }

    // ── 外部クエリ ────────────────────────────────────────────
    /// <summary>現在の滑り係数（雨/吹雪で上昇）。</summary>
    public float GetSliperiness()
    {
        return _currentWeather switch
        {
            WeatherType.Rain     => 0.4f,
            WeatherType.Blizzard => 0.7f,
            _                    => 0f
        };
    }

    // ── シェルター管理（BivouacTent など） ───────────────────
    private readonly System.Collections.Generic.HashSet<GameObject> _shelterOccupants = new();

    /// <summary>天候シェルター（テント等）に入ったオブジェクトを登録。</summary>
    public void AddShelterOccupant(GameObject go)
    {
        _shelterOccupants.Add(go);
    }

    /// <summary>シェルターから出たオブジェクトを除外。</summary>
    public void RemoveShelterOccupant(GameObject go)
    {
        _shelterOccupants.Remove(go);
    }

    /// <summary>指定オブジェクトがシェルター保護下にあるか判定。</summary>
    public bool IsInShelter(GameObject go) => _shelterOccupants.Contains(go);
}

public enum WeatherType { Sunny, Cloudy, Fog, Rain, Blizzard }
