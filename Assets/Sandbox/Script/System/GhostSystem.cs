using System.Collections;
using Unity.Netcode;
using UnityEngine;
using PeakPlunder.Audio;

/// <summary>
/// GDD §4.2 — 偵察幽霊システム（NetworkBehaviour 版）。
/// 死亡したプレイヤーは透明な幽霊になる。
/// - 物理的な干渉は不可
/// - ピン（マーカー）を打てる（全クライアントに同期）
/// - ボイチャで指示できる（プロキシミティ適用）
/// - 山に散らばる「祠」を見つけると1回限り復活
///
/// ステート管理は PlayerStateMachine に委譲。
/// このクラスは「幽霊ビジュアル」と「幽霊移動」の責任のみを持つ。
/// </summary>
[RequireComponent(typeof(PlayerHealthSystem))]
[RequireComponent(typeof(PlayerStateMachine))]
public class GhostSystem : NetworkBehaviour
{
    // Shader property IDs — キャッシュして文字列検索コストをゼロに
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int SurfaceId   = Shader.PropertyToID("_Surface");
    private static readonly int BlendId     = Shader.PropertyToID("_Blend");

    [Header("幽霊設定")]
    [SerializeField] private float _ghostMoveSpeed   = 8f;
    [SerializeField] private float _ghostFloatHeight = 2f;
    [SerializeField] private float _ghostAlpha       = 0.3f;
    [SerializeField] private Transform _cameraTransform;

    [Header("祠で復活")]
    [SerializeField] private float _reviveDetectRadius = 3f;
    [Tooltip("祠の前で E を保持してチャネリングする時間 (GDD §7.3)")]
    [SerializeField] private float _reviveChannelSeconds = 3f;

    // チャネリング状態（HUD 進捗バー用に公開・GDD §14.4）。
    public bool  IsChannelingRevive => _channelProgress > 0f;
    public float ReviveChannelProgress01 => _reviveChannelSeconds > 0f
        ? Mathf.Clamp01(_channelProgress / _reviveChannelSeconds) : 0f;
    private float _channelProgress;
    private ReviveShrine _channelShrine;

    [Header("移動範囲制限 (GDD §7.3)")]
    [Tooltip("生存者からこの距離を超えると外向き移動が減速し始める (m)")]
    [SerializeField] private float _leashSoftRadius = 180f;
    [Tooltip("生存者からこの距離で外向き移動が完全停止する (m)")]
    [SerializeField] private float _leashMaxRadius  = 200f;
    [Tooltip("ソフト境界(180m)での外向き最大速度 (m/s)。ここから 0(200m) へ線形減衰。")]
    [SerializeField] private float _leashBoundarySpeed = 6f;

    // 範囲制限の警告状態（HUD が購読可能・GDD §7.3「範囲制限に近づいています」赤点滅用）。
    public bool IsNearLeashBoundary { get; private set; }

    // ── コンポーネント ────────────────────────────────────────
    private Rigidbody           _rb;
    private Collider[]          _colliders;
    private Renderer[]          _renderers;
    private Animator            _animator;
    private PlayerHealthSystem  _health;
    private PlayerStateMachine  _stateMachine;

    private static readonly int IsGhostHash = Animator.StringToHash("IsGhost");

    // ── ローカル状態 ─────────────────────────────────────────
    private bool          _hasRevived;
    private Material[][]  _cachedMaterials;

    // ── 後方互換プロパティ（既存コードの IsGhost 参照を維持）──
    public bool IsGhost => _stateMachine != null && _stateMachine.IsGhost;

    /// <summary>このプレイヤーが既に祠で復活したか（GDD §7.3 — 復活は1人1回のみ）。全滅判定が参照する。</summary>
    public bool HasRevived => _hasRevived;

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        _rb           = GetComponent<Rigidbody>();
        _colliders    = GetComponentsInChildren<Collider>();
        _renderers    = GetComponentsInChildren<Renderer>();
        _animator     = GetComponentInChildren<Animator>();
        _health       = GetComponent<PlayerHealthSystem>();
        _stateMachine = GetComponent<PlayerStateMachine>();
        if (_cameraTransform == null)
            _cameraTransform = GetComponentInChildren<Camera>()?.transform;

        Debug.Assert(_stateMachine != null,
            "[GhostSystem] PlayerStateMachine が同一 GameObject に見つかりません");

        // マテリアルインスタンスを一度だけ生成してキャッシュする
        _cachedMaterials = new Material[_renderers.Length][];
        for (int i = 0; i < _renderers.Length; i++)
            _cachedMaterials[i] = _renderers[i].materials;
    }

    public override void OnNetworkSpawn()
    {
        _stateMachine.OnStateChanged += OnPlayerStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        _stateMachine.OnStateChanged -= OnPlayerStateChanged;
    }

    // PlayerStateMachine のステート変化を受けてビジュアルを更新
    private void OnPlayerStateChanged(PlayerState prev, PlayerState next)
    {
        bool nowGhost = next == PlayerState.Ghost;
        bool wasGhost = prev == PlayerState.Ghost;

        if (nowGhost == wasGhost) return;

        SetGhostVisuals(nowGhost);

        // Animator: IsGhost (GDD §16.2)
        _animator?.SetBool(IsGhostHash, nowGhost);

        if (nowGhost)
        {
            _rb.isKinematic = true;
            _rb.useGravity  = false;
            foreach (var col in _colliders) col.enabled = false;
        }
        else
        {
            _rb.isKinematic = false;
            _rb.useGravity  = true;
            foreach (var col in _colliders) col.enabled = true;
            _channelProgress = 0f;
            _channelShrine   = null;
        }
    }

    // ── 幽霊モード移行 ───────────────────────────────────────
    /// <summary>
    /// プレイヤー死亡時に PlayerHealthSystem から呼ばれる。
    /// ステート遷移は PlayerStateMachine に委譲。
    /// </summary>
    public void EnterGhostMode()
    {
        if (IsGhost) return;

        _stateMachine.Transition(PlayerState.Ghost);
        Debug.Log("[Ghost] 幽霊モードに移行。偵察任務を遂行せよ！");
    }

    // ── 入力処理（オーナーのみ） ─────────────────────────────
    private void Update()
    {
        if (!IsOwner || !IsGhost) return;
        HandleGhostMovement();
        HandleReviveChanneling();
    }

    private void HandleGhostMovement()
    {
        Vector2 moveInput = InputStateReader.ReadMoveVectorRaw();
        bool verticalInput = InputStateReader.IsAscendPressed() || InputStateReader.IsDescendPressed();

        Transform cam = _cameraTransform != null ? _cameraTransform : Camera.main?.transform;
        Vector3 forward = cam != null ? cam.forward : transform.forward;
        Vector3 right   = cam != null ? cam.right   : transform.right;
        forward.y = 0f;
        right.y   = 0f;
        if (forward.sqrMagnitude > 0.001f) forward.Normalize();
        if (right.sqrMagnitude > 0.001f)   right.Normalize();

        Vector3 input = forward * moveInput.y + right * moveInput.x;
        if (InputStateReader.IsAscendPressed())  input.y += 1f;
        if (InputStateReader.IsDescendPressed()) input.y -= 1f;

        if (input.sqrMagnitude > 0.001f)
        {
            Vector3 move = input.normalized * (_ghostMoveSpeed * Time.deltaTime);
            move = ApplyLeash(move);                     // GDD §7.3 — 生存者から離れ過ぎないよう外向きを減速
            transform.position += move;
        }

        // 垂直入力が無いときだけ地面から浮遊高さを維持（GDD §7.3 偵察幽霊）
        if (!verticalInput && Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 10f))
        {
            float targetY = hit.point.y + _ghostFloatHeight;
            if (transform.position.y < targetY)
                transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
        }
    }

    /// <summary>
    /// GDD §7.3 — 生存者の最寄りプレイヤーから一定距離を超えると「外向き」移動を減速させる。
    /// 180m から減速開始、200m で外向き停止（見えない壁ではなく速度減衰）。内向き（近づく）は常に全速で、
    /// 境界で詰まないようにする。生存者が居なければ（最後の1人が幽霊）制限しない。
    /// </summary>
    private Vector3 ApplyLeash(Vector3 move)
    {
        IsNearLeashBoundary = false;
        if (!TryGetNearestSurvivor(out Vector3 anchor)) return move;

        float dist = Vector3.Distance(transform.position, anchor);
        if (dist <= _leashSoftRadius) return move;

        IsNearLeashBoundary = true;

        float newDist = Vector3.Distance(transform.position + move, anchor);
        if (newDist <= dist) return move;                // 内向き or 接線は許可

        // 外向き：180m(=境界速度) → 200m(=0) へ線形に許容速度を下げる。
        float t = Mathf.InverseLerp(_leashSoftRadius, _leashMaxRadius, dist);
        float outwardCap = Mathf.Lerp(_leashBoundarySpeed, 0f, t) * Time.deltaTime;
        float moveMag = move.magnitude;
        if (moveMag <= outwardCap || moveMag <= 0.0001f) return move;
        return move * (outwardCap / moveMag);
    }

    /// <summary>最寄りの生存（非死亡）プレイヤーの位置。自分自身・死亡者は除外。居なければ false。</summary>
    private bool TryGetNearestSurvivor(out Vector3 position)
    {
        position = Vector3.zero;
        float best = float.MaxValue;
        bool found = false;
        foreach (var p in PlayerHealthSystem.RegisteredPlayers)
        {
            if (p == null || p.IsDead) continue;
            if (p.gameObject == gameObject) continue;
            float d = Vector3.Distance(transform.position, p.transform.position);
            if (d < best) { best = d; position = p.transform.position; found = true; }
        }
        return found;
    }

    // ── 祠での復活チャネリング（GDD §7.3） ────────────────────
    /// <summary>
    /// 祠の前で E を 3 秒間保持して復活する（チャネリング）。範囲外へ出る・E を離す・別の祠へ移ると
    /// 中断（リトライ可）。復活は 1 人 1 回のみ。
    /// </summary>
    private void HandleReviveChanneling()
    {
        if (_hasRevived) return;

        ReviveShrine target = FindReviveShrineInRange();
        bool holding = InputStateReader.InteractHeld(_inputSlotForRevive);

        if (target != null && holding)
        {
            if (_channelShrine != target) { _channelShrine = target; _channelProgress = 0f; }
            _channelProgress += Time.deltaTime;
            if (_channelProgress >= _reviveChannelSeconds)
                Revive(target);
        }
        else
        {
            // 中断（GDD §7.3 — 中断可能・リトライ可）
            _channelProgress = 0f;
            _channelShrine   = null;
        }
    }

    // ローカル幽霊はスロット 0 入力で操作する（ネット幽霊もオーナーのみ Update が走る）。
    private const int _inputSlotForRevive = 0;

    /// <summary>復活検出範囲内にある最初の使用可能な祠。無ければ null。</summary>
    private ReviveShrine FindReviveShrineInRange()
    {
        foreach (var shrine in ReviveShrine.RegisteredShrines)
        {
            if (shrine == null || !shrine.IsAvailable) continue;
            if (Vector3.Distance(transform.position, shrine.transform.position) <= _reviveDetectRadius)
                return shrine;
        }
        return null;
    }

    // ── 復活 ─────────────────────────────────────────────────
    private void Revive(ReviveShrine shrine)
    {
        shrine.Use();
        _hasRevived = true;

        _stateMachine.Transition(PlayerState.Alive);
        // PlayerHealthSystem._isDead を解除して HP を部分回復（Heal は死亡中早期 return するため Revive を使用）
        _health.Revive(50f);

        // GDD §15.2 — shrine_revive（復活完了音。本人への 2D フィードバック）
        if (IsOwner)
            GameServices.Audio?.PlaySE2D(SoundId.ShrineRevive);

        Debug.Log("[Ghost] 祠で復活！");
    }

    // ── ビジュアル ────────────────────────────────────────────
    private void SetGhostVisuals(bool ghost)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;

            foreach (var mat in _cachedMaterials[i])
            {
                if (mat == null) continue;

                if (ghost)
                {
                    // URP Lit: Surface Type = Transparent (Alpha blend)
                    mat.SetFloat(SurfaceId, 1f);
                    mat.SetFloat(BlendId, 0f);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
                else
                {
                    // URP Lit: Surface Type = Opaque
                    mat.SetFloat(SurfaceId, 0f);
                    mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                }

                Color c = mat.GetColor(BaseColorId);
                c.a = ghost ? _ghostAlpha : 1f;
                mat.SetColor(BaseColorId, c);
            }
        }
    }
}
