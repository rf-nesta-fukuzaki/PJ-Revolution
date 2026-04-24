using System;
using System.Collections.Generic;
using UnityEngine;

public enum BasecampShopFlowState
{
    Boot,
    Open,
    Departed
}

public sealed class BasecampShopStateMachine
{
    public BasecampShopFlowState Current { get; private set; } = BasecampShopFlowState.Boot;

    public bool TryTransition(BasecampShopFlowState next)
    {
        if (Current == next) return false;
        if (!IsValidTransition(Current, next)) return false;
        Current = next;
        return true;
    }

    public static bool IsValidTransition(BasecampShopFlowState from, BasecampShopFlowState to)
    {
        return (from, to) switch
        {
            (BasecampShopFlowState.Boot, BasecampShopFlowState.Open) => true,
            (BasecampShopFlowState.Open, BasecampShopFlowState.Departed) => true,
            _ => false
        };
    }
}

public sealed class BasecampShopSession
{
    private readonly IReadOnlyDictionary<string, BasecampShopItemDefinition> _catalogById;
    private readonly Dictionary<string, int> _purchasedCounts = new();
    private readonly BasecampShopStateMachine _stateMachine = new();

    public BasecampShopFlowState CurrentState => _stateMachine.Current;
    public IReadOnlyDictionary<string, int> PurchasedCounts => _purchasedCounts;

    public BasecampShopSession(IReadOnlyDictionary<string, BasecampShopItemDefinition> catalogById)
    {
        if (catalogById == null || catalogById.Count == 0)
            throw new ArgumentException("Shop catalog must have at least one item.", nameof(catalogById));

        _catalogById = catalogById;
        foreach (var entry in _catalogById)
        {
            string id = entry.Key;
            var item = entry.Value;
            if (item == null)
                throw new ArgumentException($"Catalog item is null. id={id}", nameof(catalogById));
            if (!item.TryValidate(out var reason))
                throw new ArgumentException($"Invalid item '{id}'. {reason}", nameof(catalogById));

            _purchasedCounts[id] = 0;
        }

        bool opened = _stateMachine.TryTransition(BasecampShopFlowState.Open);
        Debug.Assert(opened, "[Contract] BasecampShopSession failed to enter Open state.");
    }

    public bool TryBuildPurchaseRequest(
        string itemId,
        int currentBudget,
        out BasecampShopItemDefinition item,
        out string reason)
    {
        item = null;
        reason = string.Empty;

        if (CurrentState != BasecampShopFlowState.Open)
        {
            reason = "ショップは利用できません";
            return false;
        }

        if (!TryResolveItem(itemId, out item, out reason))
            return false;

        if (currentBudget < item.Cost)
        {
            reason = "予算不足です";
            return false;
        }

        return true;
    }

    public bool ConfirmPurchase(string itemId, out string reason)
    {
        reason = string.Empty;

        if (CurrentState != BasecampShopFlowState.Open)
        {
            reason = "ショップは利用できません";
            return false;
        }

        if (!TryResolveItem(itemId, out _, out reason))
            return false;

        _purchasedCounts[itemId] = _purchasedCounts[itemId] + 1;
        return true;
    }

    public bool TryRefund(string itemId, out BasecampShopItemDefinition item, out string reason)
    {
        item = null;
        reason = string.Empty;

        if (CurrentState != BasecampShopFlowState.Open)
        {
            reason = "ショップは利用できません";
            return false;
        }

        if (!TryResolveItem(itemId, out item, out reason))
            return false;

        int current = _purchasedCounts[itemId];
        if (current <= 0)
        {
            reason = "返品できる在庫がありません";
            return false;
        }

        _purchasedCounts[itemId] = current - 1;
        return true;
    }

    public int GetPurchasedCount(string itemId)
    {
        return _purchasedCounts.TryGetValue(itemId, out int count) ? count : 0;
    }

    public bool TryDepart(out string reason)
    {
        if (CurrentState != BasecampShopFlowState.Open)
        {
            reason = "既に出発済みです";
            return false;
        }

        bool moved = _stateMachine.TryTransition(BasecampShopFlowState.Departed);
        if (!moved)
        {
            reason = "出発処理に失敗しました";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private bool TryResolveItem(string itemId, out BasecampShopItemDefinition item, out string reason)
    {
        item = null;
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(itemId))
        {
            reason = "itemId が空です";
            return false;
        }

        if (!_catalogById.TryGetValue(itemId, out item) || item == null)
        {
            reason = $"itemId '{itemId}' はカタログに存在しません";
            return false;
        }

        return true;
    }
}
