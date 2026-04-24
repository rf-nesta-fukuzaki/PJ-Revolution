using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

/// <summary>
/// GDD §2.2 — ベースキャンプ準備フェーズのショップ UI。
/// チーム共有予算（100pt）でアイテムを購入する。
/// 「今日の天気」「ルート状況」をボードに表示する。
/// </summary>
public class BasecampShop : MonoBehaviour
{
    private static Material s_runtimeItemMaterial;

    private const int TEAM_BUDGET_MAX = 100;
    private const float ERROR_DISPLAY_SECONDS = 2f;

    private int _localTeamBudget = TEAM_BUDGET_MAX;
    private BasecampShopSession _session;
    private readonly Dictionary<string, BasecampShopItemDefinition> _catalogById = new();
    private readonly List<BasecampShopItemDefinition> _orderedCatalog = new();
    private readonly List<ShopItemRow> _rows = new();
    private Transform _itemListContentParent;

    private NetworkBasecampBudgetSync _budgetSync;
    private Coroutine _errorRoutine;

    // ── Inspector ───────────────────────────────────────────
    [Header("カタログ設定")]
    [SerializeField] private BasecampShopCatalogSO _catalogAsset;

    [Header("UI ルート")]
    [SerializeField] private GameObject _shopPanel;

    [Header("表示制御")]
    [SerializeField] private bool _openOnStart = false;
    [SerializeField] private KeyCode _togglePanelKey = KeyCode.B;

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

    [Header("エラー表示 (GDD §8.5 / §14.6)")]
    [SerializeField] private TextMeshProUGUI _errorLabel;

    private bool _tutorialShownThisSession;

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        BuildCatalogLookup();
        _session = new BasecampShopSession(_catalogById);

        if (_shopPanel != null && !_openOnStart)
            _shopPanel.SetActive(false);
    }

    private void Start()
    {
        _departButton?.onClick.AddListener(OnDepart);
        ConfigureItemListContainer();
        BuildItemList();

        BindBudgetSync();

        if (_errorLabel != null) _errorLabel.gameObject.SetActive(false);

        RefreshBudgetLabel();
        RefreshInfoBoard();
        RefreshAllRows();

        SetShopPanelVisible(_openOnStart);
    }

    private void OnDestroy()
    {
        _departButton?.onClick.RemoveListener(OnDepart);
        UnbindBudgetSync();
    }

    private void HandleNetworkBudgetChanged(int newBudget)
    {
        RefreshBudgetLabel();
        RefreshAllRows();
    }

    private int GetCurrentBudget()
    {
        if (IsUsingNetworkBudget())
            return _budgetSync.TeamBudget;
        return _localTeamBudget;
    }

    private bool IsUsingNetworkBudget()
    {
        return _budgetSync != null && _budgetSync.IsSpawned;
    }

    private void BindBudgetSync()
    {
        _budgetSync = NetworkBasecampBudgetSync.Instance;
        if (_budgetSync == null) return;

        _budgetSync.OnBudgetChanged          += HandleNetworkBudgetChanged;
        _budgetSync.OnPurchaseResultForLocal += HandlePurchaseResult;
    }

    private void UnbindBudgetSync()
    {
        if (_budgetSync == null) return;

        _budgetSync.OnBudgetChanged          -= HandleNetworkBudgetChanged;
        _budgetSync.OnPurchaseResultForLocal -= HandlePurchaseResult;
        _budgetSync = null;
    }

    private void Update()
    {
        if (_shopPanel == null || _session == null) return;
        if (_session.CurrentState != BasecampShopFlowState.Open) return;

        if (Input.GetKeyDown(_togglePanelKey))
            SetShopPanelVisible(!_shopPanel.activeSelf);
    }

    private void SetShopPanelVisible(bool visible)
    {
        if (_shopPanel == null) return;
        _shopPanel.SetActive(visible);

        // GDD §21.3: 初回来訪時のみショップ操作チュートリアルを表示。
        if (visible && !_tutorialShownThisSession)
        {
            ShopTutorialOverlay.Instance?.ShowIfFirstTime();
            _tutorialShownThisSession = true;
        }
    }

    public bool TryPurchase(string itemId)
    {
        if (!_session.TryBuildPurchaseRequest(itemId, GetCurrentBudget(), out var item, out var reason))
        {
            PPAudioManager.Instance?.PlaySE2D(SoundId.UiPurchaseFail);
            ShowError(reason);
            Debug.LogWarning($"[Shop] 購入拒否: {itemId} ({reason})");
            return false;
        }

        if (IsUsingNetworkBudget())
        {
            _budgetSync.RequestPurchaseServerRpc(item.Id, item.Cost);
            return true;
        }

        _localTeamBudget -= item.Cost;
        if (_localTeamBudget < 0)
        {
            Debug.LogError($"[Contract] ローカル予算が負値です。itemId={item.Id}");
            _localTeamBudget = 0;
        }

        if (!_session.ConfirmPurchase(item.Id, out var confirmError))
        {
            _localTeamBudget = Mathf.Min(TEAM_BUDGET_MAX, _localTeamBudget + item.Cost);
            PPAudioManager.Instance?.PlaySE2D(SoundId.UiPurchaseFail);
            ShowError(confirmError);
            return false;
        }

        PPAudioManager.Instance?.PlaySE2D(SoundId.UiPurchase);

        RefreshBudgetLabel();
        RefreshAllRows();
        Debug.Log($"[Shop] 購入: {item.DisplayName}  残り予算: {GetCurrentBudget()}pt");
        return true;
    }

    private void HandlePurchaseResult(string itemId, bool success, string reason)
    {
        if (success)
        {
            if (!_session.ConfirmPurchase(itemId, out var confirmError))
            {
                Debug.LogError($"[Shop] 購入承認の反映に失敗: {itemId} ({confirmError})");
                return;
            }

            PPAudioManager.Instance?.PlaySE2D(SoundId.UiPurchase);

            RefreshBudgetLabel();
            RefreshAllRows();
            Debug.Log($"[Shop] 購入承認: {itemId}");
        }
        else
        {
            PPAudioManager.Instance?.PlaySE2D(SoundId.UiPurchaseFail);
            ShowError(string.IsNullOrEmpty(reason) ? "購入に失敗しました" : reason);
            Debug.Log($"[Shop] 購入拒否: {itemId} - {reason}");
        }
    }

    private void ShowError(string message)
    {
        if (_errorLabel == null) return;

        _errorLabel.text = message;
        _errorLabel.gameObject.SetActive(true);

        if (_errorRoutine != null) StopCoroutine(_errorRoutine);
        _errorRoutine = StartCoroutine(HideErrorAfterDelay());
    }

    private IEnumerator HideErrorAfterDelay()
    {
        yield return new WaitForSeconds(ERROR_DISPLAY_SECONDS);
        if (_errorLabel != null) _errorLabel.gameObject.SetActive(false);
        _errorRoutine = null;
    }

    public bool TryRefund(string itemId)
    {
        if (!_session.TryRefund(itemId, out var item, out var reason))
        {
            if (!string.IsNullOrEmpty(reason))
                ShowError(reason);
            return false;
        }

        if (IsUsingNetworkBudget())
            _budgetSync.RefundServerRpc(item.Cost);
        else
            _localTeamBudget = Mathf.Min(TEAM_BUDGET_MAX, _localTeamBudget + item.Cost);

        RefreshBudgetLabel();
        RefreshAllRows();
        Debug.Log($"[Shop] 返品: {item.DisplayName}  残り予算: {GetCurrentBudget()}pt");
        return true;
    }

    private void BuildItemList()
    {
        if (_itemListParent == null) return;
        var rowParent = _itemListContentParent != null ? _itemListContentParent : _itemListParent;

        foreach (Transform child in rowParent)
            Destroy(child.gameObject);

        _rows.Clear();

        foreach (var item in _orderedCatalog)
        {
            GameObject entryObject;

            if (_shopItemRowPrefab != null)
            {
                entryObject = Instantiate(_shopItemRowPrefab, rowParent);
            }
            else
            {
                entryObject = CreateFallbackRow();
                entryObject.transform.SetParent(rowParent, false);
            }

            var row = entryObject.GetComponent<ShopItemRow>() ?? entryObject.AddComponent<ShopItemRow>();
            row.Init(item, TryPurchase, TryRefund);
            _rows.Add(row);
        }
    }

    private void ConfigureItemListContainer()
    {
        _itemListContentParent = _itemListParent;
        if (!(_itemListParent is RectTransform viewport)) return;

        var viewportLayout = viewport.GetComponent<VerticalLayoutGroup>();
        if (viewportLayout != null)
            viewportLayout.enabled = false;

        var viewportFitter = viewport.GetComponent<ContentSizeFitter>();
        if (viewportFitter != null)
            viewportFitter.enabled = false;

        var image = viewport.GetComponent<Image>();
        if (image == null)
            image = viewport.gameObject.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.02f);

        var mask = viewport.GetComponent<RectMask2D>();
        if (mask == null)
            mask = viewport.gameObject.AddComponent<RectMask2D>();

        var content = viewport.Find("Content") as RectTransform;
        if (content == null)
        {
            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport, false);
            content = contentGo.GetComponent<RectTransform>();
        }

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        var contentLayout = content.GetComponent<VerticalLayoutGroup>();
        if (contentLayout == null)
            contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 8f;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.padding = new RectOffset(6, 6, 6, 6);

        var contentFitter = content.GetComponent<ContentSizeFitter>();
        if (contentFitter == null)
            contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scrollRect = viewport.GetComponent<ScrollRect>();
        if (scrollRect == null)
            scrollRect = viewport.gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 40f;

        _itemListContentParent = content;
    }

    private void RefreshAllRows()
    {
        if (_session == null) return;

        int budget = GetCurrentBudget();
        foreach (var row in _rows)
        {
            int count = _session.GetPurchasedCount(row.ItemId);
            row.Refresh(count, budget);
        }
    }

    private void RefreshBudgetLabel()
    {
        if (_budgetLabel != null)
            _budgetLabel.text = $"予算: {GetCurrentBudget()} / {TEAM_BUDGET_MAX} pt";
    }

    private void RefreshInfoBoard()
    {
        var weather = GameServices.Weather;
        if (_weatherLabel != null)
            _weatherLabel.text = $"今日の天気: {GetWeatherDisplay(weather)}";

        if (_routeStatusLabel != null)
            _routeStatusLabel.text = GameServices.Spawner?.GetRouteStatusSummary()
                                     ?? "ルート状況: 調査中...";
    }

    private static string GetWeatherDisplay(IWeatherService weather)
    {
        if (weather == null) return "☀ 晴れ";
        return weather.CurrentWeather switch
        {
            WeatherType.Sunny    => "☀ 晴れ",
            WeatherType.Cloudy   => "☁ 曇り",
            WeatherType.Fog      => "🌫 霧",
            WeatherType.Rain     => "🌧 雨",
            WeatherType.Blizzard => "❄ 吹雪",
            _                    => "不明"
        };
    }

    private void OnDepart()
    {
        if (!_session.TryDepart(out var reason))
        {
            ShowError(reason);
            return;
        }

        GrantItemsToLocalPlayer();

        SetShopPanelVisible(false);

        GameServices.Expedition?.StartExpedition();

        Debug.Log($"[Shop] 出発！購入: {GetTotalPurchasedCount()}個  使用予算: {TEAM_BUDGET_MAX - GetCurrentBudget()}pt");
    }

    private int GetTotalPurchasedCount()
    {
        int total = 0;
        foreach (var count in _session.PurchasedCounts.Values)
            total += count;
        return total;
    }

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

        foreach (var kvp in _session.PurchasedCounts)
        {
            if (kvp.Value <= 0) continue;
            if (!_catalogById.TryGetValue(kvp.Key, out var definition))
            {
                Debug.LogError($"[Shop] カタログに存在しないIDです: {kvp.Key}");
                continue;
            }

            for (int i = 0; i < kvp.Value; i++)
            {
                var go = CreateItemObject(definition);
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

    private static GameObject CreateItemObject(BasecampShopItemDefinition definition)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = definition.DisplayName;
        go.transform.localScale = Vector3.one * 0.25f;

        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            var material = GetRuntimeItemMaterial();
            if (material != null) rend.sharedMaterial = material;
        }

        go.AddComponent<Rigidbody>();

        if (definition.IsMetal)
            go.AddComponent<MagneticTarget>();

        if (!BasecampShopItemFactory.TryCreate(go, definition.ItemType, out _))
        {
            Debug.LogError($"[Shop] ItemType のファクトリが未定義です: {definition.ItemType}");
            Destroy(go);
            return null;
        }

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

    private GameObject CreateFallbackRow()
    {
        var go = new GameObject("ShopRow", typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 56f);
        return go;
    }

    private void BuildCatalogLookup()
    {
        _catalogById.Clear();
        _orderedCatalog.Clear();

        bool loadedFromAsset = _catalogAsset != null
                               && _catalogAsset.Items != null
                               && _catalogAsset.Items.Count > 0
                               && TryAppendCatalog(_catalogAsset.Items, "catalog asset");

        if (!loadedFromAsset && _catalogAsset != null)
            Debug.LogWarning("[Shop] CatalogSO の内容が無効です。デフォルトカタログにフォールバックします。");

        if (!loadedFromAsset)
            TryAppendCatalog(BasecampShopDefaultCatalog.Create(), "default catalog");

        if (_catalogById.Count == 0)
            throw new InvalidOperationException("[Shop] 有効なカタログ項目が0件です。");
    }

    private bool TryAppendCatalog(IReadOnlyList<BasecampShopItemDefinition> items, string sourceName)
    {
        if (items == null || items.Count == 0) return false;

        int beforeCount = _catalogById.Count;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item == null)
            {
                Debug.LogError($"[Shop] {sourceName} の index={i} が null です。");
                continue;
            }

            if (!item.TryValidate(out var reason))
            {
                Debug.LogError($"[Shop] {sourceName} の item '{item.Id}' が無効です。理由: {reason}");
                continue;
            }

            if (_catalogById.ContainsKey(item.Id))
            {
                Debug.LogError($"[Shop] 重複 itemId を検出: {item.Id}");
                continue;
            }

            _catalogById.Add(item.Id, item);
            _orderedCatalog.Add(item);
        }

        return _catalogById.Count > beforeCount;
    }
}
