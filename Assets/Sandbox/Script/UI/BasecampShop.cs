using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GDD §2.2 — ベースキャンプ準備フェーズのショップ UI。
/// チーム共有予算（100pt）でアイテムを購入する。
/// 「今日の天気」「ルート状況」をボードに表示する。
/// </summary>
public class BasecampShop : MonoBehaviour
{
    private static Material s_runtimeItemMaterial;

    public static BasecampShop Instance { get; private set; }

    // ── チーム予算（オフライン / NGO 未スポーン時のローカルフォールバック）──
    private const int TEAM_BUDGET_MAX = 100;
    private int _teamBudget = TEAM_BUDGET_MAX;

    // NetworkBasecampBudgetSync が存在すれば優先する
    private NetworkBasecampBudgetSync _budgetSync;

    // ── Inspector ───────────────────────────────────────────
    [Header("UI ルート")]
    [SerializeField] private GameObject _shopPanel;

    [Header("予算表示")]
    [SerializeField] private TextMeshProUGUI _budgetLabel;

    [Header("アイテムリスト")]
    [SerializeField] private Transform       _itemListParent;
    [SerializeField] private GameObject      _shopItemRowPrefab;  // ShopItemRow コンポーネント付き

    [Header("情報ボード")]
    [SerializeField] private TextMeshProUGUI _weatherLabel;
    [SerializeField] private TextMeshProUGUI _routeStatusLabel;

    [Header("ボタン")]
    [SerializeField] private Button          _departButton;

    // ── アイテム定義 ─────────────────────────────────────────
    private readonly List<ShopItemData> _catalog = new()
    {
        new ShopItemData("ショートロープ（10m）",  5,  1, 1, 80,  "基本のプレイヤー連結。極端な負荷で切れる"),
        new ShopItemData("ロングロープ（25m）",    10, 2, 2, 70,  "広範囲連結。チームの展開幅が広がる"),
        new ShopItemData("アイスアックス",         8,  1, 1, 60,  "氷壁グリップ。15回使用で破損"),
        new ShopItemData("アンカーボルト（×3）",   6,  1, 1, 100, "ロープ固定点。消耗品"),
        new ShopItemData("グラップリングフック",   12, 2, 1, 50,  "遠距離の崖に到達。物理エイム"),
        new ShopItemData("折りたたみ担架",         10, 3, 3, 70,  "大型遺物運搬用。2人で安定、1人で引きずり"),
        new ShopItemData("梱包キット",             8,  2, 1, 100, "壊れやすい遺物を保護。ダメージ50%軽減。3回使用"),
        new ShopItemData("サーマルケース",          4,  1, 1, 90,  "温度変化に弱い遺物を保護"),
        new ShopItemData("固定ベルト",              6,  1, 1, 100, "遺物を背中に固定。両手が空く。小型遺物のみ"),
        new ShopItemData("食料（×3）",              3,  1, 1, 100, "スタミナ回復。投げて渡せる"),
        new ShopItemData("フレアガン",              5,  1, 1, 100, "ルートマーキング＆ヘリ信号。3発"),
        new ShopItemData("緊急無線機",              7,  1, 1, 40,  "プロキシミティ距離制限を30秒無効化。超壊れやすい"),
        new ShopItemData("ポータブルウインチ",     20, 3, 2, 50,  "機械的引き上げ。急斜面で威力を発揮。ケーブル切断リスク"),
        new ShopItemData("ビバークテント",          15, 2, 2, 80,  "設置型チェックポイント＆天候シェルター。1遠征1個限定"),
        new ShopItemData("酸素タンク",              12, 2, 1, 60,  "高山病防止（2000m以上で必要）"),
    };

    // 購入済みアイテム（アイテム名 → 個数）
    private readonly Dictionary<string, int> _purchasedItems = new();

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _departButton?.onClick.AddListener(OnDepart);
        BuildItemList();

        // NetworkBasecampBudgetSync が存在する場合はネットワーク予算を使用
        _budgetSync = NetworkBasecampBudgetSync.Instance;
        if (_budgetSync != null)
            _budgetSync.OnBudgetChanged += HandleNetworkBudgetChanged;

        RefreshBudgetLabel();
        RefreshInfoBoard();

        if (_shopPanel != null) _shopPanel.SetActive(true);
    }

    private void OnDestroy()
    {
        if (_budgetSync != null)
            _budgetSync.OnBudgetChanged -= HandleNetworkBudgetChanged;
    }

    private void HandleNetworkBudgetChanged(int newBudget)
    {
        // NetworkVariable が更新されたら全クライアントの UI を更新
        RefreshBudgetLabel();
        RefreshAllRows();
    }

    // ── 予算取得ヘルパー ─────────────────────────────────────
    /// <summary>
    /// 現在の残予算を返す。
    /// NetworkBasecampBudgetSync がスポーン済みならネットワーク値を使用し、
    /// そうでなければローカルフォールバックを使用する。
    /// </summary>
    private int GetCurrentBudget()
    {
        if (_budgetSync != null && _budgetSync.IsSpawned)
            return _budgetSync.TeamBudget;
        return _teamBudget;
    }

    // ── 購入 ─────────────────────────────────────────────────
    /// <summary>アイテム名を指定して購入。UI ボタンから呼ばれる。</summary>
    public bool TryPurchase(string itemName)
    {
        var data = _catalog.Find(d => d.Name == itemName);
        if (data == null)
        {
            Debug.LogWarning($"[Shop] 不明なアイテム: {itemName}");
            return false;
        }

        int budget = GetCurrentBudget();
        if (budget < data.Cost)
        {
            Debug.Log($"[Shop] 予算不足（残り {budget}pt、必要 {data.Cost}pt）");
            return false;
        }

        // ネットワーク同期が有効ならサーバー経由で予算を減算
        if (_budgetSync != null && _budgetSync.IsSpawned)
            _budgetSync.DeductServerRpc(data.Cost);
        else
            _teamBudget -= data.Cost;

        _purchasedItems.TryGetValue(itemName, out int count);
        _purchasedItems[itemName] = count + 1;

        RefreshBudgetLabel();
        RefreshAllRows();
        Debug.Log($"[Shop] 購入: {itemName}  残り予算: {GetCurrentBudget()}pt");
        return true;
    }

    /// <summary>アイテムを返品（使用前に限る）。</summary>
    public bool TryRefund(string itemName)
    {
        if (!_purchasedItems.TryGetValue(itemName, out int count) || count <= 0) return false;

        var data = _catalog.Find(d => d.Name == itemName);
        if (data == null) return false;

        // ネットワーク同期が有効ならサーバー経由で予算を加算
        if (_budgetSync != null && _budgetSync.IsSpawned)
            _budgetSync.RefundServerRpc(data.Cost);
        else
            _teamBudget += data.Cost;

        _purchasedItems[itemName] = count - 1;

        RefreshBudgetLabel();
        RefreshAllRows();
        Debug.Log($"[Shop] 返品: {itemName}  残り予算: {GetCurrentBudget()}pt");
        return true;
    }

    // ── UI 構築 ──────────────────────────────────────────────
    private readonly List<ShopItemRow> _rows = new();

    private void BuildItemList()
    {
        if (_itemListParent == null) return;

        foreach (Transform child in _itemListParent)
            Destroy(child.gameObject);

        _rows.Clear();

        foreach (var data in _catalog)
        {
            GameObject go;
            ShopItemRow row;

            if (_shopItemRowPrefab != null)
            {
                go  = Instantiate(_shopItemRowPrefab, _itemListParent);
                row = go.GetComponent<ShopItemRow>() ?? go.AddComponent<ShopItemRow>();
            }
            else
            {
                go  = CreateFallbackRow();
                go.transform.SetParent(_itemListParent, false);
                row = go.AddComponent<ShopItemRow>();
            }

            row.Init(data, this);
            _rows.Add(row);
        }
    }

    private void RefreshAllRows()
    {
        foreach (var row in _rows)
            row.Refresh(_purchasedItems.GetValueOrDefault(row.ItemName, 0), GetCurrentBudget());
    }

    private void RefreshBudgetLabel()
    {
        if (_budgetLabel != null)
            _budgetLabel.text = $"予算: {GetCurrentBudget()} / {TEAM_BUDGET_MAX} pt";
    }

    private void RefreshInfoBoard()
    {
        // 天候情報
        var weather = GameServices.Weather;
        if (_weatherLabel != null && weather != null)
        {
            string weatherName = weather.CurrentWeather switch
            {
                WeatherType.Sunny   => "☀ 晴れ",
                WeatherType.Cloudy  => "☁ 曇り",
                WeatherType.Fog     => "🌫 霧",
                WeatherType.Rain    => "🌧 雨",
                WeatherType.Blizzard => "❄ 吹雪",
                _                   => "不明"
            };
            _weatherLabel.text = $"今日の天気: {weatherName}";
        }
        else if (_weatherLabel != null)
        {
            _weatherLabel.text = "今日の天気: ☀ 晴れ";
        }

        // ルート状況（SpawnManager の L2 実行結果を反映）
        if (_routeStatusLabel != null)
            _routeStatusLabel.text = GameServices.Spawner?.GetRouteStatusSummary()
                                     ?? "ルート状況: 調査中...";
    }

    // ── 出発 ─────────────────────────────────────────────────
    private void OnDepart()
    {
        GrantItemsToLocalPlayer();

        if (_shopPanel != null) _shopPanel.SetActive(false);

        GameServices.Expedition?.StartExpedition();

        Debug.Log($"[Shop] 出発！購入: {GetTotalPurchasedCount()}個  使用予算: {TEAM_BUDGET_MAX - GetCurrentBudget()}pt");
    }

    private int GetTotalPurchasedCount()
    {
        int total = 0;
        foreach (var count in _purchasedItems.Values) total += count;
        return total;
    }

    // ── アイテム付与 ─────────────────────────────────────────
    /// <summary>購入済みアイテムをローカルプレイヤーのインベントリに付与する。</summary>
    private void GrantItemsToLocalPlayer()
    {
        var inventory = FindLocalPlayerInventory();
        if (inventory == null)
        {
            Debug.LogWarning("[Shop] ローカルプレイヤーのインベントリが見つかりません");
            return;
        }

        int grantedCount = 0;
        var basePos = inventory.transform.position;

        foreach (var kvp in _purchasedItems)
        {
            if (kvp.Value <= 0) continue;

            for (int i = 0; i < kvp.Value; i++)
            {
                var go = CreateItemObject(kvp.Key);
                if (go == null) continue;

                var item = go.GetComponent<ItemBase>();
                if (item == null) { Destroy(go); continue; }

                // インベントリに追加。満杯なら足元にドロップ
                bool added = inventory.TryAdd(item);
                if (!added)
                    go.transform.position = basePos + UnityEngine.Random.insideUnitSphere * 0.6f + Vector3.up * 0.5f;

                grantedCount++;
            }
        }

        Debug.Log($"[Shop] {grantedCount} 個のアイテムを付与しました");
    }

    /// <summary>ローカルオーナーの PlayerInventory を返す。NGO が未起動の場合は最初の一つを返す。</summary>
    private static PlayerInventory FindLocalPlayerInventory()
    {
        var inventories = PlayerInventory.RegisteredInventories;
        foreach (var inv in inventories)
        {
            var no = inv.GetComponent<NetworkObject>();
            if (no != null && no.IsOwner) return inv;
        }
        return inventories.Count > 0 ? inventories[0] : null;
    }

    /// <summary>
    /// カタログ名 → アイテム生成ファクトリ関数の対応表。
    /// go.AddComponent(System.Type) によるリフレクションを廃止し、
    /// 型安全なジェネリック AddComponent&lt;T&gt; に統一する。
    /// </summary>
    private static readonly Dictionary<string, System.Func<GameObject, ItemBase>> _catalogFactoryMap = new()
    {
        { "ショートロープ（10m）",  go => go.AddComponent<ShortRopeItem>() },
        { "ロングロープ（25m）",    go => go.AddComponent<LongRopeItem>() },
        { "アイスアックス",         go => go.AddComponent<IceAxeItem>() },
        { "アンカーボルト（×3）",   go => go.AddComponent<AnchorBoltItem>() },
        { "グラップリングフック",   go => go.AddComponent<GrapplingHookItem>() },
        { "折りたたみ担架",         go => go.AddComponent<StretcherItem>() },
        { "梱包キット",             go => go.AddComponent<PackingKitItem>() },
        { "サーマルケース",          go => go.AddComponent<ThermalCaseItem>() },
        { "固定ベルト",              go => go.AddComponent<SecureBeltItem>() },
        { "食料（×3）",              go => go.AddComponent<FoodItem>() },
        { "フレアガン",              go => go.AddComponent<FlareGunItem>() },
        { "緊急無線機",              go => go.AddComponent<EmergencyRadioItem>() },
        { "ポータブルウインチ",     go => go.AddComponent<PortableWinchItem>() },
        { "ビバークテント",          go => go.AddComponent<BivouacTentItem>() },
        { "酸素タンク",              go => go.AddComponent<OxygenTankItem>() },
    };

    // 金属アイテム（MagneticTarget を追加するもの）
    private static readonly HashSet<string> _metalCatalogNames = new()
    {
        "グラップリングフック",
        "アイスアックス",
        "アンカーボルト（×3）",
        "ポータブルウインチ",
        "ショートロープ（10m）",
        "ロングロープ（25m）",
    };

    /// <summary>カタログ名に対応する ItemBase コンポーネントを持つ GameObject を生成して返す。</summary>
    private static GameObject CreateItemObject(string catalogName)
    {
        if (!_catalogFactoryMap.TryGetValue(catalogName, out var factory))
        {
            Debug.LogWarning($"[Shop] 未知のカタログ名: {catalogName}");
            return null;
        }

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = catalogName;
        go.transform.localScale = Vector3.one * 0.25f;

        // URP マテリアルを適用
        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            var material = GetRuntimeItemMaterial();
            if (material != null) rend.sharedMaterial = material;
        }

        // ItemBase は [RequireComponent(Rigidbody)] のためファクトリ呼び出しより先に追加
        go.AddComponent<Rigidbody>();

        // 金属アイテムには MagneticTarget を付与（MagneticHelmetRelic の引き寄せ対象）
        if (_metalCatalogNames.Contains(catalogName))
            go.AddComponent<MagneticTarget>();

        factory(go);   // 型安全な AddComponent<T>() を呼ぶ
        return go;
    }

    private static Material GetRuntimeItemMaterial()
    {
        if (s_runtimeItemMaterial != null) return s_runtimeItemMaterial;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) return null;

        s_runtimeItemMaterial = new Material(shader)
        {
            name = "BasecampShopRuntimeItemMaterial"
        };
        return s_runtimeItemMaterial;
    }

    // ── フォールバック UI 生成 ───────────────────────────────
    private GameObject CreateFallbackRow()
    {
        var go = new GameObject("ShopRow");
        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.childControlWidth  = true;
        layout.childControlHeight = true;
        layout.spacing = 8f;
        return go;
    }
}

// ── データ構造 ──────────────────────────────────────────────
[System.Serializable]
public class ShopItemData
{
    public string Name;
    public int    Cost;
    public float  Weight;
    public int    Slots;
    public float  Durability;
    public string Description;

    public ShopItemData(string name, int cost, float weight, int slots, float durability, string desc)
    {
        Name        = name;
        Cost        = cost;
        Weight      = weight;
        Slots       = slots;
        Durability  = durability;
        Description = desc;
    }
}

/// <summary>ショップの各アイテム行 UI コンポーネント。</summary>
public class ShopItemRow : MonoBehaviour
{
    public string ItemName { get; private set; }

    private ShopItemData  _data;
    private BasecampShop  _shop;

    // UI 要素（Prefab から取得、なければコードで生成）
    private TextMeshProUGUI _nameLabel;
    private TextMeshProUGUI _costLabel;
    private TextMeshProUGUI _countLabel;
    private Button          _buyButton;
    private Button          _refundButton;

    public void Init(ShopItemData data, BasecampShop shop)
    {
        _data     = data;
        _shop     = shop;
        ItemName  = data.Name;

        SetupUI();
        Refresh(0, 100);
    }

    private void SetupUI()
    {
        // 既存の TMP/Button コンポーネントを探す
        _nameLabel    = transform.Find("NameLabel")?.GetComponent<TextMeshProUGUI>();
        _costLabel    = transform.Find("CostLabel")?.GetComponent<TextMeshProUGUI>();
        _countLabel   = transform.Find("CountLabel")?.GetComponent<TextMeshProUGUI>();
        _buyButton    = transform.Find("BuyButton")?.GetComponent<Button>();
        _refundButton = transform.Find("RefundButton")?.GetComponent<Button>();

        // Prefab に UI がなければコードで生成
        if (_nameLabel == null)
            _nameLabel = CreateTmpLabel("NameLabel", $"{_data.Name}  {_data.Cost}pt");

        _buyButton?.onClick.AddListener(() => _shop.TryPurchase(ItemName));
        _refundButton?.onClick.AddListener(() => _shop.TryRefund(ItemName));
    }

    public void Refresh(int purchasedCount, int remainingBudget)
    {
        if (_nameLabel != null)
            _nameLabel.text = $"{_data.Name}  [{_data.Cost}pt / 重{_data.Weight} / 枠{_data.Slots}]";

        if (_countLabel != null)
            _countLabel.text = $"×{purchasedCount}";

        if (_buyButton != null)
            _buyButton.interactable = remainingBudget >= _data.Cost;

        if (_refundButton != null)
            _refundButton.interactable = purchasedCount > 0;
    }

    private TextMeshProUGUI CreateTmpLabel(string objName, string text)
    {
        var go  = new GameObject(objName);
        go.transform.SetParent(transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text     = text;
        tmp.fontSize = 14f;
        return tmp;
    }
}
