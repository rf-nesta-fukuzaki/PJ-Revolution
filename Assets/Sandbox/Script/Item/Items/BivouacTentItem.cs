using UnityEngine;
using PeakPlunder.Audio;

/// <summary>
/// GDD §5.2 — アイテム「ビバークテント」
/// 設置型チェックポイント＆天候シェルター。1遠征1個限定。
/// コスト 15pt / 重量 2 / スロット 2 / 耐久 80
/// </summary>
public class BivouacTentItem : ItemBase
{
    [Header("テント設定")]
    [SerializeField] private GameObject  _tentPrefab;          // Inspector で設定（なければプリミティブ代替）
    [SerializeField] private float       _shelterRadius = 3f;  // シェルター半径（天候保護エリア）

    private static bool _hasBeenPlacedThisExpedition;   // 1遠征1個制限
    private bool        _isPlaced;
    private GameObject  _tentInstance;

    public bool IsPlaced => _isPlaced;

    public static bool IsPlacedThisExpedition => _hasBeenPlacedThisExpedition;

    public static void MarkPlacedThisExpedition() => _hasBeenPlacedThisExpedition = true;

    protected override void Awake()
    {
        base.Awake();
        _itemName          = "ビバークテント";
        _cost              = 15;
        _weight            = 2f;
        _slots             = 2;
        _maxDurability     = 80f;
        _currentDurability = _maxDurability;
        _impactDmgScale    = 0.5f;
    }

    /// <summary>現在地にテントを設置する。</summary>
    public bool TryPlace(Vector3 position, Quaternion rotation)
    {
        if (_isBroken || _isPlaced) return false;

        if (_hasBeenPlacedThisExpedition)
        {
            Debug.Log("[BivouacTent] 今回の遠征ではすでにテントを設置済みです");
            return false;
        }

        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            var sync = NetworkWorldPlacementsSync.Instance
                ?? Object.FindFirstObjectByType<NetworkWorldPlacementsSync>()
                ?? (nm.IsServer ? NetworkWorldPlacementsSync.EnsureExists() : null);
            if (sync == null)
            {
                Debug.LogWarning("[BivouacTent] NetworkWorldPlacementsSync 未準備 — 設置をスキップ");
                return false;
            }

            if (!sync.RequestPlaceBivouac(position, rotation, _shelterRadius))
                return false;

            _isPlaced = true;
            ConsumeDurability(20f);
            Debug.Log("[BivouacTent] テント設置完了（ネットワーク同期）");
            return true;
        }

        SpawnTentLocal(position, rotation);
        return true;
    }

    private void SpawnTentLocal(Vector3 position, Quaternion rotation)
    {
        if (_tentPrefab != null)
        {
            _tentInstance = Instantiate(_tentPrefab, position, rotation);
            _tentInstance.name = "BivouacTent_Placed";
            var checkpoint = _tentInstance.GetComponent<BivouacCheckpoint>();
            if (checkpoint == null)
                checkpoint = _tentInstance.AddComponent<BivouacCheckpoint>();
            checkpoint.Init(_shelterRadius);
        }
        else
        {
            _tentInstance = WorldPlacementFactory.CreateBivouacTent(position, rotation, _shelterRadius);
        }

        _isPlaced                    = true;
        _hasBeenPlacedThisExpedition = true;
        GameServices.Audio?.PlaySE(SoundId.TentSetup, position);
        ConsumeDurability(20f);
        Debug.Log("[BivouacTent] テント設置完了。チェックポイントとして登録");
    }

    protected override float GetUseDurabilityDrain() => 10f;

    protected override void OnItemBroken()
    {
        if (_tentInstance != null)
            Destroy(_tentInstance);

        Debug.Log("[BivouacTent] テントが壊れました");
        base.OnItemBroken();
    }

    // 遠征終了時にフラグをリセットする（ExpeditionManager から呼ぶ）
    public static void ResetExpeditionFlag() => _hasBeenPlacedThisExpedition = false;
}

/// <summary>設置テントのチェックポイント＆天候シェルター機能。</summary>
public class BivouacCheckpoint : MonoBehaviour
{
    private float _shelterRadius;
    private bool  _isRegistered;

    /// <summary>チェックポイントとして登録済みか（二重登録防止のため公開）。</summary>
    public bool IsRegistered => _isRegistered;

    public void Init(float shelterRadius)
    {
        // 冪等性: Init は一度だけ登録処理を行う（複数回呼ばれても安全）
        if (_isRegistered)
        {
            _shelterRadius = shelterRadius;  // 半径のみ更新
            return;
        }

        _shelterRadius = shelterRadius;

        // チェックポイントとして ExpeditionManager に登録
        GameServices.Expedition?.RegisterDynamicCheckpoint(transform);
        _isRegistered = true;
        Debug.Log($"[BivouacCheckpoint] チェックポイント登録 at {transform.position}");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.3f);
        Gizmos.DrawSphere(transform.position, _shelterRadius);
    }
}
