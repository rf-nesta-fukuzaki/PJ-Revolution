using UnityEngine;
using PeakPlunder.Audio;

/// <summary>
/// GDD §5.2 — アイテム「アンカーボルト（×3）」
/// ロープ固定点。消耗品。3回使用で消失。
/// コスト 6pt / 重量 1 / スロット 1 / 耐久 100
/// </summary>
public class AnchorBoltItem : ItemBase
{
    [Header("アンカー設定")]
    [SerializeField] private int         _maxCharges    = 3;
    [SerializeField] private GameObject  _anchorPrefab;          // Inspector で設定（なければプリミティブ代替）
    [SerializeField] private float       _placeDistance = 2.5f;  // 設置可能距離

    private int _chargesLeft;

    // ── プロパティ ────────────────────────────────────────────
    public int ChargesLeft => _chargesLeft;

    protected override void Awake()
    {
        // フィールドを先に設定してから base.Awake() を呼ぶことで
        // _currentDurability = _maxDurability が正しい値で初期化される
        _itemName       = "アンカーボルト";
        _cost           = 6;
        _weight         = 1f;
        _slots          = 1;
        _maxDurability  = 100f;
        _impactDmgScale = 0.2f;

        base.Awake();

        _chargesLeft = _maxCharges;
    }

    /// <summary>プレイヤーの向いている方向の壁にアンカーを設置する。</summary>
    /// <param name="playerTransform">設置するプレイヤーの Transform。</param>
    /// <param name="playerId">スコア記録用のプレイヤー ID。</param>
    public bool TryPlaceAnchor(Transform playerTransform, int playerId)
    {
        if (_isBroken || _chargesLeft <= 0) return false;

        // 壁 Raycast
        if (!Physics.Raycast(playerTransform.position, playerTransform.forward,
                             out var hit, _placeDistance))
            return false;

        PlaceAnchorAt(hit.point, hit.normal, playerId);
        return true;
    }

    private void PlaceAnchorAt(Vector3 position, Vector3 normal, int playerId)
    {
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            var sync = NetworkWorldPlacementsSync.Instance
                ?? Object.FindFirstObjectByType<NetworkWorldPlacementsSync>()
                ?? (nm.IsServer ? NetworkWorldPlacementsSync.EnsureExists() : null);
            if (sync == null)
            {
                Debug.LogWarning("[AnchorBolt] NetworkWorldPlacementsSync 未準備 — 設置をスキップ");
                return;
            }

            sync.RequestPlaceAnchor(position, normal, playerId);
            _chargesLeft--;
            ConsumeDurability(100f / _maxCharges);
            Debug.Log($"[AnchorBolt] 設置完了（同期）。残り {_chargesLeft}/{_maxCharges} 個");
            return;
        }

        if (_anchorPrefab != null)
        {
            var anchor = Instantiate(_anchorPrefab, position, Quaternion.LookRotation(normal));
            anchor.name = "AnchorBolt_Placed";
            var anchorRb = anchor.GetComponent<Rigidbody>() ?? anchor.AddComponent<Rigidbody>();
            anchorRb.isKinematic = true;
            anchorRb.useGravity  = false;
            GameServices.Ropes?.RegisterAnchorPoint(anchor.transform);
            GameServices.Audio?.PlaySE(SoundId.AnchorBoltSet, position);
            GameServices.Score?.RecordRopePlacement(playerId);
        }
        else
        {
            WorldPlacementFactory.CreateAnchorBolt(position, normal);
            GameServices.Audio?.PlaySE(SoundId.AnchorBoltSet, position);
            GameServices.Score?.RecordRopePlacement(playerId);
        }

        _chargesLeft--;
        ConsumeDurability(100f / _maxCharges);

        Debug.Log($"[AnchorBolt] 設置完了。残り {_chargesLeft}/{_maxCharges} 個");
    }

    public override bool TryUse()
    {
        // TryUse は TryPlaceAnchor(Transform) 経由で呼ぶ想定
        return _chargesLeft > 0 && !_isBroken;
    }

    protected override void OnItemBroken()
    {
        Debug.Log("[AnchorBolt] アンカーボルトを使い切りました");
        base.OnItemBroken();
    }
}
