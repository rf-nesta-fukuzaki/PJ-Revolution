using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敵モンスターのスポーン管理。Climbing フェーズ開始でプリミティブ製モンスターを
/// 地形上に配置し、Result フェーズで撤去する（外部アセット不要・CLAUDE.md 準拠）。
/// 遠征サービスが無いオフライン単体テストでは即時スポーンする。
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("スポーン")]
    [SerializeField] private int _enemyCount = 3;
    [SerializeField] private float _spawnRadius = 60f;
    [SerializeField] private float _minDistanceFromPlayers = 22f;

    private const float SPAWN_Y_OFFSET = 1.4f;
    private static readonly EnemyArchetype[] ARCHETYPE_ROTATION =
        { EnemyArchetype.Brute, EnemyArchetype.Stalker, EnemyArchetype.Listener };

    private readonly List<EnemyController> _spawned = new();
    private bool _hasSpawned;
    private LayerMask _groundMask;

    private void Awake()
    {
        int mask = Physics.DefaultRaycastLayers;
        int ground = LayerMask.NameToLayer("Ground");
        if (ground >= 0) mask |= 1 << ground;
        int player = LayerMask.NameToLayer("Player");
        if (player >= 0) mask &= ~(1 << player);
        mask &= ~(1 << 2);
        _groundMask = mask;
    }

    private void Update()
    {
        var expedition = GameServices.Expedition;

        // 遠征サービスが無ければオフライン単体テスト：一度だけ即時スポーン
        if (expedition == null)
        {
            if (!_hasSpawned) SpawnWave();
            return;
        }

        switch (expedition.Phase)
        {
            case ExpeditionPhase.Climbing:
                if (!_hasSpawned) SpawnWave();
                break;
            case ExpeditionPhase.Result:
            case ExpeditionPhase.Basecamp:
                if (_hasSpawned) DespawnAll();
                break;
        }
    }

    /// <summary>現在アクティブな（破壊されていない）敵の数。</summary>
    public int ActiveEnemyCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) n++;
            return n;
        }
    }

    /// <summary>デバッグメニュー用: フェーズに関係なく即時に1ウェーブをスポーンする。</summary>
    public void DebugSpawnWave() => SpawnWave();

    /// <summary>デバッグメニュー用: スポーン済みの敵を全て撤去する。</summary>
    public void DebugDespawnAll() => DespawnAll();

    private void SpawnWave()
    {
        _hasSpawned = true;
        for (int i = 0; i < _enemyCount; i++)
        {
            if (!TryFindSpawnPoint(out Vector3 pos)) continue;
            var preset = EnemyArchetypes.Build(ARCHETYPE_ROTATION[i % ARCHETYPE_ROTATION.Length]);
            _spawned.Add(BuildMonster(pos, i, preset));
        }
        Debug.Log($"[EnemySpawner] モンスター {_spawned.Count} 体をスポーンしました（複数原型）");
    }

    private void DespawnAll()
    {
        foreach (var e in _spawned)
            if (e != null) Destroy(e.gameObject);
        _spawned.Clear();
        _hasSpawned = false;
    }

    private Vector3 SpawnCenter()
    {
        var players = PlayerHealthSystem.RegisteredPlayers;
        for (int i = 0; i < players.Count; i++)
            if (players[i] != null) return players[i].transform.position;
        return transform.position;
    }

    private bool TryFindSpawnPoint(out Vector3 pos)
    {
        Vector3 center = SpawnCenter();
        for (int attempt = 0; attempt < 24; attempt++)
        {
            Vector2 xz = Random.insideUnitCircle * _spawnRadius;
            Vector3 origin = center + new Vector3(xz.x, 80f, xz.y);
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 200f, _groundMask, QueryTriggerInteraction.Ignore))
                continue;

            if (IsTooCloseToPlayers(hit.point)) continue;
            pos = hit.point + Vector3.up * SPAWN_Y_OFFSET;
            return true;
        }
        pos = transform.position;
        return false;
    }

    private bool IsTooCloseToPlayers(Vector3 point)
    {
        var players = PlayerHealthSystem.RegisteredPlayers;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] == null) continue;
            if (Vector3.Distance(players[i].transform.position, point) < _minDistanceFromPlayers)
                return true;
        }
        return false;
    }

    private EnemyController BuildMonster(Vector3 pos, int index, EnemyArchetypes.Preset preset)
    {
        float h = preset.BodyHeight;
        float w = preset.BodyWidth;

        var root = new GameObject($"Monster_{index:00}_{preset.Config.Archetype}");
        root.transform.SetParent(transform, worldPositionStays: true);
        root.transform.position = pos;

        // 胴体（原型ごとの色・体格のカプセル）
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(w, h * 0.5f, w);
        Destroy(body.GetComponent<Collider>()); // 物理コライダーは root 側に付ける
        ApplyColor(body, preset.BodyColor, emissive: false);

        // 光る目 ×2（原型ごとの色）
        CreateEye(root.transform, new Vector3(0.28f * w, h * 0.42f, 0.45f * w), preset.EyeColor);
        CreateEye(root.transform, new Vector3(-0.28f * w, h * 0.42f, 0.45f * w), preset.EyeColor);

        // 物理
        var col = root.AddComponent<CapsuleCollider>();
        col.height = h;
        col.radius = 0.5f * w;
        col.center = Vector3.zero;

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 90f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.freezeRotation = true;

        // AddComponent は即 Awake を呼ばないため、Configure を Awake より先に注入できる
        root.SetActive(false);
        var ctrl = root.AddComponent<EnemyController>();
        ctrl.Configure(preset.Config);
        root.SetActive(true);
        return ctrl;
    }

    private void CreateEye(Transform parent, Vector3 localPos, Color eyeColor)
    {
        var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eye.name = "Eye";
        eye.transform.SetParent(parent, false);
        eye.transform.localPosition = localPos;
        eye.transform.localScale = Vector3.one * 0.16f;
        Destroy(eye.GetComponent<Collider>());
        ApplyColor(eye, eyeColor, emissive: true);
    }

    private static void ApplyColor(GameObject go, Color color, bool emissive)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader) { color = color };
        if (emissive)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 3f);
        }
        r.sharedMaterial = mat;
    }
}
