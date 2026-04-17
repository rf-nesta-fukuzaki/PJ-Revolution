using System;
using UnityEngine;

/// <summary>
/// 天候情報へのアクセスを抽象化するサービスインターフェース。
///
/// 依存側は WeatherSystem の具体型ではなくこのインターフェースに依存することで
/// テスト時のモック置換やシーン非依存の初期化が可能になる。
///
/// 実装: WeatherSystem
/// </summary>
public interface IWeatherService
{
    /// <summary>現在の天候が吹雪かどうか。</summary>
    bool IsBlizzard { get; }

    /// <summary>現在の天候種別。</summary>
    WeatherType CurrentWeather { get; }

    /// <summary>現在の風ベクトル（方向 × 速度）。</summary>
    Vector3 CurrentWind { get; }

    /// <summary>現在の風速（m/s）。</summary>
    float WindSpeed { get; }

    /// <summary>天候が変化したときのイベント。</summary>
    event Action<WeatherType> OnWeatherChanged;

    /// <summary>現在の天候による地面の滑り具合（0=滑らない、1=最大滑り）。</summary>
    float GetSliperiness();

    /// <summary>シェルター内のオブジェクトを登録する（吹雪ダメージ免除）。</summary>
    void AddShelterOccupant(GameObject go);

    /// <summary>シェルター内のオブジェクト登録を解除する。</summary>
    void RemoveShelterOccupant(GameObject go);
}
