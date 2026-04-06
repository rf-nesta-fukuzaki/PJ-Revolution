using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Perlin Noise で Unity Terrain を生成し、岩・木・チェックポイント・山頂を配置する。
/// </summary>
public class MountainGenerator : MonoBehaviour
{
    [Header("Terrain Settings")]
    [SerializeField] private int terrainResolution = 513;
    [SerializeField] private float terrainWidth = 300f;
    [SerializeField] private float terrainLength = 300f;
    [SerializeField] private float terrainHeight = 200f;

    [Header("Noise Settings")]
    [SerializeField] private float noiseScale = 0.01f;
    [SerializeField] private int octaves = 5;
    [SerializeField] private float persistence = 0.5f;
    [SerializeField] private float lacunarity = 2f;
    [SerializeField] private float coneStrength = 1.5f;
    [SerializeField] private int seed = 42;

    [Header("Rock Placement")]
    [SerializeField] private int rockCount = 60;
    [SerializeField] private float rockMinScale = 1f;
    [SerializeField] private float rockMaxScale = 4f;

    [Header("Tree Placement")]
    [SerializeField] private int treeCount = 35;

    [Header("Checkpoint Settings")]
    [SerializeField] private int checkpointCount = 3;

    [Header("References")]
    [SerializeField] private CheckpointSystem checkpointSystem;
    [SerializeField] private SummitGoal summitGoal;

    private Terrain _terrain;
    private List<GameObject> _grappables = new List<GameObject>();

    private void Start()
    {
        GenerateTerrain();
        PlaceRocks();
        PlaceTrees();
        PlaceCheckpoints();
        PlaceSummit();

        Application.targetFrameRate = 60;
    }

    public void GenerateTerrain()
    {
        var terrainData = new TerrainData();
        terrainData.heightmapResolution = terrainResolution;
        terrainData.size = new Vector3(terrainWidth, terrainHeight, terrainLength);

        float[,] heights = GenerateHeightMap(terrainResolution);
        terrainData.SetHeights(0, 0, heights);

        var terrainGo = Terrain.CreateTerrainGameObject(terrainData);
        terrainGo.name = "Mountain_Terrain";
        terrainGo.transform.position = new Vector3(-terrainWidth / 2f, 0f, -terrainLength / 2f);
        terrainGo.layer = LayerMask.NameToLayer("Default");

        _terrain = terrainGo.GetComponent<Terrain>();
        _terrain.Flush();
    }

    private float[,] GenerateHeightMap(int res)
    {
        float[,] heights = new float[res, res];
        var rng = new System.Random(seed);
        float offsetX = (float)rng.NextDouble() * 10000f;
        float offsetY = (float)rng.NextDouble() * 10000f;
        float centerX = res / 2f;
        float centerZ = res / 2f;
        float maxDist = Mathf.Sqrt(centerX * centerX + centerZ * centerZ);

        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float noiseVal = GetOctaveNoise(
                    (x + offsetX) * noiseScale,
                    (z + offsetY) * noiseScale);

                // 円錐形フォールオフ（山形）
                float dx = (x - centerX) / centerX;
                float dz = (z - centerZ) / centerZ;
                float dist = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dz * dz));
                float cone = Mathf.Pow(1f - dist, coneStrength);

                heights[z, x] = Mathf.Clamp01(noiseVal * cone);
            }
        }
        return heights;
    }

    private float GetOctaveNoise(float x, float z)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxValue = 0f;

        for (int i = 0; i < octaves; i++)
        {
            value += Mathf.PerlinNoise(x * frequency, z * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }
        return value / maxValue;
    }

    public void PlaceRocks()
    {
        if (_terrain == null) return;
        var rng = new System.Random(seed + 1);

        for (int i = 0; i < rockCount; i++)
        {
            float x = (float)rng.NextDouble() * terrainWidth - terrainWidth / 2f;
            float z = (float)rng.NextDouble() * terrainLength - terrainLength / 2f;
            float y = _terrain.SampleHeight(new Vector3(x, 0f, z));

            // 頂上付近（高度 60% 以上）に多く配置
            if (y < terrainHeight * 0.3f && rng.NextDouble() < 0.5f)
            {
                i--;
                continue;
            }

            var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = $"Rock_{i}";
            float scale = (float)rng.NextDouble() * (rockMaxScale - rockMinScale) + rockMinScale;
            rock.transform.localScale = new Vector3(
                scale * (0.8f + (float)rng.NextDouble() * 0.4f),
                scale * (0.5f + (float)rng.NextDouble() * 0.8f),
                scale * (0.8f + (float)rng.NextDouble() * 0.4f));
            rock.transform.position = new Vector3(x, y, z);
            rock.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            rock.tag = "Grappable";

            var mr = rock.GetComponent<MeshRenderer>();
            if (mr != null) mr.material = CreateRockMaterial();

            _grappables.Add(rock);
        }
    }

    public void PlaceTrees()
    {
        if (_terrain == null) return;
        var rng = new System.Random(seed + 2);

        for (int i = 0; i < treeCount; i++)
        {
            float x = (float)rng.NextDouble() * terrainWidth - terrainWidth / 2f;
            float z = (float)rng.NextDouble() * terrainLength - terrainLength / 2f;
            float y = _terrain.SampleHeight(new Vector3(x, 0f, z));

            // 中腹（高度 20%〜70%）に木を配置
            float normalizedHeight = y / terrainHeight;
            if (normalizedHeight < 0.1f || normalizedHeight > 0.7f)
            {
                i--;
                continue;
            }

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = $"Tree_{i}";
            trunk.transform.position = new Vector3(x, y + 1.5f, z);
            trunk.transform.localScale = new Vector3(0.4f, 3f, 0.4f);
            trunk.tag = "Grappable";

            var mr = trunk.GetComponent<MeshRenderer>();
            if (mr != null) mr.material = CreateTreeMaterial();

            // 葉っぱ
            var foliage = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            foliage.transform.SetParent(trunk.transform);
            foliage.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            foliage.transform.localScale = new Vector3(2.5f, 2f, 2.5f);
            foliage.tag = "Grappable";
            Destroy(foliage.GetComponent<Collider>());
            var fmr = foliage.GetComponent<MeshRenderer>();
            if (fmr != null) fmr.material = CreateFoliageMaterial();

            _grappables.Add(trunk);
        }
    }

    public void PlaceCheckpoints()
    {
        if (_terrain == null) return;
        if (checkpointSystem == null)
            checkpointSystem = FindFirstObjectByType<CheckpointSystem>();

        float[] heights = { 0.2f, 0.45f, 0.65f, 0.82f };

        for (int i = 0; i < Mathf.Min(checkpointCount, heights.Length); i++)
        {
            float targetHeight = terrainHeight * heights[i];
            Vector3 pos = FindPositionAtHeight(targetHeight, seed + 10 + i);

            var cp = new GameObject($"Checkpoint_{i + 1}");
            cp.transform.position = pos;
            cp.tag = "Checkpoint";

            var col = cp.AddComponent<SphereCollider>();
            col.radius = 3f;
            col.isTrigger = true;

            var comp = cp.AddComponent<CheckpointTrigger>();
            comp.index = i;
            comp.label = $"Checkpoint {i + 1}/{checkpointCount}";

            // 目印のポール
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.transform.SetParent(cp.transform);
            pole.transform.localPosition = Vector3.zero;
            pole.transform.localScale = new Vector3(0.2f, 2f, 0.2f);
            var mr = pole.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(1f, 0.8f, 0f);
                mr.material = mat;
            }

            if (checkpointSystem != null)
                checkpointSystem.RegisterCheckpoint(cp.transform);
        }
    }

    public void PlaceSummit()
    {
        if (_terrain == null) return;
        if (summitGoal == null)
            summitGoal = FindFirstObjectByType<SummitGoal>();

        // 地形中央の最高点付近
        float y = terrainHeight * 0.92f;
        Vector3 pos = FindPositionAtHeight(y, seed + 99);

        var summit = new GameObject("Summit");
        summit.transform.position = pos;

        var col = summit.AddComponent<SphereCollider>();
        col.radius = 5f;
        col.isTrigger = true;

        if (summitGoal == null)
        {
            summitGoal = summit.AddComponent<SummitGoal>();
        }
        else
        {
            summit.AddComponent<SummitGoal>();
        }

        // 旗のポール
        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.transform.SetParent(summit.transform);
        pole.transform.localPosition = new Vector3(0f, 3f, 0f);
        pole.transform.localScale = new Vector3(0.15f, 3f, 0.15f);

        // 旗の部分
        var flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
        flag.transform.SetParent(summit.transform);
        flag.transform.localPosition = new Vector3(0.5f, 5.5f, 0f);
        flag.transform.localScale = new Vector3(1f, 0.5f, 0.05f);
        var fmr = flag.GetComponent<MeshRenderer>();
        if (fmr != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = Color.red;
            fmr.material = mat;
        }
    }

    private Vector3 FindPositionAtHeight(float targetHeight, int seedOffset)
    {
        var rng = new System.Random(seedOffset);
        for (int attempt = 0; attempt < 100; attempt++)
        {
            float x = (float)rng.NextDouble() * terrainWidth * 0.6f - terrainWidth * 0.3f;
            float z = (float)rng.NextDouble() * terrainLength * 0.6f - terrainLength * 0.3f;
            float y = _terrain.SampleHeight(new Vector3(x, 0f, z));
            if (Mathf.Abs(y - targetHeight) < terrainHeight * 0.1f)
                return new Vector3(x, y + 1f, z);
        }
        // フォールバック: 中央
        float cy = _terrain.SampleHeight(Vector3.zero);
        return new Vector3(0f, cy + 1f, 0f);
    }

    private Material CreateRockMaterial()
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = new Color(0.55f, 0.5f, 0.45f);
        return mat;
    }

    private Material CreateTreeMaterial()
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = new Color(0.4f, 0.25f, 0.1f);
        return mat;
    }

    private Material CreateFoliageMaterial()
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = new Color(0.15f, 0.45f, 0.1f);
        return mat;
    }
}
