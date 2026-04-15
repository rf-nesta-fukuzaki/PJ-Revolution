using UnityEngine;

/// <summary>
/// GDD §3.2 — プレイヤーのインタラクション入力処理。
/// カメラ前方を Raycast で走査し、遺物・アイテムを E / F / G で操作する。
///   E: 遺物を拾う / 置く。担架への乗り込み / 離脱も兼ねる
///   F: 持っているアイテムを使用
///   G: 保持中の遺物を前方に投げる（ドロップ）
/// 死亡時には保持遺物を自動ドロップし担架から離脱する（GDD §4.1）。
///
/// 担架（StretcherItem）2人操作フロー:
///   1人目: 担架に近づいて E → 端Aに吸着、担架を共同運搬状態に
///   2人目: 担架に近づいて E → 端Bに吸着、Is CarriedByTwo = true
///   離脱: 再び E → Detach()
/// </summary>
[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(PlayerHealthSystem))]
public class PlayerInteraction : MonoBehaviour
{
    private const float INTERACT_RANGE    = 2.5f;   // インタラクト可能距離
    private const float DROP_IMPULSE      = 4f;     // ドロップ時の初速 (m/s)
    private const float STRETCHER_RANGE   = 2.0f;   // 担架に乗り込める距離

    [Header("インタラクション設定")]
    [SerializeField] private float     _interactRange  = INTERACT_RANGE;
    [SerializeField] private Transform _cameraTransform;  // 未設定なら子 Camera → Camera.main の順で取得

    private PlayerInventory    _inventory;
    private PlayerHealthSystem _health;
    private RelicCarrier       _carriedRelic;
    private StretcherItem      _attachedStretcher;   // 現在乗り込んでいる担架

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        _inventory = GetComponent<PlayerInventory>();
        _health    = GetComponent<PlayerHealthSystem>();
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

    // ── E: 遺物を拾う / 置く / 担架へ乗り込む ────────────────
    private void HandleInteract()
    {
        // ── 担架に乗り込んでいる場合 → 離脱 ──
        if (_attachedStretcher != null)
        {
            DetachFromStretcher();
            return;
        }

        // ── 保持中の遺物がある → 置く ──
        if (_carriedRelic != null)
        {
            PutDownRelic();
            return;
        }

        // ── 前方に担架があれば乗り込む（遺物より優先） ──
        var stretcher = RaycastForComponent<StretcherItem>(STRETCHER_RANGE);
        if (stretcher != null)
        {
            TryAttachToStretcher(stretcher);
            return;
        }

        // ── 前方に遺物キャリアがあれば拾う ──
        var carrier = RaycastForComponent<RelicCarrier>(_interactRange);
        if (carrier != null)
        {
            PickUpRelic(carrier);
            return;
        }

        // ── 前方にアイテムがあればインベントリへ追加 ──
        var item = RaycastForComponent<ItemBase>(_interactRange);
        if (item != null)
            TryAddItem(item);
    }

    // ── 担架乗り込み ─────────────────────────────────────────
    private void TryAttachToStretcher(StretcherItem stretcher)
    {
        if (stretcher.TryAttach(this, out Transform attachPoint))
        {
            _attachedStretcher = stretcher;
            Debug.Log($"[Interaction] {name} が担架に乗り込んだ → {attachPoint?.name}");
        }
        else
        {
            Debug.Log("[Interaction] 担架が満員です（2人まで）");
        }
    }

    // ── 担架離脱 ─────────────────────────────────────────────
    private void DetachFromStretcher()
    {
        _attachedStretcher.Detach(this);
        _attachedStretcher = null;
        Debug.Log("[Interaction] 担架から離脱");
    }

    // ── F: アイテム使用 ─────────────────────────────────────
    private void HandleUse()
    {
        foreach (var item in _inventory.Items)
        {
            if (item.TryUse())
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
        Debug.Log("[Interaction] 遺物をドロップ");
    }

    // ── 遺物を拾う ────────────────────────────────────────────
    private void PickUpRelic(RelicCarrier carrier)
    {
        carrier.PickUp(transform, GetInstanceID());
        _carriedRelic = carrier;
        ScoreTracker.Instance?.RecordRelicFound(GetInstanceID());
        Debug.Log($"[Interaction] {carrier.name} を拾った");
    }

    // ── 遺物を置く ────────────────────────────────────────────
    private void PutDownRelic()
    {
        _carriedRelic.PutDown();
        _carriedRelic = null;
        Debug.Log("[Interaction] 遺物を置いた");
    }

    // ── アイテムをインベントリへ追加 ─────────────────────────
    private void TryAddItem(ItemBase item)
    {
        if (_inventory.TryAdd(item))
            Debug.Log($"[Interaction] {item.ItemName} を拾った");
        else
            Debug.Log("[Interaction] インベントリが満杯または重量超過");
    }

    // ── 死亡時：保持遺物を自動ドロップ＆担架から離脱（GDD §4.1）
    private void OnPlayerDied(PlayerHealthSystem _)
    {
        if (_attachedStretcher != null)
        {
            _attachedStretcher.Detach(this);
            _attachedStretcher = null;
        }

        if (_carriedRelic == null) return;
        _carriedRelic.Drop(Vector3.zero);
        _carriedRelic = null;
        Debug.Log("[Interaction] 死亡により遺物をドロップ・担架から離脱");
    }

    // ── Raycast ヘルパー ─────────────────────────────────────
    private T RaycastForComponent<T>(float range) where T : Component
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
        Gizmos.DrawRay(_cameraTransform.position,
                       _cameraTransform.forward * _interactRange);

        Gizmos.color = new Color(0f, 1f, 0.5f, 0.6f);
        Gizmos.DrawRay(_cameraTransform.position,
                       _cameraTransform.forward * STRETCHER_RANGE);
    }
}
