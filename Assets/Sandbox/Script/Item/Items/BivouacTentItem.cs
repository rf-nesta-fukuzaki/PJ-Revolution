using UnityEngine;

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

    private void OnEnable()
    {
        // シーン遷移（新遠征）でフラグリセット
        _hasBeenPlacedThisExpedition = false;
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

        SpawnTent(position, rotation);
        return true;
    }

    private void SpawnTent(Vector3 position, Quaternion rotation)
    {
        if (_tentPrefab != null)
        {
            _tentInstance = Instantiate(_tentPrefab, position, rotation);
        }
        else
        {
            // プリミティブ代替：Cube でテントを表現
            _tentInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _tentInstance.transform.position   = position + Vector3.up * 1f;
            _tentInstance.transform.rotation   = rotation;
            _tentInstance.transform.localScale = new Vector3(3f, 2f, 3f);

            var rend = _tentInstance.GetComponent<Renderer>();
            if (rend != null)
                rend.material.color = new Color(0.2f, 0.5f, 0.8f);

            // テント内部は Trigger にして天候保護エリアを判定
            var col = _tentInstance.GetComponent<BoxCollider>();
            if (col != null) col.isTrigger = false;
        }

        _tentInstance.name = "BivouacTent_Placed";

        // チェックポイント機能をアタッチ
        var checkpoint = _tentInstance.AddComponent<BivouacCheckpoint>();
        checkpoint.Init(_shelterRadius);

        _isPlaced                    = true;
        _hasBeenPlacedThisExpedition = true;

        ConsumeDurability(20f);  // 設置で消耗
        Debug.Log($"[BivouacTent] テント設置完了。チェックポイントとして登録");
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
#pragma warning disable CS0414
    private bool  _isRegistered;
#pragma warning restore CS0414

    public void Init(float shelterRadius)
    {
        _shelterRadius = shelterRadius;

        // チェックポイントとして ExpeditionManager に登録
        ExpeditionManager.Instance?.RegisterDynamicCheckpoint(transform);
        _isRegistered = true;
        Debug.Log($"[BivouacCheckpoint] チェックポイント登録 at {transform.position}");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var health = other.GetComponent<PlayerHealthSystem>();
        if (health == null) return;

        // テント内では天候ダメージを無効化（WeatherSystem に通知）
        WeatherSystem.Instance?.AddShelterOccupant(other.gameObject);
        Debug.Log($"[BivouacCheckpoint] {other.name} がテントに入りました（天候保護）");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        WeatherSystem.Instance?.RemoveShelterOccupant(other.gameObject);
        Debug.Log($"[BivouacCheckpoint] {other.name} がテントから出ました");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.3f);
        Gizmos.DrawSphere(transform.position, _shelterRadius);
    }
}
