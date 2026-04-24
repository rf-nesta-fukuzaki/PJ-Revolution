using UnityEngine;

/// <summary>
/// GDD §3.2 — プレイヤーのインタラクション入力処理。
/// カメラ前方を Raycast で走査し、E / F / G で操作する。
///   E: 遺物を拾う / 置く。担架への乗り込み / 離脱。ヘリ搭乗。
///   F: 持っているアイテムを使用
///   G: 保持中の遺物を前方に投げる（ドロップ）
/// 死亡時には保持遺物を自動ドロップし担架から離脱する（GDD §4.1）。
/// </summary>
[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(PlayerHealthSystem))]
public class PlayerInteraction : MonoBehaviour
{
    private const float INTERACT_RANGE      = 2.5f;   // インタラクト可能距離
    private const float DROP_IMPULSE        = 4f;     // ドロップ時の初速 (m/s)
    private const float STRETCHER_RANGE     = 2.0f;   // 担架に乗り込める距離
    private const float HELICOPTER_RANGE    = 8f;     // ヘリ搭乗可能距離（m）

    [Header("インタラクション設定")]
    [SerializeField] private float     _interactRange  = INTERACT_RANGE;
    [SerializeField] private Transform _cameraTransform;

    // ── 依存コンポーネント ────────────────────────────────────
    private PlayerInventory    _inventory;
    private PlayerHealthSystem _health;
    private RelicCarrier       _carriedRelic;
    private StretcherItem      _attachedStretcher;
    private BalanceIndicator   _balanceIndicator;

    // ── スコアサービス（Singleton 直結を排除） ────────────────
    private IScoreService ScoreService => GameServices.Score;

    /// <summary>
    /// 現在遺物を持ち運んでいるか（GDD §6.2 カメラモード「運搬」判定用）。
    /// </summary>
    public bool IsCarryingRelic => _carriedRelic != null;

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        _inventory        = GetComponent<PlayerInventory>();
        _health           = GetComponent<PlayerHealthSystem>();
        _balanceIndicator = GetComponentInChildren<BalanceIndicator>();
        if (_cameraTransform == null)
            _cameraTransform = GetComponentInChildren<Camera>()?.transform ?? transform;
    }

    private void OnEnable()  => _health.OnDied += OnPlayerDied;
    private void OnDisable() => _health.OnDied -= OnPlayerDied;

    private void Update()
    {
        if (_health.IsDead) return;

        if (InputStateReader.InteractPressedThisFrame()) HandleInteract();
        if (InputStateReader.UsePressedThisFrame())      HandleUse();
        if (InputStateReader.DropPressedThisFrame())     HandleDrop();
    }

    // ── E: インタラクト ──────────────────────────────────────
    private void HandleInteract()
    {
        if (TryBoardHelicopter()) return;
        if (TryDetachStretcher()) return;
        if (TryPutDownRelic())    return;
        if (TryAttachStretcher()) return;
        if (TryPickUpRelic())     return;
        TryPickUpItem();
    }

    // ── インタラクト分割メソッド ──────────────────────────────
    private bool TryBoardHelicopter()
    {
        // IHelicopterService 経由でアクセス（GameServices で解決）
        var heli = GameServices.Helicopter;
        if (heli == null || !heli.IsBoarding) return false;
        if (Vector3.Distance(transform.position, heli.HelipadPosition) > HELICOPTER_RANGE) return false;

        heli.BoardPlayer(_health);
        return true;
    }

    private bool TryDetachStretcher()
    {
        if (_attachedStretcher == null) return false;
        _attachedStretcher.Detach(this);
        _attachedStretcher = null;
        Debug.Log("[Interaction] 担架から離脱");
        return true;
    }

    private bool TryPutDownRelic()
    {
        if (_carriedRelic == null) return false;
        _carriedRelic.PutDown();
        _carriedRelic = null;
        _balanceIndicator?.Hide();
        Debug.Log("[Interaction] 遺物を置いた");
        return true;
    }

    private bool TryAttachStretcher()
    {
        var stretcher = RaycastFor<StretcherItem>(STRETCHER_RANGE);
        if (stretcher == null) return false;

        if (stretcher.TryAttach(this, out Transform attachPoint))
        {
            _attachedStretcher = stretcher;
            Debug.Log($"[Interaction] {name} が担架に乗り込んだ → {attachPoint?.name}");
        }
        else
        {
            Debug.Log("[Interaction] 担架が満員です（2人まで）");
        }
        return true;
    }

    private bool TryPickUpRelic()
    {
        var carrier = RaycastFor<RelicCarrier>(_interactRange);
        if (carrier == null) return false;

        carrier.PickUp(transform, GetInstanceID());
        _carriedRelic = carrier;
        _balanceIndicator?.Show(carrier);
        ScoreService?.RecordRelicFound(GetInstanceID());
        Debug.Log($"[Interaction] {carrier.name} を拾った");
        return true;
    }

    private void TryPickUpItem()
    {
        var item = RaycastFor<ItemBase>(_interactRange);
        if (item == null) return;

        if (_inventory.TryAdd(item))
            Debug.Log($"[Interaction] {item.ItemName} を拾った");
        else
            Debug.Log("[Interaction] インベントリが満杯または重量超過");
    }

    // ── F: アイテム使用 ─────────────────────────────────────
    private void HandleUse()
    {
        // GDD §5.2 — アイテム毎に必要な引数が違うので、既定の TryUse() ではなく
        // 専用 API に型別ディスパッチする。既定の TryUse() に頼ると
        //   - 照準/座標を必要とするアイテム（アンカー/フレア/グラップリング/テント）は空撃ち
        //   - 運搬中の遺物を対象とするアイテム（梱包キット/サーマルケース）は対象不明
        // になるため、ここで正しいパラメータを供給する。
        int playerId = GetInstanceID();
        foreach (var item in _inventory.Items)
        {
            bool used = item switch
            {
                AnchorBoltItem    anchor  => anchor.TryPlaceAnchor(_cameraTransform, playerId),
                FlareGunItem      flare   => flare.TryFire(_cameraTransform),
                GrapplingHookItem hook    => hook.Fire(_cameraTransform.position, _cameraTransform.forward),
                BivouacTentItem   tent    => tent.TryPlace(transform.position, transform.rotation),
                ThermalCaseItem   thermal => _carriedRelic != null
                                              && thermal.TryProtectRelic(_carriedRelic.GetComponent<RelicBase>()),
                PackingKitItem    packing => _carriedRelic != null
                                              && packing.ApplyToRelic(_carriedRelic.GetComponent<RelicBase>()),
                _                         => item.TryUse()
            };

            if (used)
            {
                Debug.Log($"[Interaction] {item.ItemName} を使用");
                return;
            }
        }
    }

    // ── G: 遺物をドロップ ────────────────────────────────────
    private void HandleDrop()
    {
        if (_carriedRelic == null) return;
        _carriedRelic.Drop(_cameraTransform.forward * DROP_IMPULSE);
        _carriedRelic = null;
        _balanceIndicator?.Hide();
        Debug.Log("[Interaction] 遺物をドロップ");
    }

    // ── 死亡時：保持遺物を自動ドロップ＆担架から離脱（GDD §4.1）
    private void OnPlayerDied(PlayerHealthSystem _)
    {
        _attachedStretcher?.Detach(this);
        _attachedStretcher = null;

        if (_carriedRelic != null)
        {
            _carriedRelic.Drop(Vector3.zero);
            _carriedRelic = null;
            _balanceIndicator?.Hide();
        }

        Debug.Log("[Interaction] 死亡により遺物をドロップ・担架から離脱");
    }

    // ── Raycast ヘルパー ─────────────────────────────────────
    private T RaycastFor<T>(float range) where T : Component
    {
        if (_cameraTransform == null) return null;
        var ray = new Ray(_cameraTransform.position, _cameraTransform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, range)) return null;
        return hit.collider.GetComponentInParent<T>();
    }

    private void OnDrawGizmosSelected()
    {
        if (_cameraTransform == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(_cameraTransform.position, _cameraTransform.forward * _interactRange);
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.6f);
        Gizmos.DrawRay(_cameraTransform.position, _cameraTransform.forward * STRETCHER_RANGE);
    }
}
