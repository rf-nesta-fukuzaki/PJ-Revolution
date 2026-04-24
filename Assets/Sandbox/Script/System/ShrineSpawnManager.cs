using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GDD §7.4 — 復活の祠のスポーンマネージャー。
///
/// ゾーンごとに固定候補地点を保持し、遠征開始時にランダムサンプリングで
/// 表で定められた数だけ <see cref="ReviveShrine"/> を配置する。
///
///   ゾーン1（森林帯）    — 2個 / 4候補
///   ゾーン2（岩場帯）    — 2個 / 4候補
///   ゾーン3（急壁）      — 1個 / 3候補
///   ゾーン4（神殿遺跡）  — 1個 / 3候補
///   ゾーン5（氷壁）      — 1個 / 2候補
///   ゾーン6（山頂遺跡）  — 0個
///
/// 合計 7 個 / 1 遠征。
///
/// シーンセットアップ:
///   1. 空 GameObject「Managers」配下に本コンポーネントをアタッチ
///   2. _shrinePrefab に ReviveShrine 付きプレハブを設定
///   3. 各 ZoneCandidates に固定候補 Transform を詰める（4/4/3/3/2/0）
///   4. Awake で GDD §7.4 の表通りの個数を自動サンプリングして生成
/// </summary>
[DisallowMultipleComponent]
public class ShrineSpawnManager : MonoBehaviour
{
    public static ShrineSpawnManager Instance { get; private set; }

    // GDD §7.4 表: 各ゾーンの必要個数
    public static readonly int[] SHRINE_COUNT_PER_ZONE = { 2, 2, 1, 1, 1, 0 };

    [System.Serializable]
    public class ZoneCandidates
    {
        [Tooltip("ゾーン表示名 (Inspector デバッグ用)")]
        public string zoneName = "Zone";
        [Tooltip("候補 Transform 配列。null は無視される")]
        public Transform[] candidates;
    }

    [Header("祠プレハブ (ReviveShrine 付き)")]
    [SerializeField] private ReviveShrine _shrinePrefab;

    [Header("ゾーン候補地点（インデックス 0=Zone1 ... 5=Zone6）")]
    [SerializeField] private ZoneCandidates[] _zoneCandidates = new ZoneCandidates[6];

    [Header("ランダム制御")]
    [Tooltip("0 以外で指定すると乱数シード固定（テスト・再現性用）")]
    [SerializeField] private int _deterministicSeed;
    [SerializeField] private bool _spawnOnAwake = true;

    private readonly List<ReviveShrine> _spawned = new();
    public IReadOnlyList<ReviveShrine> Spawned => _spawned;

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (_spawnOnAwake)
            SpawnAll();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── 公開 API ─────────────────────────────────────────────
    /// <summary>GDD §7.4 の表通りに祠を配置する。既存生成分はクリアする。</summary>
    public void SpawnAll()
    {
        Debug.Assert(_shrinePrefab != null, "[Contract] ShrineSpawnManager: 祠プレハブ未設定");
        if (_shrinePrefab == null) return;

        ClearSpawned();

        var rng = (_deterministicSeed != 0)
            ? new System.Random(_deterministicSeed)
            : new System.Random();

        int zoneLimit = Mathf.Min(SHRINE_COUNT_PER_ZONE.Length,
                                  _zoneCandidates != null ? _zoneCandidates.Length : 0);

        for (int zoneIdx = 0; zoneIdx < zoneLimit; zoneIdx++)
        {
            int need = SHRINE_COUNT_PER_ZONE[zoneIdx];
            if (need <= 0) continue;

            var zone = _zoneCandidates[zoneIdx];
            if (zone == null || zone.candidates == null || zone.candidates.Length == 0) continue;

            var picks = SampleWithoutReplacement(zone.candidates, need, rng);
            foreach (var t in picks)
            {
                if (t == null) continue;
                var shrine = Instantiate(_shrinePrefab, t.position, t.rotation, t);
                shrine.name = $"Shrine_Zone{zoneIdx + 1}_{_spawned.Count}";
                _spawned.Add(shrine);
            }
        }

        Debug.Log($"[ShrineSpawn] 生成完了: {_spawned.Count} 個");
    }

    public void ClearSpawned()
    {
        foreach (var s in _spawned)
            if (s != null) Destroy(s.gameObject);
        _spawned.Clear();
    }

    // ── 非重複サンプリング ───────────────────────────────────
    private static List<T> SampleWithoutReplacement<T>(T[] source, int count, System.Random rng)
        where T : class
    {
        var pool   = new List<T>(source.Length);
        foreach (var s in source) if (s != null) pool.Add(s);

        var picked = new List<T>(Mathf.Min(count, pool.Count));
        int take   = Mathf.Min(count, pool.Count);
        for (int i = 0; i < take; i++)
        {
            int idx = rng.Next(pool.Count);
            picked.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        return picked;
    }
}
