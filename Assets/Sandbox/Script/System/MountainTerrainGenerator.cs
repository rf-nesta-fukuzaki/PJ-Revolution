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
    [SerializeField] private float _terrainHeight = 220f;
    [SerializeField] private int   _resolution    = 513;    // 2^n+1 必須

    // ── Perlin ノイズ ──────────────────────────────────────────
    [Header("Perlin ノイズ")]
    [SerializeField] private int   _seed   = 42;
    [SerializeField] private float _scale1 = 0.007f;
    [SerializeField] private float _amp1   = 0.55f;
    [SerializeField] private float _scale2 = 0.022f;
    [SerializeField] private float _amp2   = 0.22f;
    [SerializeField] private float _scale3 = 0.065f;
    [SerializeField] private float _amp3   = 0.07f;

    // ── マテリアル ─────────────────────────────────────────────
    [Header("Terrain Material (任意 / 未設定=デフォルト)")]
    [SerializeField] private Material _terrainMaterial;

    // ── ゾーン別高度プロファイル ────────────────────────────────
    // nz 0.0 = ベースキャンプ側、nz 1.0 = 山頂側（0〜1 の正規化 Z 座標）
    // height 0.0〜1.0 は _terrainHeight に掛ける係数
    private static readonly float[] s_zoneNz =
        { 0.04f, 0.18f, 0.35f, 0.52f, 0.65f, 0.78f, 0.93f };
    private static readonly float[] s_zoneH =
        { 0.00f, 0.11f, 0.30f, 0.55f, 0.64f, 0.82f, 1.00f };

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

        // seed から再現可能なオフセット値を生成
        float ox1 = (_seed * 7919 % 9973) / 99.73f;
        float oz1 = (_seed * 6271 % 9973) / 99.73f;
        float ox2 = (_seed * 4013 % 9973) / 99.73f;
        float oz2 = (_seed * 3571 % 9973) / 99.73f;
        float ox3 = (_seed * 2011 % 9973) / 99.73f;
        float oz3 = (_seed * 1999 % 9973) / 99.73f;

        float totalAmp = _amp1 + _amp2 + _amp3;

        for (int zi = 0; zi < res; zi++)
        {
            float nz    = (float)zi / (res - 1);
            float baseH = ZoneProfile(nz);

            for (int xi = 0; xi < res; xi++)
            {
                float nx = (float)xi / (res - 1);

                // ① 稜線: 山頂ほど細く、麓ほど広い
                float halfW = Mathf.Lerp(0.48f, 0.10f, baseH);
                float dx    = Mathf.Abs(nx - 0.5f);
                float ridge = Mathf.Clamp01(1f - dx / Mathf.Max(halfW, 0.001f));
                ridge = ridge * ridge;  // 二乗で側面を急峻に

                // ② Perlin ノイズ（3 オクターブ）
                float n1 = Mathf.PerlinNoise(xi * _scale1 + ox1, zi * _scale1 + oz1) * _amp1;
                float n2 = Mathf.PerlinNoise(xi * _scale2 + ox2, zi * _scale2 + oz2) * _amp2;
                float n3 = Mathf.PerlinNoise(xi * _scale3 + ox3, zi * _scale3 + oz3) * _amp3;
                float noise = (n1 + n2 + n3) / totalAmp;  // 0〜1 に正規化

                // ③ 合算: ゾーンプロファイル × 稜線 + ノイズ補正
                float h = baseH * ridge + noise * 0.12f * ridge;

                // ④ ベースキャンプ付近を平坦化（プレイヤーが立てるように）
                if (nz < 0.09f)
                    h *= Mathf.SmoothStep(0f, 1f, nz / 0.09f);

                heights[zi, xi] = Mathf.Clamp01(h);
            }
        }

        return heights;
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
