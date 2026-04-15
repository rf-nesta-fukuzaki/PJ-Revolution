using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// GDD §4.2 — 偵察幽霊システム（NetworkBehaviour 版）。
/// 死亡したプレイヤーは透明な幽霊になる。
/// - 物理的な干渉は不可
/// - ピン（マーカー）を打てる（全クライアントに同期）
/// - ボイチャで指示できる（プロキシミティ適用）
/// - 山に散らばる「祠」を見つけると1回限り復活
/// </summary>
[RequireComponent(typeof(PlayerHealthSystem))]
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

    [Header("ピン")]
    [SerializeField] private int _maxPins = 5;

    [Header("祠で復活")]
    [SerializeField] private float _reviveDetectRadius = 3f;

    // ── コンポーネント ────────────────────────────────────────
    private Rigidbody          _rb;
    private Collider[]         _colliders;
    private Renderer[]         _renderers;
    private PlayerHealthSystem _health;

    // ── ネットワーク状態 ─────────────────────────────────────
    // 全クライアントで幽霊状態を共有。Write 権限は Server のみ。
    private readonly NetworkVariable<bool> _networkIsGhost = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ── ローカル状態 ─────────────────────────────────────────
    private bool       _hasRevived;
    private int        _pinsLeft;
    private readonly List<GameObject> _pins = new();
    private static Material s_pinMaterial;

    private Material[][] _cachedMaterials;

    public bool IsGhost          => _networkIsGhost.Value;
    public int  PinsLeft         => _pinsLeft;
    public int  GhostContributions { get; private set; }

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        _rb        = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>();
        _renderers = GetComponentsInChildren<Renderer>();
        _health    = GetComponent<PlayerHealthSystem>();

        // マテリアルインスタンスを一度だけ生成してキャッシュする
        // r.materials は毎回配列を生成するため、ここで一括処理
        _cachedMaterials = new Material[_renderers.Length][];
        for (int i = 0; i < _renderers.Length; i++)
            _cachedMaterials[i] = _renderers[i].materials;
    }

    public override void OnNetworkSpawn()
    {
        _networkIsGhost.OnValueChanged += OnGhostStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        _networkIsGhost.OnValueChanged -= OnGhostStateChanged;
    }

    // NetworkVariable の変化を受けて全クライアントのビジュアルを更新
    private void OnGhostStateChanged(bool _, bool isGhost)
    {
        SetGhostVisuals(isGhost);
    }

    // ── 幽霊モード移行 ───────────────────────────────────────
    public void EnterGhostMode()
    {
        if (IsGhost) return;

        _pinsLeft = _maxPins;

        // 物理無効化（ローカル）
        _rb.isKinematic = true;
        _rb.useGravity  = false;
        foreach (var col in _colliders)
            col.enabled = false;

        // ホスト権威で状態を更新（クライアントは ServerRpc 経由）
        if (IsServer)
            _networkIsGhost.Value = true;
        else
            SetGhostStateServerRpc();

        Debug.Log("[Ghost] 幽霊モードに移行。偵察任務を遂行せよ！");
        StartCoroutine(ScanForShrines());
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SetGhostStateServerRpc() => _networkIsGhost.Value = true;

    // ── 入力処理（オーナーのみ） ─────────────────────────────
    private void Update()
    {
        if (!IsOwner || !IsGhost) return;

        HandleGhostMovement();
        HandlePinInput();
    }

    private void HandleGhostMovement()
    {
        Vector2 moveInput = InputStateReader.ReadMoveVectorRaw();
        Vector3 input = new Vector3(moveInput.x, 0f, moveInput.y);
        if (InputStateReader.IsAscendPressed()) input.y += 1f;
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

    // ── ピン ─────────────────────────────────────────────────
    private void HandlePinInput()
    {
        if (!InputStateReader.PrimaryPointerPressedThisFrame()) return;
        PlacePin();
    }

    public void PlacePin()
    {
        if (_pinsLeft <= 0)
        {
            Debug.Log("[Ghost] ピン使用回数上限");
            return;
        }

        Vector3 pinPos = transform.position + transform.forward * 5f;
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, 20f))
            pinPos = hit.point + Vector3.up * 0.5f;

        _pinsLeft--;
        GhostContributions++;
        ScoreTracker.Instance?.RecordGhostPin((int)OwnerClientId);

        // ホスト経由で全クライアントにピンをスポーン
        PlacePinServerRpc(pinPos);
        Debug.Log($"[Ghost] ピン設置 残り: {_pinsLeft}");
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PlacePinServerRpc(Vector3 position)
    {
        SpawnPinClientRpc(position);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SpawnPinClientRpc(Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.transform.position   = position;
        go.transform.localScale = new Vector3(0.1f, 0.5f, 0.1f);

        // URP マテリアル適用
        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.sharedMaterial = GetPinMaterial();
        }

        Destroy(go.GetComponent<Collider>());
        _pins.Add(go);
        Destroy(go, 30f);
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

    private static Material GetPinMaterial()
    {
        if (s_pinMaterial != null) return s_pinMaterial;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) return null;

        s_pinMaterial = new Material(shader);
        if (s_pinMaterial.HasProperty(BaseColorId))
            s_pinMaterial.SetColor(BaseColorId, Color.cyan);
        return s_pinMaterial;
    }

    // ── 復活 ─────────────────────────────────────────────────
    private void Revive(ReviveShrine shrine)
    {
        shrine.Use();
        _hasRevived = true;

        _rb.isKinematic = false;
        _rb.useGravity  = true;
        foreach (var col in _colliders)
            col.enabled = true;

        if (IsServer)
            _networkIsGhost.Value = false;
        else
            ClearGhostStateServerRpc();

        _health.Heal(50f);
        Debug.Log("[Ghost] 祠で復活！");
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ClearGhostStateServerRpc() => _networkIsGhost.Value = false;

    // ── ビジュアル ────────────────────────────────────────────
    private void SetGhostVisuals(bool ghost)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;

            // キャッシュ済みの Material インスタンスを使用（毎回配列生成なし）
            foreach (var mat in _cachedMaterials[i])
            {
                if (mat == null) continue;

                if (ghost)
                {
                    // URP Lit: Surface Type = Transparent (Alpha blend)
                    mat.SetFloat(SurfaceId, 1f);   // cached PropertyID
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

                // _BaseColor の alpha を変更（cached PropertyID）
                Color c = mat.GetColor(BaseColorId);
                c.a = ghost ? _ghostAlpha : 1f;
                mat.SetColor(BaseColorId, c);
            }
        }
    }
}
