using System;
using UnityEngine;

/// <summary>
/// 物音ブロードキャストバス。
/// プレイヤーのダッシュ・叫び・落石などが物音を発し、近くの敵モンスターが
/// EnemySensor 経由で反応する（R.E.P.O. の音駆動エンカウンターを再現）。
/// </summary>
public static class NoiseEvent
{
    /// <summary>(発生位置, 音の到達半径) を受け取る購読イベント。</summary>
    public static event Action<Vector3, float> OnNoise;

    /// <summary>物音を発する。半径が大きいほど遠くの敵に届く。</summary>
    public static void Emit(Vector3 position, float radius)
    {
        if (radius <= 0f) return;
        OnNoise?.Invoke(position, radius);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Reset() => OnNoise = null;
}
