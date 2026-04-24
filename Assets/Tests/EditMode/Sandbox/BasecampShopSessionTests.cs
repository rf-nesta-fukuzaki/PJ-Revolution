using System;
using System.Collections.Generic;
using NUnit.Framework;

public sealed class BasecampShopSessionTests
{
    [Test]
    public void Constructor_Throws_WhenCatalogIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => new BasecampShopSession(new Dictionary<string, BasecampShopItemDefinition>()));
    }

    [Test]
    public void PurchaseFlow_Succeeds_WhenBudgetIsEnough()
    {
        var session = CreateSession();

        bool canPurchase = session.TryBuildPurchaseRequest("shop.short_rope_10m", 10, out var item, out var reason);
        bool committed = session.ConfirmPurchase("shop.short_rope_10m", out var confirmReason);

        Assert.That(canPurchase, Is.True, reason);
        Assert.That(item, Is.Not.Null);
        Assert.That(committed, Is.True, confirmReason);
        Assert.That(session.GetPurchasedCount("shop.short_rope_10m"), Is.EqualTo(1));
    }

    [Test]
    public void PurchaseFlow_Fails_WhenBudgetIsInsufficient()
    {
        var session = CreateSession();

        bool canPurchase = session.TryBuildPurchaseRequest("shop.long_rope_25m", 4, out _, out var reason);

        Assert.That(canPurchase, Is.False);
        Assert.That(reason, Is.EqualTo("予算不足です"));
    }

    [Test]
    public void Refund_Fails_WhenNoPurchasedStock()
    {
        var session = CreateSession();

        bool refunded = session.TryRefund("shop.short_rope_10m", out _, out var reason);

        Assert.That(refunded, Is.False);
        Assert.That(reason, Is.EqualTo("返品できる在庫がありません"));
    }

    [Test]
    public void Depart_BlocksFurtherPurchases()
    {
        var session = CreateSession();

        bool departed = session.TryDepart(out var departReason);
        bool canPurchase = session.TryBuildPurchaseRequest("shop.short_rope_10m", 10, out _, out var purchaseReason);

        Assert.That(departed, Is.True, departReason);
        Assert.That(canPurchase, Is.False);
        Assert.That(purchaseReason, Is.EqualTo("ショップは利用できません"));
    }

    [Test]
    public void StateMachine_RejectsIllegalTransitions()
    {
        var stateMachine = new BasecampShopStateMachine();

        Assert.That(stateMachine.Current, Is.EqualTo(BasecampShopFlowState.Boot));
        Assert.That(stateMachine.TryTransition(BasecampShopFlowState.Departed), Is.False);
        Assert.That(stateMachine.TryTransition(BasecampShopFlowState.Open), Is.True);
        Assert.That(stateMachine.TryTransition(BasecampShopFlowState.Boot), Is.False);
        Assert.That(stateMachine.TryTransition(BasecampShopFlowState.Departed), Is.True);
        Assert.That(stateMachine.TryTransition(BasecampShopFlowState.Open), Is.False);
    }

    private static BasecampShopSession CreateSession()
    {
        var shortRope = new BasecampShopItemDefinition(
            "shop.short_rope_10m",
            "ショートロープ（10m）",
            5,
            1f,
            1,
            80f,
            "test",
            ShopItemType.ShortRope10m,
            true);

        var longRope = new BasecampShopItemDefinition(
            "shop.long_rope_25m",
            "ロングロープ（25m）",
            10,
            2f,
            2,
            70f,
            "test",
            ShopItemType.LongRope25m,
            true);

        return new BasecampShopSession(
            new Dictionary<string, BasecampShopItemDefinition>
            {
                { shortRope.Id, shortRope },
                { longRope.Id, longRope }
            });
    }
}
