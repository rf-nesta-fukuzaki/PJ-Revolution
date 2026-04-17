using System.Collections;
using Unity.Netcode;
using UnityEngine;

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

    [Header("祠で復活")]
    [SerializeField] private float _reviveDetectRadius = 3f;

    // ── コンポーネント ────────────────────────────────────────
    private Rigidbody           _rb;
    private Collider[]          _colliders;
    private Renderer[]          _renderers;
    private PlayerHealthSystem  _health;
    private PlayerStateMachine  _stateMachine;

    // ── ローカル状態 ─────────────────────────────────────────
    private bool          _hasRevived;
    private Material[][]  _cachedMaterials;

    // ── 後方互換プロパティ（既存コードの IsGhost 参照を維持）──
    public bool IsGhost           => _stateMachine != null && _stateMachine.IsGhost;
    public int  GhostContributions { get; private set; }

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        _rb           = GetComponent<Rigidbody>();
        _colliders    = GetComponentsInChildren<Collider>();
        _renderers    = GetComponentsInChildren<Renderer>();
        _health       = GetComponent<PlayerHealthSystem>();
        _stateMachine = GetComponent<PlayerStateMachine>();

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

        if (nowGhost)
        {
            _rb.isKinematic = true;
            _rb.useGravity  = false;
            foreach (var col in _colliders) col.enabled = false;
            StartCoroutine(ScanForShrines());
        }
        else
        {
            _rb.isKinematic = false;
            _rb.useGravity  = true;
            foreach (var col in _colliders) col.enabled = true;
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
    }

    private void HandleGhostMovement()
    {
        Vector2 moveInput = InputStateReader.ReadMoveVectorRaw();
        Vector3 input = new Vector3(moveInput.x, 0f, moveInput.y);
        if (InputStateReader.IsAscendPressed())  input.y += 1f;
        if (InputStateReader.IsDescendPressed()) input.y -= 1f;

        transform.position += input.normalized * (_ghostMoveSpeed * Time.deltaTime);

        // 地面から一定高さを維持
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 10f))
        {
            float targetY = hit.point.y + _ghostFloatHeight;
            if (transform.position.y < targetY)
                transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
        }
    }

    // ── 祠スキャン（復活） ────────────────────────────────────
    private IEnumerator ScanForShrines()
    {
        while (IsGhost && !_hasRevived)
        {
            yield return new WaitForSeconds(0.5f);

            foreach (var shrine in ReviveShrine.RegisteredShrines)
            {
                if (!shrine.IsAvailable) continue;
                if (Vector3.Distance(transform.position, shrine.transform.position) > _reviveDetectRadius) continue;

                Revive(shrine);
                yield break;
            }
        }
    }

    // ── 復活 ─────────────────────────────────────────────────
    private void Revive(ReviveShrine shrine)
    {
        shrine.Use();
        _hasRevived = true;

        _stateMachine.Transition(PlayerState.Alive);
        _health.Heal(50f);
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
