using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敵モンスターのスポーン管理。Climbing フェーズで山の各ステージ（標高ゾーン）に対応した位置へ
/// プリミティブ製モンスターを配置し、Result/Basecamp フェーズで撤去する（外部アセット不要・CLAUDE.md 準拠）。
///
/// 配置方針:
///   - 拠点（基地）と拠点周辺（_basecampSafeRadius 内）には一切湧かせない＝安全地帯。
///   - Zone1〜6 の各ゾーンを MountainProfile の登攀ルート(基地→山頂)上の割合へ写像し、
///     ゾーンごとに原型(Archetype)と数を変えて配置する（低地=導入の弱め、高地=複数で難化）。
///   - ルート XZ は CombinedTerrainConformer が MountainProfile.PublishRoute で公開する。
///     ルートが無いオフライン単体テスト等では、タイムアウト後に従来のプレイヤー周辺スポーンへ退避。
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    // ── ゾーン別スポーン設定 ───────────────────────────────────
    [System.Serializable]
    public struct ZoneSpawnRule
    {
        [Range(1, 6)] public int zone;        // 1=Forest 〜 6=Summit
        public EnemyArchetype archetype;
        [Min(0)] public int count;
    }

    [Header("ゾーン別スポーン（山のステージに対応）")]
    [Tooltip("ON で各ゾーン(標高ステージ)に対応して敵を配置する。ルート未確定/OFF 時はプレイヤー周辺へ退避。")]
    [SerializeField] private bool _zoneBasedSpawning = true;

    [Tooltip("基地中心からこの水平距離内には敵を一切湧かせない（拠点＝安全地帯）。" +
             "敵の最大視界(Stalker=30m)＋余裕を取る。")]
    [SerializeField] private float _basecampSafeRadius = 60f;

    [Tooltip("各ゾーンへ配置する原型と数。空ならコード内 DefaultRules を使用。")]
    [SerializeField] private ZoneSpawnRule[] _zoneRules;

    // ── 従来（フォールバック）スポーン設定 ─────────────────────
    [Header("フォールバック（プレイヤー周辺・ルート未確定時のみ）")]
    [SerializeField] private int _enemyCount = 3;
    [SerializeField] private float _spawnRadius = 60f;
    [Tooltip("プレイヤーからこの距離(水平)未満には敵を湧かせない。基地スポーン直後の即死を防ぐため、" +
             "敵の視界距離(EnemyConfigSO.VisionRange 既定 22m)より大きく取る。")]
    [SerializeField] private float _minDistanceFromPlayers = 40f;

    // ── 定数 ──────────────────────────────────────────────────
    private const float SPAWN_Y_OFFSET = 1.4f;

    // 敵の視界(EnemyConfigSO.VisionRange 既定 22m)＋余裕。これ未満の距離で湧くと、スポーン直後に
    // 即座に索敵・追跡され、身動きが取れないまま HP が削られる。serialize 値が小さくても下限として強制する。
    private const float SAFE_SPAWN_DISTANCE = 38f;

    // ゾーン番号→ルート割合(0=基地,1=山頂)。CombinedTerrainConformer の登攀コース配置
    // (climbStartFraction=0.08 〜 climbTopFraction=0.9) と一致させ、遺物/祠と同じステージ帯へ並べる。
    private const float ROUTE_START_FRACTION = 0.08f;
    private const float ROUTE_TOP_FRACTION   = 0.9f;

    private const float ZONE_LATERAL_SPREAD = 26f;   // ルート横方向のばらつき[m]
    private const float ZONE_ALONG_SPREAD   = 20f;   // ルート前後方向のばらつき[m]
    private const float ZONE_FRACTION_BAND  = 0.14f; // 接地点の高度がこの割合差以内なら当該ゾーンとして許容
    private const float ROUTE_WAIT_TIMEOUT  = 15f;   // ルート確定(山頂安定)をこの秒数待ち、超えたらフォールバック
    private const float ZONE_FILL_TIMEOUT   = 6f;    // 残ゾーンをこの秒数で諦めて確定（高所チャンク後着対策）

    private static readonly EnemyArchetype[] ARCHETYPE_ROTATION =
        { EnemyArchetype.Brute, EnemyArchetype.Stalker, EnemyArchetype.Listener };

    // 既定のゾーン配分（serialize 未設定時）。低地=導入の弱め(速いが低火力の Stalker / 単体)、
    // 中腹=火力(Brute)・聴覚(Listener)、高地=複数原型で難化、というステージ設計。
    private static readonly ZoneSpawnRule[] DefaultRules =
    {
        new ZoneSpawnRule { zone = 1, archetype = EnemyArchetype.Stalker,  count = 1 }, // Forest: 導入
        new ZoneSpawnRule { zone = 2, archetype = EnemyArchetype.Brute,    count = 1 }, // RockySlope: 火力導入
        new ZoneSpawnRule { zone = 3, archetype = EnemyArchetype.Listener, count = 1 }, // CliffWall: 静音登攀
        new ZoneSpawnRule { zone = 4, archetype = EnemyArchetype.Brute,    count = 1 }, // TempleRuins
        new ZoneSpawnRule { zone = 4, archetype = EnemyArchetype.Stalker,  count = 1 },
        new ZoneSpawnRule { zone = 5, archetype = EnemyArchetype.Listener, count = 1 }, // IceWall
        new ZoneSpawnRule { zone = 5, archetype = EnemyArchetype.Stalker,  count = 1 },
        new ZoneSpawnRule { zone = 6, archetype = EnemyArchetype.Brute,    count = 1 }, // Summit: 最高難度
        new ZoneSpawnRule { zone = 6, archetype = EnemyArchetype.Listener, count = 1 },
    };

    // ── 状態 ──────────────────────────────────────────────────
    private struct PendingSpawn { public int zone; public EnemyArchetype archetype; public int index; }

    private readonly List<EnemyController> _spawned = new();
    private List<PendingSpawn> _pending;   // null=未構築。ゾーン別の配置待ちリスト
    private bool _hasSpawned;
    private float _routeWaitTime;
    private float _fillElapsed;
    private LayerMask _groundMask;

    private float EffectiveMinDistance => Mathf.Max(_minDistanceFromPlayers, SAFE_SPAWN_DISTANCE);
    private float EffectiveSpawnRadius => Mathf.Max(_spawnRadius, EffectiveMinDistance + 35f);
    private ZoneSpawnRule[] EffectiveRules =>
        (_zoneRules != null && _zoneRules.Length > 0) ? _zoneRules : DefaultRules;

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

        // 遠征サービスが無ければオフライン単体テスト：登攀扱いでスポーンを進める。
        if (expedition == null)
        {
            if (!_hasSpawned) TickSpawn();
            return;
        }

        switch (expedition.Phase)
        {
            case ExpeditionPhase.Climbing:
                if (!_hasSpawned) TickSpawn();
                break;
            case ExpeditionPhase.Result:
            case ExpeditionPhase.Basecamp:
                if (_hasSpawned || _pending != null) DespawnAll();
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
    public void DebugSpawnWave()
    {
        _hasSpawned = false;
        _pending = null;
        _routeWaitTime = ROUTE_WAIT_TIMEOUT; // ルート待ちを飛ばす
        TickSpawn();
    }

    /// <summary>デバッグメニュー用: スポーン済みの敵を全て撤去する。</summary>
    public void DebugDespawnAll() => DespawnAll();

    // ── スポーン進行 ──────────────────────────────────────────
    private void TickSpawn()
    {
        bool useZones = _zoneBasedSpawning && MountainProfile.HasRoute;

        // ルート未確定なら少し待つ。一定時間来なければ（山の無いシーン等）フォールバックへ。
        if (_zoneBasedSpawning && !MountainProfile.HasRoute)
        {
            _routeWaitTime += Time.deltaTime;
            if (_routeWaitTime < ROUTE_WAIT_TIMEOUT) return;
            useZones = false;
        }

        if (useZones) TickZoneSpawn();
        else          TickFallbackSpawn();
    }

    private void TickZoneSpawn()
    {
        if (_pending == null) BuildPendingZoneSpawns();

        Vector2 baseXZ = MountainProfile.BaseXZ;
        Vector2 dir = MountainProfile.SummitXZ - baseXZ;
        dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector2.up;
        Vector2 perp = new Vector2(-dir.y, dir.x);

        // 配置できたものから取り除く（高所チャンクが後着でも、焼け次第そのゾーンが埋まる）。
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            if (!TryFindZoneSpawnPoint(_pending[i].zone, baseXZ, dir, perp, out Vector3 pos)) continue;
            var preset = EnemyArchetypes.Build(_pending[i].archetype);
            _spawned.Add(BuildMonster(pos, _pending[i].index, preset));
            _pending.RemoveAt(i);
        }

        _fillElapsed += Time.deltaTime;
        if (_pending.Count == 0 || _fillElapsed >= ZONE_FILL_TIMEOUT)
        {
            _hasSpawned = true;
            Debug.Log($"[EnemySpawner] ゾーン別スポーン完了: {_spawned.Count} 体（未配置 {_pending.Count}）");
        }
    }

    private void BuildPendingZoneSpawns()
    {
        _pending = new List<PendingSpawn>();
        _fillElapsed = 0f;
        int idx = 0;
        foreach (var rule in EffectiveRules)
        {
            int z = Mathf.Clamp(rule.zone, 1, 6);
            for (int k = 0; k < Mathf.Max(0, rule.count); k++)
                _pending.Add(new PendingSpawn { zone = z, archetype = rule.archetype, index = idx++ });
        }
    }

    /// <summary>ゾーン番号→ルート割合(0=基地,1=山頂)。登攀コース配置と同じ写像。</summary>
    private static float ZoneFraction(int zone)
        => Mathf.Lerp(ROUTE_START_FRACTION, ROUTE_TOP_FRACTION, Mathf.Clamp01((zone - 1) / 5f));

    /// <summary>
    /// 指定ゾーンのルート上アンカー周辺で、拠点セーフゾーン外かつ当該標高帯の接地点を探す。
    /// 標高帯に合致しなくても、最も近い接地点を best として確保し、配置漏れを避ける。
    /// </summary>
    private bool TryFindZoneSpawnPoint(int zone, Vector2 baseXZ, Vector2 dir, Vector2 perp, out Vector3 pos)
    {
        float targetFrac = ZoneFraction(zone);
        Vector2 anchor = MountainProfile.RoutePointXZ(targetFrac);
        float safeSqr = _basecampSafeRadius * _basecampSafeRadius;
        float castTopY = MountainProfile.SummitY + 100f;

        Vector3 best = default; bool haveBest = false; float bestErr = float.MaxValue;

        for (int attempt = 0; attempt < 30; attempt++)
        {
            float lat   = Random.Range(-1f, 1f) * ZONE_LATERAL_SPREAD;
            float along = Random.Range(-1f, 1f) * ZONE_ALONG_SPREAD;
            Vector2 xz = anchor + perp * lat + dir * along;

            Vector3 origin = new Vector3(xz.x, castTopY, xz.y);
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1000f, _groundMask, QueryTriggerInteraction.Ignore))
                continue;

            // 拠点セーフゾーン除外（水平）。拠点＝安全地帯のため必須。
            Vector2 hxz = new Vector2(hit.point.x, hit.point.z);
            if ((hxz - baseXZ).sqrMagnitude < safeSqr) continue;
            // 基地に立つプレイヤーへの即時攻撃も避ける。
            if (IsTooCloseToPlayers(hit.point)) continue;

            float err = Mathf.Abs(MountainProfile.Fraction(hit.point.y) - targetFrac);
            if (err <= ZONE_FRACTION_BAND)
            {
                pos = hit.point + Vector3.up * SPAWN_Y_OFFSET;
                return true;
            }
            if (err < bestErr) { bestErr = err; best = hit.point; haveBest = true; }
        }

        if (haveBest) { pos = best + Vector3.up * SPAWN_Y_OFFSET; return true; }
        pos = default;
        return false;
    }

    // ── フォールバック（プレイヤー周辺・基地除外あり）─────────
    private void TickFallbackSpawn()
    {
        int placed = 0;
        for (int i = 0; i < _enemyCount; i++)
        {
            if (!TryFindFallbackSpawnPoint(out Vector3 pos)) continue;
            var preset = EnemyArchetypes.Build(ARCHETYPE_ROTATION[i % ARCHETYPE_ROTATION.Length]);
            _spawned.Add(BuildMonster(pos, i, preset));
            placed++;
        }

        // 1体も置けないうちは確定せず再試行（着地スナップ中/地形コライダー未生成で接地点が無い）。
        if (placed == 0) return;

        _hasSpawned = true;
        Debug.Log($"[EnemySpawner] フォールバックスポーン: {_spawned.Count} 体");
    }

    private bool TryFindFallbackSpawnPoint(out Vector3 pos)
    {
        Vector3 center = SpawnCenter();
        bool hasBase = TryGetBasecampXZ(out Vector2 baseXZ);
        float safeSqr = _basecampSafeRadius * _basecampSafeRadius;

        for (int attempt = 0; attempt < 24; attempt++)
        {
            Vector2 xz = Random.insideUnitCircle * EffectiveSpawnRadius;
            Vector3 origin = center + new Vector3(xz.x, 80f, xz.y);
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 200f, _groundMask, QueryTriggerInteraction.Ignore))
                continue;

            if (IsTooCloseToPlayers(hit.point)) continue;
            if (hasBase)
            {
                Vector2 hxz = new Vector2(hit.point.x, hit.point.z);
                if ((hxz - baseXZ).sqrMagnitude < safeSqr) continue; // 拠点周辺は除外
            }
            pos = hit.point + Vector3.up * SPAWN_Y_OFFSET;
            return true;
        }
        pos = transform.position;
        return false;
    }

    private bool TryGetBasecampXZ(out Vector2 xz)
    {
        if (MountainProfile.HasRoute) { xz = MountainProfile.BaseXZ; return true; }
        var pad = GameObject.Find("BasecampPad") ?? GameObject.Find("Basecamp");
        if (pad != null) { var p = pad.transform.position; xz = new Vector2(p.x, p.z); return true; }
        xz = default;
        return false;
    }

    private void DespawnAll()
    {
        foreach (var e in _spawned)
            if (e != null) Destroy(e.gameObject);
        _spawned.Clear();
        _pending = null;
        _hasSpawned = false;
        _routeWaitTime = 0f;
        _fillElapsed = 0f;
    }

    private Vector3 SpawnCenter()
    {
        var players = PlayerHealthSystem.RegisteredPlayers;
        for (int i = 0; i < players.Count; i++)
            if (players[i] != null) return players[i].transform.position;
        return transform.position;
    }

    private bool IsTooCloseToPlayers(Vector3 point)
    {
        float minSqr = EffectiveMinDistance * EffectiveMinDistance;
        var players = PlayerHealthSystem.RegisteredPlayers;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] == null) continue;
            // 水平(XZ)距離で判定する。スポーン直後はプレイヤーがまだ基地台座へ着地スナップされて
            // おらず Y が大きく低い瞬間があり、3D 距離だと Y 差で水増しされて誤判定されるため。
            Vector3 d = players[i].transform.position - point;
            d.y = 0f;
            if (d.sqrMagnitude < minSqr) return true;
        }
        return false;
    }

    // ── モンスター生成（描画/物理。原型ごとの体格・色）─────────
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
