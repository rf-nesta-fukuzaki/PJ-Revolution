using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// GDD §2.2 — Peak Plunder 山岳地形ジェネレーター。
///
/// Perlin ノイズ × ゾーン別高度プロファイルで Unity Terrain を生成し、
/// 既存オブジェクト（GrappableRocks・IcePatches 等）を地形面へスナップする。
///
/// 使い方:
///   Inspector の [Generate Mountain Terrain] ContextMenu を実行する。
///   または MCP RunCommand から Generate() を呼び出す。
/// </summary>
public class MountainTerrainGenerator : MonoBehaviour
{
    // ── Terrain サイズ ─────────────────────────────────────────
    [Header("Terrain サイズ")]
    [SerializeField] private float _terrainWidth  = 300f;
    [SerializeField] private float _terrainLength = 300f;
    [SerializeField] private float _terrainHeight = 520f;
    [SerializeField] private int   _resolution    = 513;    // 2^n+1 必須

    // ── fBm 山岳ノイズ ─────────────────────────────────────────
    [Header("fBm 山岳ノイズ")]
    [SerializeField] private int   _seed   = 42;
    [SerializeField] private float _scale1 = 0.0065f; // 1層目: 大きな山体
    [SerializeField] private float _amp1   = 0.62f;
    [SerializeField] private float _scale2 = 0.020f;  // 2層目: 尾根・谷
    [SerializeField] private float _amp2   = 0.34f;
    [SerializeField] private float _scale3 = 0.075f;  // 3層目: 岩肌
    [SerializeField] private float _amp3   = 0.12f;

    [Header("登山ルート整形")]
    [SerializeField] private float _routeNoiseScale = 0.018f;
    [SerializeField] private float _routeMeanderMeters = 14f;
    [SerializeField] private float _routeBaseWidth = 24f;
    [SerializeField] private float _routeSummitWidth = 5.5f;
    [SerializeField, Range(0f, 1f)] private float _routeFlattenStrength = 0.29f;

    [Header("山肌ディテール")]
    [SerializeField, Range(1, 8)] private int _fractalOctaves = 6;
    [SerializeField, Range(0.1f, 0.9f)] private float _fractalPersistence = 0.47f;
    [SerializeField] private float _fractalLacunarity = 2.12f;
    [SerializeField] private float _domainWarpScale = 0.010f;
    [SerializeField] private float _domainWarpMeters = 18f;

    // ── マテリアル ─────────────────────────────────────────────
    [Header("Terrain Material (任意 / 未設定=デフォルト)")]
    [SerializeField] private Material _terrainMaterial;

    // ── ゾーン別高度プロファイル ────────────────────────────────
    // nz 0.0 = ベースキャンプ側、nz 1.0 = 山頂側（0〜1 の正規化 Z 座標）
    // height 0.0〜1.0 は _terrainHeight に掛ける係数
    private static readonly float[] s_zoneNz =
        { 0.04f, 0.18f, 0.35f, 0.52f, 0.65f, 0.78f, 0.93f };
    private static readonly float[] s_zoneH =
        { 0.00f, 0.055f, 0.26f, 0.64f, 0.76f, 0.94f, 1.00f };

    // ── 内部キャッシュ ─────────────────────────────────────────
    private Terrain _terrain;
    private readonly Dictionary<string, GameObject> _sceneObjectsByName = new();

    // ──────────────────────────────────────────────────────────
    //  Unity ライフサイクル
    // ──────────────────────────────────────────────────────────

    private void Awake()
    {
        _terrain = GetComponentInChildren<Terrain>();
    }

    // ──────────────────────────────────────────────────────────
    //  公開 API
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// 地形を生成してシーンに追加する。
    /// Inspector の ContextMenu または MCP RunCommand から呼ぶ。
    /// </summary>
    [ContextMenu("Generate Mountain Terrain")]
    public void Generate()
    {
        DestroyExistingTerrain();

        var data = BuildTerrainData();
        var go   = CreateTerrainGameObject(data);
        _terrain = go.GetComponent<Terrain>();

        RebuildSceneObjectCache();
        PositionZoneMarkers();
        SnapAllToTerrain();
        HideZoneCubeMeshes();

#if UNITY_EDITOR
        PersistTerrainData(data);
#endif
        Debug.Log("[MountainTerrain] 生成完了。Terrain サイズ: "
            + $"{_terrainWidth}×{_terrainLength}m, 最高高度: {_terrainHeight}m");
    }

    /// <summary>既存オブジェクトを地形面にスナップする（地形変更後の手動補正用）。</summary>
    [ContextMenu("Snap Objects to Terrain")]
    public void SnapObjectsToTerrain()
    {
        if (_terrain == null)
            _terrain = GetComponentInChildren<Terrain>();
        if (_terrain == null)
        {
            Debug.LogWarning("[MountainTerrain] Terrain が見つかりません。先に Generate を実行してください。");
            return;
        }
        SnapAllToTerrain();
    }

    // ──────────────────────────────────────────────────────────
    //  Terrain 生成
    // ──────────────────────────────────────────────────────────

    private void DestroyExistingTerrain()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.GetComponent<Terrain>() == null) continue;
#if UNITY_EDITOR
            DestroyImmediate(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }
    }

    private TerrainData BuildTerrainData()
    {
        var data = new TerrainData
        {
            heightmapResolution = _resolution,
            size                = new Vector3(_terrainWidth, _terrainHeight, _terrainLength)
        };
        data.SetHeights(0, 0, ComputeHeightmap());
        return data;
    }

    private GameObject CreateTerrainGameObject(TerrainData data)
    {
        var go = Terrain.CreateTerrainGameObject(data);
        go.name = "MountainTerrain";
        go.transform.SetParent(transform);
        // Terrain 原点は左下手前 → ワールド中心に合わせてセンタリング
        go.transform.localPosition = new Vector3(
            -_terrainWidth  * 0.5f,
            0f,
            -_terrainLength * 0.5f);
        go.isStatic = true;

        var t = go.GetComponent<Terrain>();
        t.drawInstanced     = true;
        t.heightmapPixelError = 5f;
        t.basemapDistance    = 1000f;
        if (_terrainMaterial != null)
            t.materialTemplate = _terrainMaterial;

        var col = go.GetComponent<TerrainCollider>();
        if (col != null) col.terrainData = data;

        return go;
    }

    // ──────────────────────────────────────────────────────────
    //  高度マップ生成
    // ──────────────────────────────────────────────────────────

    private float[,] ComputeHeightmap()
    {
        int res     = _resolution;
        var heights = new float[res, res];

        for (int zi = 0; zi < res; zi++)
        {
            float nz    = (float)zi / (res - 1);
            float baseH = ZoneProfile(nz);
            float worldZ = Mathf.Lerp(-_terrainLength * 0.5f, _terrainLength * 0.5f, nz);
            float routeCenterX = ComputeRouteCenterX(worldZ, nz);
            float routeWidth = Mathf.Lerp(_routeBaseWidth, _routeSummitWidth, baseH);

            for (int xi = 0; xi < res; xi++)
            {
                float nx = (float)xi / (res - 1);
                float worldX = Mathf.Lerp(-_terrainWidth * 0.5f, _terrainWidth * 0.5f, nx);

                Vector2 warped = DomainWarp(worldX, worldZ);

                // ① 1層目: 大きく緩やかなfBm。山全体の塊と主峰のうねりを作る。
                float macro = SignedFbm(warped.x, warped.y, _scale1, _fractalOctaves, _fractalPersistence, _fractalLacunarity, 11);

                // ② 2層目: Ridged fBm。尾根・谷・切り立った岩壁の骨格を作る。
                float ridgeNoise = RidgedFbm(warped.x, warped.y, _scale2, 5, 0.54f, 2.08f, 23);

                // ③ 3層目: 高周波Ridged fBm。近距離で見える岩肌のデコボコを作る。
                float rockNoise = RidgedFbm(warped.x, warped.y, _scale3, 5, 0.50f, 2.18f, 37);

                float ridgeDistance = Mathf.Abs(worldX - routeCenterX * 0.35f);
                float mainHalfWidth = Mathf.Lerp(_terrainWidth * 0.58f, _terrainWidth * 0.10f, Mathf.Pow(baseH, 0.85f));
                float mountainEnvelope = 1f - Smooth01(ridgeDistance / Mathf.Max(mainHalfWidth, 0.001f));
                mountainEnvelope = Mathf.Pow(Mathf.Clamp01(mountainEnvelope), Mathf.Lerp(1.15f, 3.25f, baseH));

                float sideMask = Smooth01(ridgeDistance / Mathf.Max(mainHalfWidth * 0.72f, 0.001f)) * mountainEnvelope;
                float cliffBand = Bell(nz, 0.35f, 0.050f) + Bell(nz, 0.53f, 0.045f) + Bell(nz, 0.76f, 0.055f);
                float summitSpike = Mathf.Pow(Mathf.Clamp01(nz), 2.65f) * Mathf.Pow(mountainEnvelope, 1.55f) * 0.16f;

                float h = baseH * Mathf.Lerp(0.025f, 1.08f, mountainEnvelope);
                h += macro * _amp1 * 0.18f * Mathf.Lerp(0.35f, 1f, mountainEnvelope);
                h += ridgeNoise * _amp2 * 0.42f * baseH * mountainEnvelope;
                h += rockNoise * _amp3 * 0.90f * baseH * Mathf.SmoothStep(0.16f, 0.9f, baseH);
                h += sideMask * ridgeNoise * 0.16f * baseH;
                h += cliffBand * (0.12f + rockNoise * 0.08f) * Mathf.SmoothStep(0.25f, 0.98f, mountainEnvelope);
                h += summitSpike;

                // 侵食谷。登山路から離れた斜面を削り、山体を「丘」ではなく険しい峰に見せる。
                float valley = RidgedFbm(warped.x + 91.7f, warped.y - 43.2f, 0.014f, 4, 0.56f, 2.05f, 71);
                float routeDistance = Mathf.Abs(worldX - routeCenterX);
                float routeMask = 1f - Smooth01((routeDistance - routeWidth * 0.36f) / Mathf.Max(routeWidth * 0.64f, 0.001f));
                h -= valley * 0.10f * baseH * (1f - routeMask) * Mathf.SmoothStep(0.12f, 0.9f, mountainEnvelope);

                // 中央登山路は「通れるが怖い」程度にだけ均す。
                float routeSurface = baseH + cliffBand * 0.055f + macro * 0.028f + ridgeNoise * 0.025f + rockNoise * 0.008f;
                h = Mathf.Lerp(h, routeSurface, routeMask * _routeFlattenStrength);

                // ゾーンの見せ場は、プロップが置きやすく歩ける台地として軽く整形する。
                h = ApplyPlateaus(h, worldX, worldZ);

                // ベースキャンプ付近を平坦化（プレイヤーが立てるように）。
                if (nz < 0.10f)
                    h *= Mathf.SmoothStep(0f, 1f, nz / 0.10f);

                heights[zi, xi] = Mathf.Clamp01(h);
            }
        }

        return heights;
    }

    private float ComputeRouteCenterX(float worldZ, float nz)
    {
        float baseNoise = FractalNoise(0f, worldZ, _routeNoiseScale, 3, 0.5f, 2f, 53) - 0.5f;
        float summitTaper = 1f - Mathf.SmoothStep(0.78f, 0.96f, nz);
        float basecampTaper = Mathf.SmoothStep(0.08f, 0.22f, nz);
        return baseNoise * 2f * _routeMeanderMeters * summitTaper * basecampTaper;
    }

    private float ApplyPlateaus(float height, float worldX, float worldZ)
    {
        height = ApplyPlateau(height, worldX, worldZ, 0f, -130f, 36f, 18f, 0.018f, 0.92f);
        height = ApplyPlateau(height, worldX, worldZ, 0f, 62f, 26f, 20f, ZoneProfile(WorldZToNz(62f)) + 0.015f, 0.58f);
        height = ApplyPlateau(height, worldX, worldZ, 0f, 132f, 24f, 18f, 0.965f, 0.82f);
        return height;
    }

    private float ApplyPlateau(float height, float worldX, float worldZ, float centerX, float centerZ, float width, float length, float targetHeight, float strength)
    {
        float dx = Mathf.Abs(worldX - centerX) / Mathf.Max(width * 0.5f, 0.001f);
        float dz = Mathf.Abs(worldZ - centerZ) / Mathf.Max(length * 0.5f, 0.001f);
        float distance = Mathf.Max(dx, dz);
        float mask = 1f - Smooth01((distance - 0.65f) / 0.35f);
        return Mathf.Lerp(height, targetHeight, mask * strength);
    }

    private float WorldZToNz(float worldZ)
    {
        return Mathf.InverseLerp(-_terrainLength * 0.5f, _terrainLength * 0.5f, worldZ);
    }

    private Vector2 DomainWarp(float x, float z)
    {
        float wx = SignedFbm(x, z, _domainWarpScale, 3, 0.55f, 2.0f, 101);
        float wz = SignedFbm(x + 37.1f, z - 19.7f, _domainWarpScale, 3, 0.55f, 2.0f, 131);
        return new Vector2(x + wx * _domainWarpMeters, z + wz * _domainWarpMeters);
    }

    private float SignedFbm(float x, float z, float scale, int octaves, float persistence, float lacunarity, int salt)
    {
        return FractalNoise(x, z, scale, octaves, persistence, lacunarity, salt) * 2f - 1f;
    }

    private float RidgedFbm(float x, float z, float scale, int octaves, float persistence, float lacunarity, int salt)
    {
        float frequency = Mathf.Max(scale, 0.0001f);
        float amplitude = 1f;
        float total = 0f;
        float normalizer = 0f;
        float ox = SeedOffset(salt);
        float oz = SeedOffset(salt + 1);

        for (int i = 0; i < octaves; i++)
        {
            float n = Mathf.PerlinNoise(x * frequency + ox, z * frequency + oz) * 2f - 1f;
            float ridge = 1f - Mathf.Abs(n);
            total += ridge * ridge * amplitude;
            normalizer += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
            ox += 23.41f;
            oz += 31.77f;
        }

        return normalizer > 0f ? total / normalizer : 0f;
    }

    private float FractalNoise(float x, float z, float scale, int octaves, float persistence, float lacunarity, int salt)
    {
        float frequency = Mathf.Max(scale, 0.0001f);
        float amplitude = 1f;
        float total = 0f;
        float normalizer = 0f;
        float ox = SeedOffset(salt);
        float oz = SeedOffset(salt + 1);

        for (int i = 0; i < octaves; i++)
        {
            total += Mathf.PerlinNoise(x * frequency + ox, z * frequency + oz) * amplitude;
            normalizer += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
            ox += 17.13f;
            oz += 29.71f;
        }

        return normalizer > 0f ? total / normalizer : 0f;
    }

    private float SeedOffset(int salt)
    {
        int value = Mathf.Abs((_seed * 73856093) ^ (salt * 19349663));
        return (value % 100000) * 0.001f;
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static float Bell(float value, float center, float width)
    {
        float t = Mathf.Abs(value - center) / Mathf.Max(width, 0.0001f);
        return 1f - Smooth01(t);
    }

    /// <summary>nz (0=ベースキャンプ, 1=山頂) を正規化高度 0〜1 に変換する。</summary>
    private static float ZoneProfile(float nz)
    {
        for (int i = 0; i < s_zoneNz.Length - 1; i++)
        {
            if (nz > s_zoneNz[i + 1]) continue;

            float t = Mathf.InverseLerp(s_zoneNz[i], s_zoneNz[i + 1], nz);
            t = t * t * (3f - 2f * t);  // smoothstep
            return Mathf.Lerp(s_zoneH[i], s_zoneH[i + 1], t);
        }
        return s_zoneH[s_zoneH.Length - 1];
    }

    // ──────────────────────────────────────────────────────────
    //  ゾーンマーカー自動配置
    // ──────────────────────────────────────────────────────────

    private static readonly string[] s_zoneNames =
    {
        "Basecamp", "Zone1_Forest", "Zone2_RockySlope",
        "Zone3_CliffWall", "Zone4_TempleRuins",
        "Zone5_IceWall", "Zone6_Summit"
    };

    private void PositionZoneMarkers()
    {
        if (_terrain == null) return;

        for (int i = 0; i < s_zoneNames.Length; i++)
        {
            var go = GetSceneObjectByName(s_zoneNames[i]);
            if (go == null) continue;

            float wz = -_terrainLength * 0.5f + s_zoneNz[i] * _terrainLength;
            float wx = 0f;
            float wy = _terrain.SampleHeight(new Vector3(wx, 0f, wz));
            go.transform.position = new Vector3(wx, wy + 1f, wz);
        }
    }

    // ──────────────────────────────────────────────────────────
    //  地形面スナップ
    // ──────────────────────────────────────────────────────────

    private void SnapAllToTerrain()
    {
        if (_terrain == null) return;

        // (オブジェクト名, 各子に加えるY オフセット)
        var targets = new (string name, float yOffset)[]
        {
            ("GrappableRocks",    0.0f),
            ("IcePatches",        0.1f),
            ("Checkpoints",       1.5f),
            ("RouteGates",        0.0f),
            ("RelicSpawnPoints",  0.5f),
            ("PlayerSpawnPoints", 0.5f),
            ("HazardSpawnPoints", 3.0f),
        };

        foreach (var (name, yOffset) in targets)
        {
            var root = GetSceneObjectByName(name);
            if (root == null) continue;
            SnapChildrenToTerrain(root.transform, yOffset);
        }
    }

    private void SnapChildrenToTerrain(Transform root, float yOffset)
    {
        float tw = _terrainWidth  * 0.5f;
        float tl = _terrainLength * 0.5f;

        for (int i = 0; i < root.childCount; i++)
        {
            var  child = root.GetChild(i);
            var  pos   = child.position;

            // 地形範囲内にクランプ
            float cx = Mathf.Clamp(pos.x, -tw, tw);
            float cz = Mathf.Clamp(pos.z, -tl, tl);

            float y = _terrain.SampleHeight(new Vector3(cx, 0f, cz));
            child.position = new Vector3(cx, y + yOffset, cz);
        }
    }

    // ──────────────────────────────────────────────────────────
    //  Zone プレースホルダー Cube の Mesh を非表示化
    // ──────────────────────────────────────────────────────────

    private static readonly string[] s_zoneObjectNames =
    {
        "Zone1_Forest", "Zone2_RockySlope", "Zone3_CliffWall",
        "Zone4_TempleRuins", "Zone5_IceWall", "Zone6_Summit"
    };

    private void HideZoneCubeMeshes()
    {
        foreach (var zoneName in s_zoneObjectNames)
        {
            var go = GetSceneObjectByName(zoneName);
            if (go == null) continue;

            // 当該 GameObject 自体のMesh/Colliderを無効化
            DisableMeshComponents(go);

            // 子オブジェクトも処理（SummitGoal 等は除外）
            foreach (Transform child in go.transform)
            {
                if (child.GetComponent<ReviveShrine>() != null) continue;
                DisableMeshComponents(child.gameObject);
            }
        }
    }

    private static void DisableMeshComponents(GameObject go)
    {
        var mr = go.GetComponent<MeshRenderer>();
        var bc = go.GetComponent<BoxCollider>();

        // MeshRenderer を非表示にするだけで十分（MeshFilter は enabled プロパティなし）
        if (mr != null) mr.enabled = false;
        // Collider は Terrain 側に衝突判定が移るため無効化
        if (bc != null) bc.enabled = false;
    }

    private void RebuildSceneObjectCache()
    {
        _sceneObjectsByName.Clear();
        var transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (var t in transforms)
        {
            if (t == null) continue;
            if (!_sceneObjectsByName.ContainsKey(t.name))
                _sceneObjectsByName[t.name] = t.gameObject;
        }
    }

    private GameObject GetSceneObjectByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return null;
        if (_sceneObjectsByName.Count == 0)
            RebuildSceneObjectCache();

        _sceneObjectsByName.TryGetValue(objectName, out var go);
        return go;
    }

    // ──────────────────────────────────────────────────────────
    //  Editor 専用: TerrainData アセット保存
    // ──────────────────────────────────────────────────────────

#if UNITY_EDITOR
    // ──────────────────────────────────────────────────────────
    //  Editor メニュー項目（Peak Plunder > Generate Mountain Terrain）
    // ──────────────────────────────────────────────────────────

    [MenuItem("Peak Plunder/Generate Mountain Terrain")]
    private static void GenerateFromMenu()
    {
        var gen = Object.FindFirstObjectByType<MountainTerrainGenerator>();
        if (gen == null)
        {
            Debug.LogWarning("[MountainTerrain] シーンに MountainTerrainGenerator が見つかりません。"
                + "World/MountainTerrainGenerator に追加してから実行してください。");
            return;
        }
        gen.Generate();
    }

    private void PersistTerrainData(TerrainData data)
    {
        const string dir  = "Assets/Sandbox/Terrain";
        const string path = dir + "/MountainTerrainData.asset";

        if (!System.IO.Directory.Exists(Application.dataPath + "/../" + dir))
            System.IO.Directory.CreateDirectory(Application.dataPath + "/../" + dir);

        // 既存アセットを削除してから再作成（上書き）
        if (AssetDatabase.LoadAssetAtPath<TerrainData>(path) != null)
            AssetDatabase.DeleteAsset(path);

        AssetDatabase.CreateAsset(data, path);
        AssetDatabase.SaveAssets();

        // 保存した TerrainData をコンポーネント参照に差し替え
        var saved = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
        var terrainComp = GetComponentInChildren<Terrain>();
        if (terrainComp != null)
        {
            terrainComp.terrainData = saved;
            var col = terrainComp.GetComponent<TerrainCollider>();
            if (col != null) col.terrainData = saved;
        }

        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[MountainTerrain] TerrainData を保存: {path}");
    }
#endif
}
