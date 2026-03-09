using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 恒久アップグレードシステム。宝石を消費してプレイヤーのパラメータを永続的に強化する。
///
/// [設計]
///   Singleton。レベルは PlayerPrefs に "Upgrade_{UpgradeId}" キーで int 保存。
///   ゲーム開始時（GameManager が Exploring に入ったとき）に ApplyAllUpgrades() を呼んで
///   SurvivalStats / PlayerMovement / InventorySystem / TorchSystem へ反映する。
///
/// [宝石消費]
///   PlayerCosmeticSaveData を経由して宝石残高を確認・消費する（共通の宝石管理）。
///
/// [パラメータ増加量]
///   MaxHealth    : base 100 + level × 20
///   MaxOxygen    : base 100 + level × 10
///   MaxHunger    : base 100 + level × 10
///   MoveSpeed    : base 6   + level × 0.5
///   JumpForce    : base 7   + level × 0.3
///   TorchFuel    : base 100 + level × 20
///   CarryWeight  : base 30  + level × 5
/// </summary>
public class UpgradeSystem : MonoBehaviour
{
    // ─────────────── Singleton ───────────────

    public static UpgradeSystem Instance { get; private set; }

    // ─────────────── Inspector ───────────────

    [Header("アップグレード定義一覧")]
    [Tooltip("UpgradeDefinition ScriptableObject を登録する")]
    [SerializeField] private List<UpgradeDefinition> _upgrades = new();

    // ─────────────── Unity Lifecycle ───────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─────────────── 公開 API ───────────────

    /// <summary>指定 ID のアップグレードの現在レベルを返す（0 = 未取得）。</summary>
    public int GetLevel(string upgradeId)
        => PlayerPrefs.GetInt(PrefsKey(upgradeId), 0);

    /// <summary>
    /// アップグレードを 1 段階上げる試み。
    /// - 最大レベル到達済みなら false を返す。
    /// - 宝石が不足していれば false を返す。
    /// - 成功したら PlayerPrefs に保存し ApplyAllUpgrades() を呼ぶ。
    /// </summary>
    public bool TryUpgrade(string upgradeId)
    {
        var def = FindDef(upgradeId);
        if (def == null)
        {
            Debug.LogWarning($"[UpgradeSystem] UpgradeId '{upgradeId}' が見つかりません。");
            return false;
        }

        int current = GetLevel(upgradeId);
        if (current >= def.MaxLevel)
        {
            Debug.Log($"[UpgradeSystem] '{upgradeId}' はすでに最大レベルです。");
            return false;
        }

        // GemCostPerLevel の範囲チェック
        if (def.GemCostPerLevel == null || current >= def.GemCostPerLevel.Length)
        {
            Debug.LogWarning($"[UpgradeSystem] '{upgradeId}' の GemCostPerLevel が未設定または範囲外です。");
            return false;
        }

        int cost = def.GemCostPerLevel[current];
        var saveData = PlayerCosmeticSaveData.Instance;
        if (saveData == null)
        {
            Debug.LogWarning("[UpgradeSystem] PlayerCosmeticSaveData が見つかりません。");
            return false;
        }

        // 宝石消費（残高チェック込み）
        if (!saveData.SpendGems(cost))
        {
            Debug.Log($"[UpgradeSystem] 宝石不足。必要: {cost}, 所持: {saveData.Gems}");
            return false;
        }

        // レベル保存
        int newLevel = current + 1;
        PlayerPrefs.SetInt(PrefsKey(upgradeId), newLevel);
        PlayerPrefs.Save();

        Debug.Log($"[UpgradeSystem] '{def.DisplayName}' Lv{newLevel} にアップグレード（宝石 -{cost}）");

        // 即座に反映
        ApplyAllUpgrades();
        return true;
    }

    /// <summary>
    /// 全アップグレードの現在レベルをシーン上の各コンポーネントに反映する。
    /// GameManager.Exploring 開始時と TryUpgrade 成功時に呼ばれる。
    /// </summary>
    public void ApplyAllUpgrades()
    {
        var stats     = FindFirstObjectByType<SurvivalStats>();
        var movement  = FindFirstObjectByType<PlayerMovement>();
        var inventory = FindFirstObjectByType<InventorySystem>();
        var torch     = FindFirstObjectByType<TorchSystem>();

        foreach (var def in _upgrades)
        {
            if (def == null) continue;
            int level = GetLevel(def.UpgradeId);

            switch (def.Type)
            {
                case UpgradeType.MaxHealth:
                    stats?.SetMaxHealth(100f + level * 20f);
                    break;

                case UpgradeType.MaxOxygen:
                    stats?.SetMaxOxygen(100f + level * 10f);
                    break;

                case UpgradeType.MaxHunger:
                    stats?.SetMaxHunger(100f + level * 10f);
                    break;

                case UpgradeType.MoveSpeed:
                    movement?.SetMoveSpeed(6f + level * 0.5f);
                    break;

                case UpgradeType.JumpForce:
                    movement?.SetJumpForce(7f + level * 0.3f);
                    break;

                case UpgradeType.TorchFuel:
                    torch?.SetMaxFuel(100f + level * 20f);
                    break;

                case UpgradeType.CarryWeight:
                    inventory?.SetMaxWeight(30f + level * 5f);
                    break;
            }
        }

        Debug.Log("[UpgradeSystem] アップグレードを全コンポーネントに反映しました。");
    }

    // ─────────────── 内部ヘルパー ───────────────

    private static string PrefsKey(string upgradeId) => $"Upgrade_{upgradeId}";

    private UpgradeDefinition FindDef(string upgradeId)
    {
        foreach (var def in _upgrades)
        {
            if (def != null && def.UpgradeId == upgradeId)
                return def;
        }
        return null;
    }
}
