using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全 NPC で共有するワールドセンサーのスロットル付きキャッシュ。
///
/// 以前は各 NPCController が毎秒 RefreshSensors で約 11 回の FindObjectsByType
/// （RelicCarrier / RelicBase / ShelterZone + 7 種ハザード + ReturnZone）を実行しており、
/// NPC が N 体いると毎秒 11×N 回の全シーン走査＋配列確保が発生し、周期的なフレームスパイク
/// （ガクつき）と GC 圧の主因になっていた。
///
/// このキャッシュは走査を「グローバルに 1 回」へ集約する:
///   - ハザード / シェルター / ReturnZone は基本静的なので長間隔(StaticRescanInterval)で再走査。
///   - 遺物(RelicCarrier/RelicBase)は運搬で変化するため短間隔(動的)で再走査。
/// NPC 側は走査せず、このキャッシュを読むだけにする（破棄済み参照は読み取り側で null チェック）。
/// </summary>
public static class NpcSensorCache
{
    public static readonly List<RelicCarrier> Carriers = new();
    public static readonly List<RelicBase> Bases = new();
    public static readonly List<ShelterZone> Shelters = new();
    public static readonly List<Transform> Hazards = new();
    public static Transform ReturnZone { get; private set; }

    // 静的オブジェクト（ハザード等）の安全側再走査間隔。実行時生成された
    // ハザードやシーン再構築を取りこぼさないための保険。
    private const float StaticRescanInterval = 8f;

    private static float _nextDynamicScan = float.MinValue;
    private static float _nextStaticScan = float.MinValue;

    // ドメインリロード無効環境でも各プレイ開始時に確実に初期化する。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        Carriers.Clear();
        Bases.Clear();
        Shelters.Clear();
        Hazards.Clear();
        ReturnZone = null;
        _nextDynamicScan = float.MinValue;
        _nextStaticScan = float.MinValue;
    }

    /// <summary>
    /// 必要なら走査を更新する。複数 NPC が同フレームで呼んでも、間隔内なら
    /// 実走査は最初の 1 回だけ走り、以降は既存キャッシュを再利用する。
    /// </summary>
    public static void EnsureFresh(float dynamicInterval)
    {
        float now = Time.time;

        if (now >= _nextStaticScan)
        {
            _nextStaticScan = now + StaticRescanInterval;
            ScanStatic();
        }

        if (now >= _nextDynamicScan)
        {
            _nextDynamicScan = now + Mathf.Max(0.1f, dynamicInterval);
            ScanDynamic();
        }
    }

    private static void ScanStatic()
    {
        Shelters.Clear();
        Shelters.AddRange(Object.FindObjectsByType<ShelterZone>(FindObjectsSortMode.None));

        Hazards.Clear();
        AddHazards(Object.FindObjectsByType<IcePatch>(FindObjectsSortMode.None));
        AddHazards(Object.FindObjectsByType<CollapsiblePlatform>(FindObjectsSortMode.None));
        AddHazards(Object.FindObjectsByType<RockfallTrigger>(FindObjectsSortMode.None));
        AddHazards(Object.FindObjectsByType<FakeFloor>(FindObjectsSortMode.None));
        AddHazards(Object.FindObjectsByType<PressurePlateArrow>(FindObjectsSortMode.None));
        AddHazards(Object.FindObjectsByType<PendulumLog>(FindObjectsSortMode.None));
        AddHazards(Object.FindObjectsByType<FallingCeiling>(FindObjectsSortMode.None));

        var returnZone = Object.FindFirstObjectByType<ReturnZone>();
        ReturnZone = returnZone != null ? returnZone.transform : null;
    }

    private static void ScanDynamic()
    {
        Carriers.Clear();
        Carriers.AddRange(Object.FindObjectsByType<RelicCarrier>(FindObjectsSortMode.None));

        Bases.Clear();
        Bases.AddRange(Object.FindObjectsByType<RelicBase>(FindObjectsSortMode.None));
    }

    private static void AddHazards<T>(T[] hazards) where T : Component
    {
        foreach (T hazard in hazards)
        {
            if (hazard == null)
                continue;
            Hazards.Add(hazard.transform);
        }
    }
}
