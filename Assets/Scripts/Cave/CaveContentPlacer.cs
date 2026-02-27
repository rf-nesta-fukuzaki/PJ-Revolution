using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 洞窟生成完了後にクリスタル・食料・酸素キノコを自動配置するコンポーネント。
/// CaveGenerator と同じ GameObject にアタッチし、GenerateCave() 末尾から呼び出す。
///
/// [ランダム再現性]
///   配置に使うランダムは System.Random(seed) で行う（UnityEngine.Random 使用禁止）。
///   → NGO 対応時に同じシードを渡すことで全クライアントが同一配置になる。
///
/// [床面検出の方針]
///   Raycast ではなく CaveChunk のスカラー場を直接参照する。
///   これにより「Raycastが天井の岩に当たって洞窟床面に届かない」問題を回避する。
///   床面 = Y方向で「空洞（scalar < isoLevel）の直下が岩（scalar >= isoLevel）」のセル。
/// </summary>
public class CaveContentPlacer : MonoBehaviour
{
    // ─── 配置密度 ──────────────────────────────────────────────────────────

    [Header("配置密度（空洞セルに対する出現確率 %）")]
    [Range(0, 100)]
    [SerializeField] private int crystalDensity = 15;

    [Range(0, 100)]
    [SerializeField] private int foodDensity = 8;

    [Range(0, 100)]
    [SerializeField] private int mushroomDensity = 6;

    // ─── Prefab 参照 ───────────────────────────────────────────────────────

    [Header("Prefab 参照")]
    [Tooltip("クリスタルの Prefab（複数設定するとランダム選択）")]
    [SerializeField] private GameObject[] crystalPrefabs;

    [Tooltip("食料アイテムの Prefab（PlacedResourceItem を使用）")]
    [SerializeField] private GameObject foodPrefab;

    [Tooltip("酸素キノコの Prefab（PlacedResourceItem を使用）")]
    [SerializeField] private GameObject mushroomPrefab;

    // ─── 配置制限 ──────────────────────────────────────────────────────────

    [Header("配置制限")]
    [Tooltip("スタート地点付近のアイテム除外半径（m）")]
    [SerializeField] private float startExcludeRadius = 12f;

    [Tooltip("同種アイテム同士の最小間隔（m）")]
    [SerializeField] private float minSpacing = 3f;

    [Tooltip("クリスタルの最大数")]
    [SerializeField] private int maxCrystals = 80;

    [Tooltip("食料アイテムの最大数")]
    [SerializeField] private int maxFood = 30;

    [Tooltip("酸素キノコの最大数")]
    [SerializeField] private int maxMushrooms = 20;

    // ─── 内部状態 ──────────────────────────────────────────────────────────

    private CaveGenerator _generator;

    // 配置済み座標リスト（間隔チェック用）
    private readonly List<Vector3> _placedCrystals  = new List<Vector3>();
    private readonly List<Vector3> _placedFood      = new List<Vector3>();
    private readonly List<Vector3> _placedMushrooms = new List<Vector3>();

    // 再生成時に前の配置オブジェクトを消すためのリスト
    private readonly List<GameObject> _spawnedObjects = new List<GameObject>();

    // ─── 初期化 ────────────────────────────────────────────────────────────

    private void Awake()
    {
        _generator = GetComponent<CaveGenerator>();
    }

    // ─── 公開 API ──────────────────────────────────────────────────────────

    /// <summary>
    /// CaveGenerator.Generate() 末尾から呼び出す。
    /// 既配置オブジェクトをすべて破棄してから再配置する。
    /// </summary>
    public void PlaceContent(int seed)
    {
        if (_generator == null)
        {
            Debug.LogError("[CaveContentPlacer] CaveGenerator コンポーネントが見つかりません");
            return;
        }

        ClearPlaced();

        var     rng      = new System.Random(seed);
        Vector3 startPos = _generator.StartWorldPosition;

        PlaceContent3D_ScalarBased(rng, startPos);

        Debug.Log($"[CaveContentPlacer] 配置完了 seed={seed} " +
                  $"クリスタル:{_placedCrystals.Count} " +
                  $"食料:{_placedFood.Count} " +
                  $"キノコ:{_placedMushrooms.Count}");
    }

    // ─── 3D モード配置（Marching Cubes, スカラー場直接参照）──────────────────
    //
    // Raycast ではなく CaveChunk.GetScalar() を使って床面を検出する。
    // 上から下にスキャンして「空洞（scalar < isoLevel）の直下が岩（scalar >= isoLevel）」
    // となる最初の Y 位置を床面と判定する。
    //
    // 【Raycast をやめた理由】
    //   上から下への Raycast は、洞窟の天井面（岩の上面）で最初にヒットして止まる。
    //   実際の洞窟床面は天井の岩の内側にあるため Raycast では到達できず、
    //   結果として岩の上（洞窟の外）にしかアイテムが配置されない。
    //
    // 【左端偏り の修正方針】
    //   foreach でチャンクを順番に処理すると cx=0 側（左端）で maxCrystals 等の上限に
    //   達してしまい、残りのチャンクに何も配置されない。
    //   → 全床面候補を先に収集し Fisher-Yates シャッフル後に配置することで解消する。

    private void PlaceContent3D_ScalarBased(System.Random rng, Vector3 startPos)
    {
        var chunks = _generator.Chunks;
        if (chunks == null || chunks.Count == 0)
        {
            Debug.LogWarning("[CaveContentPlacer] チャンクが見つかりません（3D モードか確認してください）");
            return;
        }

        float isoLevel = _generator.NoiseConfig.isoLevel;
        float cellSize = _generator.CellSize3D;
        int   cs       = CaveChunk.ChunkSize;

        // [Fix] 全チャンクの床面候補を先に収集する
        //       chunkOrigin は chunk.transform.position（ワールド座標）を使用することで
        //       CaveGenerator が原点以外に配置されていてもワールド座標が正しく得られる
        var floorCandidates = new List<Vector3>();

        foreach (var chunk in chunks)
        {
            if (chunk == null) continue;

            // [Fix] ワールド座標基点として chunk.transform.position を使用
            Vector3 chunkOrigin = chunk.transform.position;

            for (int lx = 0; lx < cs; lx++)
            for (int lz = 0; lz < cs; lz++)
            {
                // 上から下にスキャンして最初の「床面」を探す
                // 床面 = この Y が空洞 かつ 1つ下が岩
                for (int ly = cs - 1; ly >= 1; ly--)
                {
                    float here  = chunk.GetScalar(lx, ly,     lz);
                    float below = chunk.GetScalar(lx, ly - 1, lz);

                    bool isAir   = here  < isoLevel;
                    bool isSolid = below >= isoLevel;

                    if (!isAir || !isSolid) continue;

                    // [Fix] 床面ワールド座標（chunkOrigin がワールド基点のため変換不要）
                    Vector3 floorPos = chunkOrigin + new Vector3(
                        (lx + 0.5f) * cellSize,
                        ly           * cellSize,
                        (lz + 0.5f) * cellSize);

                    if (Vector3.Distance(floorPos, startPos) < startExcludeRadius)
                        break; // このXZセルでは全Y除外

                    floorCandidates.Add(floorPos);
                    break; // 1 XZカラムにつき1床面（一番上の床）
                }
            }
        }

        // [Fix] Fisher-Yates シャッフル：チャンク順依存の左端偏りを解消する
        //       RNG に同じシードを使うため NGO 環境でも全クライアントが同一配置になる
        for (int i = floorCandidates.Count - 1; i > 0; i--)
        {
            int     j   = rng.Next(i + 1);
            Vector3 tmp = floorCandidates[i];
            floorCandidates[i] = floorCandidates[j];
            floorCandidates[j] = tmp;
        }

        // [Fix] シャッフル後に順番にアイテム配置
        foreach (var floorPos in floorCandidates)
        {
            TrySpawnItems(floorPos, rng);
        }
    }

    // ─── 配置コア ────────────────────────────────────────────────────────

    private void TrySpawnItems(Vector3 floorPos, System.Random rng)
    {
        // クリスタル（優先）
        if (_placedCrystals.Count < maxCrystals
            && crystalPrefabs != null && crystalPrefabs.Length > 0
            && rng.NextDouble() < crystalDensity / 100.0
            && IsSpacingOk(floorPos, _placedCrystals))
        {
            int idx = rng.Next(crystalPrefabs.Length);
            SpawnItem(crystalPrefabs[idx], floorPos, rng);
            _placedCrystals.Add(floorPos);
            return; // 1セル1アイテム
        }

        // 食料
        if (_placedFood.Count < maxFood
            && foodPrefab != null
            && rng.NextDouble() < foodDensity / 100.0
            && IsSpacingOk(floorPos, _placedFood))
        {
            SpawnItem(foodPrefab, floorPos, rng);
            _placedFood.Add(floorPos);
            return;
        }

        // 酸素キノコ
        if (_placedMushrooms.Count < maxMushrooms
            && mushroomPrefab != null
            && rng.NextDouble() < mushroomDensity / 100.0
            && IsSpacingOk(floorPos, _placedMushrooms))
        {
            SpawnItem(mushroomPrefab, floorPos, rng);
            _placedMushrooms.Add(floorPos);
        }
    }

    private bool IsSpacingOk(Vector3 pos, List<Vector3> placed)
    {
        float sqrMin = minSpacing * minSpacing;
        foreach (var p in placed)
            if ((pos - p).sqrMagnitude < sqrMin)
                return false;
        return true;
    }

    private void SpawnItem(GameObject prefab, Vector3 floorPos, System.Random rng)
    {
        float      yRot     = (float)(rng.NextDouble() * 360.0);
        Quaternion rot      = Quaternion.Euler(0f, yRot, 0f);
        Vector3    spawnPos = floorPos + Vector3.up * 0.05f;

        GameObject obj = Instantiate(prefab, spawnPos, rot);
        _spawnedObjects.Add(obj);
    }

    // ─── クリア ────────────────────────────────────────────────────────────

    private void ClearPlaced()
    {
        foreach (var obj in _spawnedObjects)
        {
            if (obj == null) continue;
#if UNITY_EDITOR
            DestroyImmediate(obj);
#else
            Destroy(obj);
#endif
        }
        _spawnedObjects.Clear();
        _placedCrystals.Clear();
        _placedFood.Clear();
        _placedMushrooms.Clear();
    }
}
