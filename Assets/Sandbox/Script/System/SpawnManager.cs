using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// GDD §7.1 — セミランダム5層スポーンマネージャー。
/// 固定地形の上にランダム要素を重ねる。完全プロシージャルのリスクなしにリプレイ性を確保。
///
/// L1: 固定地形     → このマネージャーは担当しない（山のメッシュ等は固定）
/// L2: ルート開閉   → RouteGate コンポーネント
/// L3: 遺物配置     → SpawnPoint(Relic)
/// L4: 天候＆環境   → WeatherSystem が担当
/// L5: ハザード配置 → SpawnPoint(Hazard)
/// </summary>
public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Header("遺物スポーン")]
    [SerializeField] private int _minRelics = 3;
    [SerializeField] private int _maxRelics = 5;

    [Header("遺物プール（全8種プレハブ — 空の場合は SpawnPoint 個別設定を使用）")]
    [Tooltip("Inspector に全8遺物のプレハブを登録しておくと、各 SpawnPoint に自動注入される")]
    [SerializeField] private GameObject[] _relicPrefabPool;

    [Header("ハザード設定")]
    [SerializeField, Range(0f, 1f)] private float _hazardDensity = 0.4f;

    [Header("ルート開閉")]
    [SerializeField, Range(0f, 1f)] private float _routeOpenChance = 0.5f;

    private SpawnPoint[] _allSpawnPoints;
    private RouteGate[]  _allRouteGates;
    private int          _openRouteCount;   // L2 実行後に更新されるオープンルート数

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _allSpawnPoints = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        _allRouteGates  = FindObjectsByType<RouteGate>(FindObjectsSortMode.None);

        RunAllLayers();
    }

    // ── 全レイヤー実行 ───────────────────────────────────────
    public void RunAllLayers()
    {
        ClearAll();
        InjectRelicPool();
        GenerateLayer2_Routes();
        GenerateLayer3_Relics();
        GenerateLayer5_Hazards();
        GenerateLayer5_DroppedItems();
        PairTwinStatues();
    }

    // ── 遺物プール注入 ────────────────────────────────────────
    /// <summary>
    /// _relicPrefabPool（全8種）が設定されている場合、
    /// 全ての Relic 層 SpawnPoint にプールを注入する。
    /// SpawnPoint.TryActivate/Activate はプールからランダムに選択して生成する。
    /// </summary>
    private void InjectRelicPool()
    {
        if (_relicPrefabPool == null || _relicPrefabPool.Length == 0) return;

        int injected = 0;
        foreach (var sp in _allSpawnPoints)
        {
            if (sp.Layer != SpawnLayer.Relic) continue;
            sp.SetPrefabPool(_relicPrefabPool);
            injected++;
        }

        Debug.Log($"[Spawn] 遺物プール（{_relicPrefabPool.Length}種）を {injected} 個の SpawnPoint に注入");
    }

    // ── TwinStatue 自動ペアリング ─────────────────────────────
    /// <summary>
    /// L3 スポーン後に呼び出し、シーン内の全 TwinStatueRelic を2体ずつペアリングする。
    /// Inspector の _partner 参照が null のまま生成された場合にも対応。
    /// </summary>
    private void PairTwinStatues()
    {
        var twins = Object.FindObjectsByType<TwinStatueRelic>(FindObjectsSortMode.None);
        if (twins.Length < 2) return;

        int pairs = 0;
        for (int i = 0; i + 1 < twins.Length; i += 2)
        {
            twins[i].SetPartner(twins[i + 1]);
            twins[i + 1].SetPartner(twins[i]);
            pairs++;
            Debug.Log($"[Spawn L3] 双子像ペアリング: {twins[i].name} ↔ {twins[i + 1].name}");
        }

        if (twins.Length % 2 != 0)
            Debug.LogWarning($"[Spawn L3] 双子像が奇数個（{twins.Length}体）→ 最後の1体はパートナーなし");

        Debug.Log($"[Spawn L3] 双子像 {pairs} ペアをペアリング完了");
    }

    private void ClearAll()
    {
        foreach (var sp in _allSpawnPoints)
            sp.Deactivate();
    }

    // ── L2: ルート開閉 ───────────────────────────────────────
    private void GenerateLayer2_Routes()
    {
        if (_allRouteGates == null) return;

        _openRouteCount = 0;

        foreach (var gate in _allRouteGates)
        {
            bool isOpen = Random.value < _routeOpenChance;
            gate.SetOpen(isOpen);

            if (isOpen)
                _openRouteCount++;
            else
                Debug.Log($"[Spawn L2] ルート閉鎖: {gate.name}");
        }
    }

    // ── 外部クエリ ────────────────────────────────────────────
    /// <summary>
    /// 現在のルート開閉状況の要約文字列を返す。
    /// BasecampShop._routeStatusLabel などで使用する。
    /// </summary>
    public string GetRouteStatusSummary()
    {
        int total = _allRouteGates?.Length ?? 0;
        if (total == 0) return "ルート状況: 情報なし";

        int closed = total - _openRouteCount;
        return $"ルート開通: {_openRouteCount}/{total}  閉鎖: {closed}";
    }

    // ── L3: 遺物配置 ─────────────────────────────────────────
    private void GenerateLayer3_Relics()
    {
        var relicPoints = _allSpawnPoints
            .Where(sp => sp.Layer == SpawnLayer.Relic)
            .OrderBy(_ => Random.value)
            .ToList();

        int count = Random.Range(_minRelics, _maxRelics + 1);

        // ゾーンごとに分散（低地帯に低価値、高地帯に高価値）
        var byZone = relicPoints.GroupBy(sp => sp.ZoneId);
        int spawned = 0;

        foreach (var zone in byZone.OrderBy(g => g.Key))
        {
            if (spawned >= count) break;

            var point = zone.First();
            if (point.TryActivate())
                spawned++;
        }

        Debug.Log($"[Spawn L3] 遺物 {spawned} 個を配置");
    }

    // ── L5: ハザード ─────────────────────────────────────────
    private void GenerateLayer5_Hazards()
    {
        var hazardPoints = _allSpawnPoints
            .Where(sp => sp.Layer == SpawnLayer.Hazard)
            .ToList();

        int spawned = 0;
        foreach (var sp in hazardPoints)
        {
            if (Random.value < _hazardDensity)
            {
                sp.Activate();
                spawned++;
            }
        }

        Debug.Log($"[Spawn L5] ハザード {spawned} 個を配置");
    }

    // ── L5: 山中ドロップアイテム ─────────────────────────────
    private void GenerateLayer5_DroppedItems()
    {
        var itemPoints = _allSpawnPoints
            .Where(sp => sp.Layer == SpawnLayer.Item)
            .ToList();

        int spawned = 0;
        foreach (var sp in itemPoints)
        {
            // 50% の確率で遺留品が落ちている（耐久値が低い）
            if (Random.value < 0.5f)
            {
                sp.Activate();
                spawned++;
            }
        }

        Debug.Log($"[Spawn L5] 遺留品 {spawned} 個を配置");
    }

    // ── 再生成 ────────────────────────────────────────────────
    public void RegenerateAll() => RunAllLayers();
}
