using UnityEngine;

/// <summary>
/// GDD §5.2（遺物凍結ダメージ） — 遺物凍結ダメージシステム。
///
/// 吹雪中にサーマルケースに入っていない遺物が露出すると
/// RelicFreezeDamagePerSec dmg/秒 の凍結ダメージを受ける。
/// 対象ゾーン：ゾーン5-6（BlizzardAltitudeMin 以上）。
///
/// 設定値は EnvironmentHazardConfigSO に外部化されている。
/// WeatherSystem は Inspector で注入する（IWeatherService 経由）。
/// </summary>
[RequireComponent(typeof(RelicBase))]
public class RelicFreezeDamage : MonoBehaviour
{
    // ── データ駆動設定 ────────────────────────────────────────
    [Header("ハザード設定 (ScriptableObject)")]
    [SerializeField] private EnvironmentHazardConfigSO _hazardConfig;

    // ── 依存性注入（Inspector or Fallback） ───────────────────
    [Header("依存性（未設定なら GameServices.Weather にフォールバック）")]
    [SerializeField] private WeatherSystem _weatherSystemRef;
    private IWeatherService _weather;

    // ── コンポーネント ─────────────────────────────────────────
    private RelicBase _relic;

    // ── 状態 ─────────────────────────────────────────────────
    private int  _shelterCount;
    private bool _inThermalCase;

    public bool IsFreezeImmune => _shelterCount > 0 || _inThermalCase;

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        _relic = GetComponent<RelicBase>();
        Debug.Assert(_relic != null,
            "[RelicFreezeDamage] RelicBase が同一 GameObject に見つかりません");

        _weather = _weatherSystemRef != null
            ? (IWeatherService)_weatherSystemRef
            : GameServices.Weather;

        if (_weather == null)
            Debug.LogWarning("[RelicFreezeDamage] IWeatherService が見つかりません。凍結ダメージは無効です。");
    }

    private void Update()
    {
        if (_relic.IsDestroyed) return;
        if (IsFreezeImmune) return;
        if (_weather == null || !_weather.IsBlizzard) return;

        float altMin = _hazardConfig != null ? _hazardConfig.BlizzardAltitudeMin : 1600f;
        if (transform.position.y < altMin) return;

        float damage = _hazardConfig != null ? _hazardConfig.RelicFreezeDamagePerSec : 2f;
        _relic.ApplyEnvironmentalDamage(damage * Time.deltaTime);
    }

    // ── シェルター検出 ────────────────────────────────────────
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<ShelterZone>() != null)
        {
            _shelterCount++;
            Debug.Log($"[RelicFreeze] {_relic.RelicName} シェルター入場。凍結無効");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<ShelterZone>() != null)
            _shelterCount = Mathf.Max(0, _shelterCount - 1);
    }

    /// <summary>ThermalCase がアイテムを格納/取出したときに呼ぶ。</summary>
    public void SetInThermalCase(bool inCase)
    {
        _inThermalCase = inCase;
        Debug.Log($"[RelicFreeze] {_relic.RelicName} → サーマルケース格納={inCase}");
    }
}
