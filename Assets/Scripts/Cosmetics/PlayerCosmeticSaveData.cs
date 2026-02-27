using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーのコスメデータをローカルに保存・読み込みする MonoBehaviour。
/// PlayerPrefs に JSON 形式で保存し、シーン横断でデータを永続化する。
///
/// [保存内容]
///   - アンロック済みアイテム ID リスト
///   - 所持宝石数
///   - 各カテゴリの現在装備中 ID
///
/// [利用フロー]
///   1. タイトル/ロビーシーンで LoadData() を呼び、アンロック状況を復元する
///   2. ゲーム開始時に ApplyToPlayer() でキャラクターに装備を反映する
///   3. ショップで購入・装備変更のたびに SaveData() を呼ぶ
///
/// [設計方針]
///   - このクラスはローカルのセーブ/ロードのみを担当する
///   - ネットワーク同期は PlayerCosmetics.RequestEquip() に委ねる
/// </summary>
public class PlayerCosmeticSaveData : MonoBehaviour
{
    // ─────────────── 定数 ───────────────

    private const string SaveKey = "PlayerCosmeticSaveData_v1";

    // ─────────────── Inspector ───────────────

    [Header("依存参照")]
    [Tooltip("全アイテム定義。デフォルトアイテムの初期解放に使用する")]
    [SerializeField] private CosmeticDatabase _database;

    // ─────────────── 内部状態 ───────────────

    private SavePayload _payload = new();

    // ─────────────── 公開プロパティ ───────────────

    /// <summary>現在の所持宝石数。</summary>
    public int Gems => _payload.gems;

    /// <summary>アンロック済みアイテム ID の読み取り専用セット。</summary>
    public IReadOnlyCollection<string> UnlockedIds => _payload.unlockedIds;

    // ─────────────── Unity Lifecycle ───────────────

    private void Awake()
    {
        LoadData();
    }

    // ─────────────── 公開 API ───────────────

    /// <summary>
    /// PlayerPrefs からセーブデータを読み込む。
    /// データが存在しない場合はデフォルトアイテムをアンロック済みに初期化する。
    /// </summary>
    public void LoadData()
    {
        string json = PlayerPrefs.GetString(SaveKey, string.Empty);
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                _payload = JsonUtility.FromJson<SavePayload>(json) ?? new SavePayload();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerCosmeticSaveData] セーブデータ読み込み失敗: {e.Message}。初期化します。");
                _payload = new SavePayload();
            }
        }
        else
        {
            _payload = new SavePayload();
        }

        // デフォルトアイテムが未登録なら追加する（初回起動・データ破損時の安全策）
        if (_database != null)
        {
            var defaults = _database.GetAllDefaultIds();
            foreach (var kv in defaults)
            {
                string defaultId = kv.Value;
                if (!string.IsNullOrEmpty(defaultId) && !_payload.unlockedIds.Contains(defaultId))
                    _payload.unlockedIds.Add(defaultId);

                // 装備中が未設定ならデフォルトを適用
                CosmeticCategory cat = kv.Key;
                if (string.IsNullOrEmpty(GetEquipped(cat)) && !string.IsNullOrEmpty(defaultId))
                    SetEquipped(cat, defaultId);
            }
        }

        Debug.Log($"[PlayerCosmeticSaveData] データ読み込み完了: " +
                  $"宝石={_payload.gems}, アンロック数={_payload.unlockedIds.Count}");
    }

    /// <summary>
    /// 現在の状態を PlayerPrefs に保存する。
    /// 購入・装備変更後に必ず呼ぶ。
    /// </summary>
    public void SaveData()
    {
        string json = JsonUtility.ToJson(_payload);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
        Debug.Log($"[PlayerCosmeticSaveData] 保存完了: gems={_payload.gems}");
    }

    /// <summary>
    /// 指定アイテムがアンロック済みかを返す。
    /// デフォルトアイテムは常に true を返す。
    /// </summary>
    public bool IsUnlocked(string itemId)
    {
        return _payload.unlockedIds.Contains(itemId);
    }

    /// <summary>
    /// 宝石を消費してアイテムをアンロックする。
    /// 宝石が不足している場合または既にアンロック済みの場合は false を返す。
    /// 成功時は SaveData() を自動呼び出しする。
    /// </summary>
    /// <param name="itemId">アンロックするアイテム ID</param>
    /// <param name="price">必要な宝石数</param>
    /// <returns>アンロック成功なら true</returns>
    public bool TryUnlock(string itemId, int price)
    {
        if (_payload.unlockedIds.Contains(itemId))
        {
            Debug.Log($"[PlayerCosmeticSaveData] {itemId} は既にアンロック済みです");
            return false;
        }
        if (_payload.gems < price)
        {
            Debug.Log($"[PlayerCosmeticSaveData] 宝石不足: 必要={price}, 所持={_payload.gems}");
            return false;
        }

        _payload.gems -= price;
        _payload.unlockedIds.Add(itemId);
        SaveData();
        Debug.Log($"[PlayerCosmeticSaveData] アンロック成功: {itemId} (-{price}宝石, 残={_payload.gems})");
        return true;
    }

    /// <summary>
    /// 宝石を加算する。GameManager.CollectedGems の変化分を渡す。
    /// SaveData() を自動呼び出しする。
    /// </summary>
    public void AddGems(int amount)
    {
        _payload.gems += amount;
        SaveData();
        Debug.Log($"[PlayerCosmeticSaveData] 宝石 +{amount} → 合計 {_payload.gems}");
    }

    /// <summary>
    /// 指定カテゴリの現在装備中アイテム ID を返す。未設定なら空文字。
    /// </summary>
    public string GetEquipped(CosmeticCategory category)
    {
        return category switch
        {
            CosmeticCategory.Hat       => _payload.equippedHat,
            CosmeticCategory.Pickaxe   => _payload.equippedPickaxe,
            CosmeticCategory.TorchSkin => _payload.equippedTorchSkin,
            CosmeticCategory.Accessory => _payload.equippedAccessory,
            _                          => string.Empty,
        };
    }

    /// <summary>
    /// ローカルセーブデータの装備 ID を更新する。
    /// ネットワーク同期は行わない（呼び出し側が PlayerCosmetics.RequestEquip を別途呼ぶこと）。
    /// SaveData() を自動呼び出しする。
    /// </summary>
    public void SetEquipped(CosmeticCategory category, string itemId)
    {
        switch (category)
        {
            case CosmeticCategory.Hat:       _payload.equippedHat       = itemId; break;
            case CosmeticCategory.Pickaxe:   _payload.equippedPickaxe   = itemId; break;
            case CosmeticCategory.TorchSkin: _payload.equippedTorchSkin = itemId; break;
            case CosmeticCategory.Accessory: _payload.equippedAccessory = itemId; break;
        }
        SaveData();
    }

    /// <summary>
    /// ゲーム開始時にプレイヤーの PlayerCosmetics へ保存済みの装備を適用する。
    /// プレイヤー生成後に呼ぶ。
    /// </summary>
    /// <param name="cosmetics">対象プレイヤーの PlayerCosmetics</param>
    public void ApplyToPlayer(PlayerCosmetics cosmetics)
    {
        if (cosmetics == null) return;

        foreach (CosmeticCategory cat in Enum.GetValues(typeof(CosmeticCategory)))
        {
            string id = GetEquipped(cat);
            if (!string.IsNullOrEmpty(id))
                cosmetics.RequestEquip(cat, id);
        }

        Debug.Log("[PlayerCosmeticSaveData] プレイヤーへ装備反映完了");
    }

    /// <summary>
    /// セーブデータを完全リセットする（デバッグ用）。
    /// </summary>
    [ContextMenu("セーブデータをリセット")]
    public void ResetData()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        _payload = new SavePayload();
        LoadData();
        Debug.Log("[PlayerCosmeticSaveData] セーブデータをリセットしました");
    }

    // ─────────────── 内部 JSON 構造 ───────────────

    /// <summary>PlayerPrefs に JSON でシリアライズする内部データ構造。</summary>
    [Serializable]
    private class SavePayload
    {
        public int          gems             = 0;
        public List<string> unlockedIds      = new();
        public string       equippedHat       = string.Empty;
        public string       equippedPickaxe   = string.Empty;
        public string       equippedTorchSkin = string.Empty;
        public string       equippedAccessory = string.Empty;
    }
}
