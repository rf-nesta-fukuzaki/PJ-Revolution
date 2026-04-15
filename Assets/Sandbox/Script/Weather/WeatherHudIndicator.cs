using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 現在の天候・風速を HUD に表示するシンプルな UI コンポーネント。
/// </summary>
public class WeatherHudIndicator : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _weatherLabel;
    [SerializeField] private TextMeshProUGUI _windLabel;
    [SerializeField] private Image           _weatherIcon;

    [Header("天候アイコン（Sprite 配列: Sunny/Cloudy/Fog/Rain/Blizzard 順）")]
    [SerializeField] private Sprite[] _weatherIcons;

    private void Update()
    {
        if (WeatherSystem.Instance == null) return;

        var weather = WeatherSystem.Instance.CurrentWeather;

        if (_weatherLabel != null)
            _weatherLabel.text = weather switch
            {
                WeatherType.Sunny    => "晴れ",
                WeatherType.Cloudy   => "曇り",
                WeatherType.Fog      => "濃霧",
                WeatherType.Rain     => "雨",
                WeatherType.Blizzard => "吹雪 ⚠",
                _                    => "—"
            };

        if (_windLabel != null)
            _windLabel.text = $"風速 {WeatherSystem.Instance.WindSpeed:F1} m/s";

        if (_weatherIcon != null && _weatherIcons != null && _weatherIcons.Length >= 5)
            _weatherIcon.sprite = _weatherIcons[(int)weather];
    }
}
