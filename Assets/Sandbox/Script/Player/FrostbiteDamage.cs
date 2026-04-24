using UnityEngine;

/// <summary>
/// GDD §3.4 — 凍傷ダメージシステム。
/// 吹雪中（WeatherType.Blizzard）にシェルターなしで露出していると
/// FrostDamagePerSec dmg/秒 のダメージを受ける。対象ゾーン：ゾーン5-6。
///
/// 設定値は EnvironmentHazardConfigSO に外部化されている。
/// WeatherSystem は Inspector で注入する（IWeatherService 経由）。
///
/// シェルターとして認識されるもの：
///   - ShelterZone コンポーネントを持つコライダー（BivouacTent 展開時に有効化）
///   - SafeZone コンポーネントを持つコライダー（洞窟・建物内部）
/// </summary>
[RequireComponent(typeof(PlayerHealthSystem))]
public class FrostbiteDamage : MonoBehaviour
{
    // ── データ駆動設定 ────────────────────────────────────────
    [Header("ハザード設定 (ScriptableObject)")]
    [SerializeField] private EnvironmentHazardConfigSO _hazardConfig;

    // ── 依存性注入（Inspector or Fallback）────────────────────
    [Header("依存性（未設定なら Instance にフォールバック）")]
    [SerializeField] private WeatherSystem _weatherSystemRef;
    private IWeatherService _weather;

    // ── コンポーネント ─────────────────────────────────────────
    private PlayerHealthSystem _health;
    private GhostSystem        _ghost;

    // ── 状態 ─────────────────────────────────────────────────
    private int  _shelterCount;
    private bool IsSheltered => _shelterCount > 0;

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        _health = GetComponent<PlayerHealthSystem>();
        _ghost  = GetComponent<GhostSystem>();

        _weather = _weatherSystemRef != null
            ? (IWeatherService)_weatherSystemRef
            : GameServices.Weather;

        if (_weather == null)
            Debug.LogWarning("[FrostbiteDamage] IWeatherService が見つかりません。凍傷ダメージは無効です。");

        if (_hazardConfig == null)
            Debug.LogWarning("[FrostbiteDamage] EnvironmentHazardConfigSO が未設定です。デフォルト値が使用されます。");
    }

    private void Update()
    {
        if (_health.IsDead) return;
        if (_ghost != null && _ghost.IsGhost) return;
        if (IsSheltered) return;
        if (_weather == null || !_weather.IsBlizzard) return;

        float altMin = _hazardConfig != null ? _hazardConfig.BlizzardAltitudeMin : 1600f;
        if (transform.position.y < altMin) return;

        float damage = _hazardConfig != null ? _hazardConfig.FrostDamagePerSec : 5f;
        _health.TakeDamage(damage * Time.deltaTime);
    }

    // ── シェルター検出 ────────────────────────────────────────
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<ShelterZone>() != null)
        {
            _shelterCount++;
            Debug.Log($"[Frostbite] シェルター入場 ({_shelterCount} 個目)。凍傷無効");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<ShelterZone>() != null)
        {
            _shelterCount = Mathf.Max(0, _shelterCount - 1);
            if (_shelterCount == 0)
                Debug.Log("[Frostbite] シェルター退出。露出中");
        }
    }

    // ── デバッグ用プロパティ ──────────────────────────────────
    public bool IsShelteredPublic  => IsSheltered;
    public bool IsAtRiskAltitude   =>
        transform.position.y >= (_hazardConfig != null ? _hazardConfig.BlizzardAltitudeMin : 1600f);
}
