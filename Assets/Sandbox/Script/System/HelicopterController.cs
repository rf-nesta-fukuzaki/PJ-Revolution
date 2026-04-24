using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

/// <summary>
/// GDD §2.4 — ヘリコプター演出・搭乗システム。
///
/// フレアガンで上空（仰角 60°以上）に発射 → 60 秒後にヘリが到着。
/// ヘリ周囲でダウンウォッシュ（下向き 30N・半径 10m）を発生させる。
/// プレイヤーは E キーで搭乗。搭乗猶予 30 秒。
/// 全員搭乗 or 猶予終了でヘリ離陸 → リザルト画面へ。
/// </summary>
public class HelicopterController : NetworkBehaviour, IHelicopterService
{
    public static HelicopterController Instance { get; private set; }

    // ── GDD 定数 ─────────────────────────────────────────────
    private const float ARRIVAL_DELAY      = 60f;   // フレア発射からヘリ到着まで（秒）
    private const float BOARDING_WINDOW    = 30f;   // 搭乗猶予（秒）
    private const float DOWNDRAFT_FORCE    = 30f;   // ダウンウォッシュ力（N）
    private const float DOWNDRAFT_RADIUS   = 10f;   // ダウンウォッシュ半径（m）
    private const float APPROACH_ALTITUDE  = 60f;   // アプローチ高度
    private const float HOVER_ALTITUDE     = 8f;    // ホバリング高度

    [Header("ヘリの見た目（任意プレハブ）")]
    [SerializeField] private GameObject _helicopterMesh;    // 未設定なら Capsule プリミティブで代替

    [Header("搭乗 UI")]
    [SerializeField] private TextMeshProUGUI _boardingTimerText;

    [Header("到着カウントダウン UI (GDD §2.4)")]
    [SerializeField] private TextMeshProUGUI _arrivalTimerText;

    // ── 状態 ─────────────────────────────────────────────────
    private bool    _isActive;
    private bool    _isBoarding;
    private float   _boardingTimer;
    private bool    _isArriving;
    private float   _arrivalTimer;
    private Vector3 _helipadPosition;

    /// <summary>搭乗フェーズが進行中か（PlayerInteraction から参照）。</summary>
    public bool    IsBoarding      => _isBoarding;

    /// <summary>ヘリ到着待機中か（HUD 連動用）。</summary>
    public bool    IsArriving      => _isArriving;

    /// <summary>到着までの残り秒数（HUD 表示用）。</summary>
    public float   ArrivalRemaining => Mathf.Max(_arrivalTimer, 0f);

    /// <summary>搭乗猶予の残り秒数（HUD 表示用）。</summary>
    public float   BoardingRemaining => Mathf.Max(_boardingTimer, 0f);

    /// <summary>ヘリパッドのワールド座標（PlayerInteraction の距離チェック用）。</summary>
    public Vector3 HelipadPosition => _helipadPosition;
    private readonly HashSet<int> _boardedPlayerIds = new();
    private GameObject _heliVisual;

    // ── ネットワーク ─────────────────────────────────────────
    private readonly NetworkVariable<bool> _helicopterCalled = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // HUD 文字がシーン開始直後に常時見える状態を避ける。
        if (_boardingTimerText != null)
        {
            _boardingTimerText.text = string.Empty;
            _boardingTimerText.gameObject.SetActive(false);
        }

        if (_arrivalTimerText != null)
        {
            _arrivalTimerText.text = string.Empty;
            _arrivalTimerText.gameObject.SetActive(false);
        }
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    private void FixedUpdate()
    {
        if (!_isActive) return;
        ApplyDowndraft();
    }

    private void Update()
    {
        // ヘリ到着カウントダウン (GDD §2.4)
        if (_isArriving)
        {
            _arrivalTimer -= Time.deltaTime;
            UpdateArrivalUI();
            if (_arrivalTimer <= 0f)
            {
                _isArriving = false;
                if (_arrivalTimerText != null)
                    _arrivalTimerText.gameObject.SetActive(false);
            }
        }

        if (!_isBoarding) return;

        _boardingTimer -= Time.deltaTime;
        UpdateBoardingUI();

        if (_boardingTimer <= 0f && IsServer)
            Depart();
    }

    // ── ヘリ呼び出し（FlareGun から呼ばれる）─────────────────
    /// <summary>フレアが上空に発射されたときに呼び出す。</summary>
    public void CallHelicopter(Vector3 flareOrigin)
    {
        if (_helicopterCalled.Value) return;

        if (IsServer)
            StartHelicopterSequenceServerRpc(flareOrigin);
        else
            RequestCallServerRpc(flareOrigin);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestCallServerRpc(Vector3 origin) => StartHelicopterSequenceServerRpc(origin);

    [Rpc(SendTo.Server)]
    private void StartHelicopterSequenceServerRpc(Vector3 origin)
    {
        if (_helicopterCalled.Value) return;
        _helicopterCalled.Value = true;

        // ヘリパッドを発射地点の近くに設定（フラットな地面を探す）
        _helipadPosition = FindHelipad(origin);

        AnnounceHelicopterClientRpc(_helipadPosition);
        StartCoroutine(ArrivalCoroutine());
        Debug.Log($"[Heli] ヘリコプター呼び出し！{ARRIVAL_DELAY}秒後に到着。ヘリパッド: {_helipadPosition}");
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void AnnounceHelicopterClientRpc(Vector3 helipadPos)
    {
        _helipadPosition = helipadPos;

        // GDD §2.4 — 全クライアントで同期されたカウントダウン開始。
        // ARRIVAL_DELAY は固定秒数でサーバー側のシーケンスと完全に並走するため、
        // NetworkVariable ではなく各クライアント独立のタイマーで十分一致する。
        _isArriving   = true;
        _arrivalTimer = ARRIVAL_DELAY;
        if (_arrivalTimerText != null)
            _arrivalTimerText.gameObject.SetActive(true);

        Debug.Log("[Heli] ヘリが呼ばれました！60秒後に到着します。");
    }

    // ── 到着シーケンス ────────────────────────────────────────
    private IEnumerator ArrivalCoroutine()
    {
        yield return new WaitForSeconds(ARRIVAL_DELAY - 20f);  // 20秒前からローター音（将来の Audio 実装用）
        NotifyApproachClientRpc();

        yield return new WaitForSeconds(20f);

        // ヘリ本体スポーン
        SpawnHelicopterClientRpc(_helipadPosition);
        yield return new WaitForSeconds(3f);   // 着陸演出

        StartBoardingPhaseClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void NotifyApproachClientRpc()
    {
        // GDD §15.2 — heli_approach（接近時の遠くのローター音）
        PPAudioManager.Instance?.PlaySE(SoundId.HeliApproach, _helipadPosition);

        Debug.Log("[Heli] ヘリのローター音が聞こえてきた！あと20秒！");
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SpawnHelicopterClientRpc(Vector3 helipadPos)
    {
        _helipadPosition = helipadPos;
        Vector3 startPos = helipadPos + Vector3.up * APPROACH_ALTITUDE;
        Vector3 hoverPos = helipadPos + Vector3.up * HOVER_ALTITUDE;

        // ヘリビジュアルを生成
        if (_helicopterMesh != null)
            _heliVisual = Instantiate(_helicopterMesh, startPos, Quaternion.identity);
        else
            _heliVisual = CreatePlaceholderHeli(startPos);

        _isActive = true;
        StartCoroutine(LandingAnimation(startPos, hoverPos));

        // GDD §15.2 — heli_hover（ホバリング時の近距離ローター音）
        PPAudioManager.Instance?.PlaySE(SoundId.HeliHover, hoverPos);

        Debug.Log("[Heli] ヘリが到着！");
    }

    private IEnumerator LandingAnimation(Vector3 start, Vector3 target)
    {
        float t = 0f;
        const float duration = 3f;
        while (t < duration)
        {
            t += Time.deltaTime;
            if (_heliVisual != null)
                _heliVisual.transform.position = Vector3.Lerp(start, target, t / duration);
            yield return null;
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void StartBoardingPhaseClientRpc()
    {
        _isBoarding    = true;
        _boardingTimer = BOARDING_WINDOW;
        if (_boardingTimerText != null)
            _boardingTimerText.gameObject.SetActive(true);
        Debug.Log($"[Heli] 搭乗フェーズ開始！{BOARDING_WINDOW}秒以内に乗り込め！");
    }

    // ── 搭乗操作 ─────────────────────────────────────────────
    /// <summary>プレイヤーが E キーでヘリに搭乗する。PlayerInteraction から呼ぶ。</summary>
    public void BoardPlayer(PlayerHealthSystem player)
    {
        if (!_isBoarding) return;

        int id = player.GetInstanceID();
        if (_boardedPlayerIds.Contains(id)) return;

        _boardedPlayerIds.Add(id);
        Debug.Log($"[Heli] {player.name} が搭乗！({_boardedPlayerIds.Count}人)");

        // 全員搭乗チェック（サーバー権威）
        if (IsServer && AllSurvivorsBoarded())
            Depart();
    }

    private bool AllSurvivorsBoarded()
    {
        if (PlayerHealthSystem.RegisteredPlayers == null) return true;
        foreach (var p in PlayerHealthSystem.RegisteredPlayers)
        {
            if (p.IsDead) continue;
            if (!_boardedPlayerIds.Contains(p.GetInstanceID())) return false;
        }
        return true;
    }

    // ── ヘリ離陸 ─────────────────────────────────────────────
    private void Depart()
    {
        if (!IsServer) return;

        _isBoarding = false;
        DepartClientRpc();

        bool allSurvived = AllSurvivorsBoarded();
        GameServices.Expedition?.ReturnToBase(allSurvived);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void DepartClientRpc()
    {
        _isBoarding = false;
        if (_boardingTimerText != null)
            _boardingTimerText.gameObject.SetActive(false);

        StartCoroutine(DepartureAnimation());

        // GDD §15.2 — heli_depart（離陸時の上昇ローター音）
        Vector3 seOrigin = _heliVisual != null ? _heliVisual.transform.position : _helipadPosition;
        PPAudioManager.Instance?.PlaySE(SoundId.HeliDepart, seOrigin);

        Debug.Log("[Heli] ヘリが離陸！");
    }

    private IEnumerator DepartureAnimation()
    {
        if (_heliVisual == null) yield break;

        Vector3 start = _heliVisual.transform.position;
        Vector3 end   = start + Vector3.up * 80f;
        float t = 0f;
        const float duration = 5f;

        while (t < duration)
        {
            t += Time.deltaTime;
            _heliVisual.transform.position = Vector3.Lerp(start, end, t / duration);
            yield return null;
        }

        Destroy(_heliVisual);
    }

    // ── ダウンウォッシュ ─────────────────────────────────────
    private void ApplyDowndraft()
    {
        if (_heliVisual == null) return;

        Vector3 center = _heliVisual.transform.position;
        Collider[] hits = Physics.OverlapSphere(center, DOWNDRAFT_RADIUS);

        foreach (var hit in hits)
        {
            var rb = hit.attachedRigidbody;
            if (rb == null || rb.isKinematic) continue;

            // 遺物・プレイヤーのみに適用
            if (hit.GetComponentInParent<RelicBase>() != null ||
                hit.GetComponentInParent<PlayerHealthSystem>() != null)
            {
                float dist = Vector3.Distance(center, hit.transform.position);
                float falloff = 1f - (dist / DOWNDRAFT_RADIUS);
                rb.AddForce(Vector3.down * (DOWNDRAFT_FORCE * falloff), ForceMode.Force);
            }
        }
    }

    // ── UI 更新 ───────────────────────────────────────────────
    private void UpdateBoardingUI()
    {
        if (_boardingTimerText == null) return;
        int s = Mathf.CeilToInt(_boardingTimer);
        _boardingTimerText.text = $"搭乗猶予: {s}秒";
        _boardingTimerText.color = s <= 10 ? Color.red : Color.white;
    }

    private void UpdateArrivalUI()
    {
        if (_arrivalTimerText == null) return;
        int s = Mathf.CeilToInt(_arrivalTimer);
        _arrivalTimerText.text = $"ヘリ到着まで: {s}秒";
        _arrivalTimerText.color = s <= 20 ? new Color(1f, 0.8f, 0.2f) : Color.white;
    }

    // ── ヘリパッド検索 ────────────────────────────────────────
    private static Vector3 FindHelipad(Vector3 origin)
    {
        if (Physics.Raycast(origin + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 50f))
            return hit.point + Vector3.up * 0.5f;
        return origin;
    }

    // ── プレースホルダー生成 ────────────────────────────────
    private static GameObject CreatePlaceholderHeli(Vector3 pos)
    {
        var root   = new GameObject("Helicopter_Placeholder");
        root.transform.position = pos;

        var body   = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(root.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale    = new Vector3(2f, 0.6f, 4f);
        Object.Destroy(body.GetComponent<Collider>());

        // 黄色マテリアル
        var rend = body.GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1f, 0.9f, 0f));
            rend.sharedMaterial = mat;
        }

        return root;
    }

    // ── Gizmos ───────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        if (!_isActive) return;
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.3f);
        Gizmos.DrawSphere(_helipadPosition + Vector3.up * HOVER_ALTITUDE, DOWNDRAFT_RADIUS);
    }
}
