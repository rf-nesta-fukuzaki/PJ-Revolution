using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using PeakPlunder.Localization;

/// <summary>
/// GDD §10.6 — ベースキャンプの天候ボード。
///
/// 機能:
///   - 常時表示の簡易モード（天候名 + 風速）
///   - プレイヤーが近づくと詳細モードに展開（予報・推奨装備・ルート状況）
///   - WeatherSystem の OnWeatherChanged をサブスクライブし即時更新
///   - RouteGate を Scene から収集し開閉状況を列挙
///
/// 使用法:
///   Basecamp のサインボードに本コンポーネントを付け、
///   compactRoot / expandedRoot を WorldSpace Canvas として割り当てる。
///
///   近接判定は "Player" タグ（PlayerInteraction 互換）で行う。
/// </summary>
public class WeatherBoardManager : MonoBehaviour
{
    [Header("UI ルート")]
    [SerializeField] private GameObject _compactRoot;
    [SerializeField] private GameObject _expandedRoot;

    [Header("簡易モード表示")]
    [SerializeField] private TextMeshProUGUI _compactWeatherLabel;
    [SerializeField] private TextMeshProUGUI _compactWindLabel;

    [Header("詳細モード表示")]
    [SerializeField] private TextMeshProUGUI _expandedWeatherLabel;
    [SerializeField] private TextMeshProUGUI _expandedWindLabel;
    [SerializeField] private TextMeshProUGUI _expandedRecommendationLabel;
    [SerializeField] private TextMeshProUGUI _expandedRouteStatusLabel;

    [Header("近接展開")]
    [Tooltip("詳細モードに展開する半径 (GDD §10.6 = 3m)")]
    [SerializeField] private float _expandRadius = 3f;

    [Tooltip("近接判定のサンプリング間隔 (秒)")]
    [SerializeField] private float _proximityCheckInterval = 0.25f;

    [Header("アイコン (Sunny/Cloudy/Fog/Rain/Blizzard の順)")]
    [SerializeField] private GameObject[] _weatherIcons;

    private IWeatherService _weather;
    private readonly List<RouteGate> _routeGates = new();
    private readonly StringBuilder _sb = new(128);

    private float _proximityTimer;
    private bool _isExpanded;

    private void Start()
    {
        _weather = GameServices.Weather ?? WeatherSystem.Instance;
        if (_weather != null)
        {
            _weather.OnWeatherChanged += HandleWeatherChanged;
            HandleWeatherChanged(_weather.CurrentWeather);
        }

        RefreshRouteGates();
        SetExpanded(false);
    }

    private void OnDestroy()
    {
        if (_weather != null)
            _weather.OnWeatherChanged -= HandleWeatherChanged;
    }

    private void Update()
    {
        _proximityTimer += Time.deltaTime;
        if (_proximityTimer < _proximityCheckInterval) return;
        _proximityTimer = 0f;

        bool shouldExpand = IsPlayerWithinRange();
        if (shouldExpand != _isExpanded)
            SetExpanded(shouldExpand);

        if (_isExpanded)
            UpdateExpandedRuntime();
        else
            UpdateCompactRuntime();
    }

    private bool IsPlayerWithinRange()
    {
        var players = GameObject.FindGameObjectsWithTag("Player");
        float sqrRadius = _expandRadius * _expandRadius;
        Vector3 origin = transform.position;
        float nearestSqr = float.MaxValue;
        Transform nearest = null;

        foreach (var p in players)
        {
            if (p == null) continue;
            float sqr = (p.transform.position - origin).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = p.transform;
            }
        }

        return nearest != null && nearestSqr <= sqrRadius;
    }

    private void SetExpanded(bool expanded)
    {
        _isExpanded = expanded;
        if (_compactRoot != null) _compactRoot.SetActive(!expanded);
        if (_expandedRoot != null) _expandedRoot.SetActive(expanded);

        if (expanded)
        {
            RefreshRouteGates();
            UpdateExpandedRuntime();
        }
    }

    private void HandleWeatherChanged(WeatherType weather)
    {
        UpdateWeatherIcon(weather);
        UpdateCompactRuntime();
        if (_isExpanded) UpdateExpandedRuntime();
    }

    private void UpdateWeatherIcon(WeatherType weather)
    {
        if (_weatherIcons == null) return;
        int idx = (int)weather;
        for (int i = 0; i < _weatherIcons.Length; i++)
        {
            if (_weatherIcons[i] != null)
                _weatherIcons[i].SetActive(i == idx);
        }
    }

    private void UpdateCompactRuntime()
    {
        if (_weather == null) return;
        var name = GetLocalizedWeatherName(_weather.CurrentWeather);

        if (_compactWeatherLabel != null)
            _compactWeatherLabel.text = name;

        if (_compactWindLabel != null)
            _compactWindLabel.text = $"風速 {_weather.WindSpeed:F1} m/s";
    }

    private void UpdateExpandedRuntime()
    {
        if (_weather == null) return;
        var current = _weather.CurrentWeather;
        var name = GetLocalizedWeatherName(current);

        if (_expandedWeatherLabel != null)
            _expandedWeatherLabel.text = $"天候: {name}";

        if (_expandedWindLabel != null)
        {
            var dir = _weather.CurrentWind;
            _expandedWindLabel.text = $"風速 {_weather.WindSpeed:F1} m/s   方向 ({dir.x:F1}, {dir.z:F1})";
        }

        if (_expandedRecommendationLabel != null)
            _expandedRecommendationLabel.text = BuildRecommendation(current);

        if (_expandedRouteStatusLabel != null)
            _expandedRouteStatusLabel.text = BuildRouteStatus();
    }

    private static string GetLocalizedWeatherName(WeatherType weather)
    {
        string key = weather switch
        {
            WeatherType.Sunny    => LocalizationKeys.WeatherClear,
            WeatherType.Cloudy   => LocalizationKeys.WeatherCloudy,
            WeatherType.Fog      => LocalizationKeys.WeatherFog,
            WeatherType.Rain     => LocalizationKeys.WeatherRain,
            WeatherType.Blizzard => LocalizationKeys.WeatherBlizzard,
            _                    => LocalizationKeys.WeatherClear
        };
        return LocalizedText.Get(key, LocalizationKeys.TableWeather);
    }

    private string BuildRecommendation(WeatherType weather)
    {
        _sb.Clear();
        _sb.AppendLine("推奨装備:");
        switch (weather)
        {
            case WeatherType.Blizzard:
                _sb.AppendLine($"・{LocalizedText.Get(LocalizationKeys.ItemOxygenTank, LocalizationKeys.TableItem)}");
                _sb.AppendLine($"・{LocalizedText.Get(LocalizationKeys.ItemThermalCase, LocalizationKeys.TableItem)}");
                _sb.AppendLine($"・{LocalizedText.Get(LocalizationKeys.ItemBivouacTent, LocalizationKeys.TableItem)}");
                _sb.AppendLine("警告: 凍傷ダメージ上昇中");
                break;
            case WeatherType.Rain:
                _sb.AppendLine($"・{LocalizedText.Get(LocalizationKeys.ItemAnchorBolt, LocalizationKeys.TableItem)}");
                _sb.AppendLine($"・{LocalizedText.Get(LocalizationKeys.ItemSecureBelt, LocalizationKeys.TableItem)}");
                _sb.AppendLine("注意: 岩面が滑りやすい");
                break;
            case WeatherType.Fog:
                _sb.AppendLine($"・{LocalizedText.Get(LocalizationKeys.ItemFlareGun, LocalizationKeys.TableItem)}");
                _sb.AppendLine($"・{LocalizedText.Get(LocalizationKeys.ItemEmergencyRadio, LocalizationKeys.TableItem)}");
                _sb.AppendLine("注意: 視界不良");
                break;
            case WeatherType.Cloudy:
                _sb.AppendLine($"・{LocalizedText.Get(LocalizationKeys.ItemShortRope, LocalizationKeys.TableItem)}");
                _sb.AppendLine($"・{LocalizedText.Get(LocalizationKeys.ItemIceAxe, LocalizationKeys.TableItem)}");
                break;
            case WeatherType.Sunny:
            default:
                _sb.AppendLine($"・{LocalizedText.Get(LocalizationKeys.ItemLongRope, LocalizationKeys.TableItem)}");
                _sb.AppendLine($"・{LocalizedText.Get(LocalizationKeys.ItemFood, LocalizationKeys.TableItem)}");
                _sb.AppendLine("条件良好 — 遠征チャンス");
                break;
        }
        return _sb.ToString();
    }

    private string BuildRouteStatus()
    {
        _sb.Clear();
        _sb.AppendLine("ルート状況:");
        if (_routeGates.Count == 0)
        {
            _sb.AppendLine("・登録されたルート情報はありません");
            return _sb.ToString();
        }

        foreach (var gate in _routeGates)
        {
            if (gate == null) continue;
            string status = gate.IsOpen ? "<color=#7FD46C>開通</color>" : "<color=#E05656>閉鎖</color>";
            _sb.Append("・").Append(gate.Name).Append(" — ").AppendLine(status);
        }
        return _sb.ToString();
    }

    private void RefreshRouteGates()
    {
        _routeGates.Clear();
        var gates = FindObjectsByType<RouteGate>(FindObjectsSortMode.None);
        if (gates != null) _routeGates.AddRange(gates);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
        Gizmos.DrawSphere(transform.position, _expandRadius);
    }
}
