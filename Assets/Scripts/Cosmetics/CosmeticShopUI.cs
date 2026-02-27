using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// コスメティックショップの UI を制御する MonoBehaviour。
/// CosmeticDatabase の全アイテムを一覧表示し、購入・装備の操作を提供する。
///
/// [UI 構成 (最低限)]
///   ショップCanvas
///   └─ Panel
///      ├─ GemCountText       (Text)   ← 所持宝石数表示
///      ├─ CategoryButtons    (Button×4) ← Hat/Pickaxe/TorchSkin/Accessory 切替
///      ├─ ItemListContent    (Transform) ← ScrollView の Content。アイテム行を動的生成
///      └─ CloseButton        (Button)
///
/// [アイテム行の状態]
///   - アンロック済み + 装備中: 「装備中」ラベル（ボタン無効）
///   - アンロック済み + 未装備: 「装備する」ボタン
///   - 未アンロック:           「🔒 N宝石」購入ボタン
///
/// [セットアップ手順]
///   1. Canvas に Panel を配置し、このスクリプトをアタッチする
///   2. Inspector の各フィールドを割り当てる
///   3. ItemRowPrefab として Button + 複数 Text を持つ Prefab を作成して割り当てる
///   4. PlayerCosmeticSaveData / PlayerCosmetics は FindFirstObjectByType で自動検索する
/// </summary>
public class CosmeticShopUI : MonoBehaviour
{
    // ─────────────── Inspector ───────────────

    [Header("データ参照")]
    [Tooltip("全アイテム定義 ScriptableObject")]
    [SerializeField] private CosmeticDatabase _database;

    [Header("UI 参照")]
    [Tooltip("所持宝石数を表示する Text")]
    [SerializeField] private Text _gemCountText;

    [Tooltip("アイテム行を並べる ScrollView の Content Transform")]
    [SerializeField] private Transform _itemListContent;

    [Tooltip("アイテム 1 行分の Prefab。ItemRowView コンポーネントが付いていること")]
    [SerializeField] private GameObject _itemRowPrefab;

    [Tooltip("Hat カテゴリ切替ボタン")]
    [SerializeField] private Button _hatButton;

    [Tooltip("Pickaxe カテゴリ切替ボタン")]
    [SerializeField] private Button _pickaxeButton;

    [Tooltip("TorchSkin カテゴリ切替ボタン")]
    [SerializeField] private Button _torchSkinButton;

    [Tooltip("Accessory カテゴリ切替ボタン")]
    [SerializeField] private Button _accessoryButton;

    [Tooltip("ショップを閉じるボタン")]
    [SerializeField] private Button _closeButton;

    [Tooltip("ショップ全体の Panel GameObject（開閉制御用）")]
    [SerializeField] private GameObject _shopPanel;

    // ─────────────── 内部状態 ───────────────

    private PlayerCosmeticSaveData _saveData;
    private PlayerCosmetics        _cosmetics;
    private CosmeticCategory       _currentCategory = CosmeticCategory.Hat;

    private readonly List<GameObject> _rowInstances = new();

    // ─────────────── Unity Lifecycle ───────────────

    private void Awake()
    {
        // カテゴリボタンのイベント登録
        if (_hatButton      != null) _hatButton.onClick.AddListener(()      => ShowCategory(CosmeticCategory.Hat));
        if (_pickaxeButton  != null) _pickaxeButton.onClick.AddListener(()  => ShowCategory(CosmeticCategory.Pickaxe));
        if (_torchSkinButton!= null) _torchSkinButton.onClick.AddListener(()=> ShowCategory(CosmeticCategory.TorchSkin));
        if (_accessoryButton!= null) _accessoryButton.onClick.AddListener(()=> ShowCategory(CosmeticCategory.Accessory));
        if (_closeButton    != null) _closeButton.onClick.AddListener(CloseShop);
    }

    private void Start()
    {
        // プレイヤー依存コンポーネントを検索（スポーン後に SetPlayer() で再設定可）
        RefreshPlayerReferences();

        // ショップは最初非表示
        if (_shopPanel != null) _shopPanel.SetActive(false);
    }

    // ─────────────── 公開 API ───────────────

    /// <summary>
    /// ショップを開く。PlayerInputController からキー入力で呼ぶ想定。
    /// </summary>
    public void OpenShop()
    {
        RefreshPlayerReferences();
        if (_shopPanel != null) _shopPanel.SetActive(true);
        RefreshGemCount();
        ShowCategory(_currentCategory);
    }

    /// <summary>ショップを閉じる。</summary>
    public void CloseShop()
    {
        if (_shopPanel != null) _shopPanel.SetActive(false);
    }

    /// <summary>
    /// プレイヤー参照を外部から注入する。
    /// プレイヤー生成後に呼ぶ。
    /// </summary>
    public void SetPlayer(PlayerCosmeticSaveData saveData, PlayerCosmetics cosmetics)
    {
        _saveData  = saveData;
        _cosmetics = cosmetics;
    }

    // ─────────────── 内部処理 ───────────────

    /// <summary>シーン内のプレイヤーコンポーネントを自動検索して参照を更新する。</summary>
    private void RefreshPlayerReferences()
    {
        if (_saveData  == null) _saveData  = FindFirstObjectByType<PlayerCosmeticSaveData>();
        if (_cosmetics == null) _cosmetics = FindFirstObjectByType<PlayerCosmetics>();
    }

    /// <summary>所持宝石数の表示を更新する。</summary>
    private void RefreshGemCount()
    {
        if (_gemCountText == null) return;
        int gems = _saveData != null ? _saveData.Gems : 0;
        _gemCountText.text = $"所持宝石: {gems}";
    }

    /// <summary>指定カテゴリのアイテム一覧を表示する。</summary>
    private void ShowCategory(CosmeticCategory category)
    {
        _currentCategory = category;
        ClearRows();

        if (_database == null || _itemListContent == null || _itemRowPrefab == null) return;

        var items = _database.GetByCategory(category);
        foreach (var item in items)
            CreateRow(item);
    }

    /// <summary>既存のアイテム行をすべて削除する。</summary>
    private void ClearRows()
    {
        foreach (var go in _rowInstances)
            if (go != null) Destroy(go);
        _rowInstances.Clear();
    }

    /// <summary>アイテム 1 行分の GameObject を生成してリストに追加する。</summary>
    private void CreateRow(CosmeticItemData item)
    {
        var go   = Instantiate(_itemRowPrefab, _itemListContent);
        var view = go.GetComponent<CosmeticItemRow>();

        if (view == null)
        {
            // ItemRowPrefab に CosmeticItemRow が付いていない場合は直接 Text/Button を操作
            SetupRowFallback(go, item);
        }
        else
        {
            bool isUnlocked = _saveData != null && _saveData.IsUnlocked(item.Id);
            bool isEquipped = isUnlocked && IsCurrentlyEquipped(item);
            view.Setup(item, isUnlocked, isEquipped, OnBuyClicked, OnEquipClicked);
        }

        _rowInstances.Add(go);
    }

    /// <summary>
    /// CosmeticItemRow コンポーネントがない場合のフォールバック。
    /// 子 Text[0] にアイテム名、子 Button[0] にアクション を割り当てる。
    /// </summary>
    private void SetupRowFallback(GameObject go, CosmeticItemData item)
    {
        var texts   = go.GetComponentsInChildren<Text>(true);
        var buttons = go.GetComponentsInChildren<Button>(true);

        bool isUnlocked = _saveData != null && _saveData.IsUnlocked(item.Id);
        bool isEquipped = isUnlocked && IsCurrentlyEquipped(item);

        // テキスト設定
        if (texts.Length > 0) texts[0].text = item.DisplayName;
        if (texts.Length > 1)
        {
            if (isEquipped)       texts[1].text = "装備中";
            else if (isUnlocked)  texts[1].text = "装備する";
            else                  texts[1].text = $"{item.UnlockPrice} 宝石";
        }

        // ボタン設定
        if (buttons.Length > 0)
        {
            buttons[0].interactable = !isEquipped;
            var capturedItem = item;
            buttons[0].onClick.RemoveAllListeners();
            if (isUnlocked)
                buttons[0].onClick.AddListener(() => OnEquipClicked(capturedItem));
            else
                buttons[0].onClick.AddListener(() => OnBuyClicked(capturedItem));
        }
    }

    /// <summary>現在そのアイテムが装備中かを判定する。</summary>
    private bool IsCurrentlyEquipped(CosmeticItemData item)
    {
        if (_saveData == null) return false;
        return _saveData.GetEquipped(item.Category) == item.Id;
    }

    /// <summary>購入ボタンが押されたときの処理。</summary>
    private void OnBuyClicked(CosmeticItemData item)
    {
        if (_saveData == null)
        {
            Debug.LogWarning("[CosmeticShopUI] PlayerCosmeticSaveData が見つかりません");
            return;
        }

        bool success = _saveData.TryUnlock(item.Id, item.UnlockPrice);
        if (!success) return;

        // 購入後は即装備する
        OnEquipClicked(item);
    }

    /// <summary>装備ボタンが押されたときの処理。</summary>
    private void OnEquipClicked(CosmeticItemData item)
    {
        if (_saveData != null)
            _saveData.SetEquipped(item.Category, item.Id);

        if (_cosmetics != null)
            _cosmetics.RequestEquip(item.Category, item.Id);

        Debug.Log($"[CosmeticShopUI] 装備: {item.Category} = {item.Id}");

        // 表示を更新
        RefreshGemCount();
        ShowCategory(_currentCategory);
    }
}

// ─────────────── アイテム行ビュー ───────────────

/// <summary>
/// アイテム 1 行分の View コンポーネント。ItemRowPrefab にアタッチする。
/// Text および Button を公開フィールドで受け取り、CosmeticShopUI から Setup() で初期化される。
///
/// [Prefab 構成例]
///   ItemRowPrefab
///   ├─ ThumbnailImage  (Image)
///   ├─ NameText        (Text)
///   ├─ StatusText      (Text)    ← "装備中" / "装備する" / "N 宝石"
///   └─ ActionButton    (Button)
/// </summary>
public class CosmeticItemRow : MonoBehaviour
{
    [Header("UI 参照")]
    [Tooltip("アイテム名を表示する Text")]
    [SerializeField] private Text _nameText;

    [Tooltip("状態（装備中 / 装備する / 価格）を表示する Text")]
    [SerializeField] private Text _statusText;

    [Tooltip("購入または装備を実行するボタン")]
    [SerializeField] private Button _actionButton;

    [Tooltip("サムネイル画像 (省略可)")]
    [SerializeField] private Image _thumbnailImage;

    /// <summary>
    /// 行の内容を初期化する。CosmeticShopUI.CreateRow() から呼ばれる。
    /// </summary>
    public void Setup(
        CosmeticItemData item,
        bool isUnlocked,
        bool isEquipped,
        Action<CosmeticItemData> onBuy,
        Action<CosmeticItemData> onEquip)
    {
        if (_nameText      != null) _nameText.text   = item.DisplayName;
        if (_thumbnailImage != null && item.Thumbnail != null)
            _thumbnailImage.sprite = item.Thumbnail;

        _actionButton?.onClick.RemoveAllListeners();

        if (isEquipped)
        {
            if (_statusText    != null) _statusText.text = "装備中";
            if (_actionButton  != null) _actionButton.interactable = false;
        }
        else if (isUnlocked)
        {
            if (_statusText   != null) _statusText.text = "装備する";
            if (_actionButton != null)
            {
                _actionButton.interactable = true;
                var capturedItem = item;
                _actionButton.onClick.AddListener(() => onEquip(capturedItem));
            }
        }
        else
        {
            if (_statusText   != null) _statusText.text = $"{item.UnlockPrice} 宝石";
            if (_actionButton != null)
            {
                _actionButton.interactable = true;
                var capturedItem = item;
                _actionButton.onClick.AddListener(() => onBuy(capturedItem));
            }
        }
    }
}
