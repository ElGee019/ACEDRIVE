using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Delivery receipt math (PORTPLAN 16, Unity
/// DeliveryJobHandler.ApplyStoreDeliveryEffects + DeliveryReceiptScreen):
/// tiered distance charge, goods profit and the no-class XP, headless and
/// deterministic against the built-in table fixture.</summary>
public sealed class DeliveryReceiptTests
{
    // 8.2 km walk the first three tiers of the Unity delivery table:
    // 2 @ 20 = 40.00, 3 @ 17 = 51.00, 3.2 @ 12 = 38.40 -> 129.40.
    private static readonly DeliveryReceipt Base = DeliveryReceiptBuilder.Build(
        8.2, paidForGoods: 49.00, receivedForGoods: 63.00,
        DeliveryFareTable.Default, streetKnowledgePct: 100, currentXp: 0.0);

    [Fact]
    public void Charge_WalksOnlyTheTiersTheRunReaches()
    {
        Assert.Equal(3, Base.TierLines.Count);
        Assert.Equal(new ReceiptTier(0.0, 2.0, 20.0, 2.0, 40.00), Base.TierLines[0]);
        Assert.Equal(new ReceiptTier(2.0, 3.0, 17.0, 3.0, 51.00), Base.TierLines[1]);
        Assert.Equal(new ReceiptTier(5.0, 5.0, 12.0, 3.2, 38.40), Base.TierLines[2]);
        Assert.True(Base.TierLines[^1].IsOpen is false);
    }

    [Fact]
    public void FareAndProfit_SumThePage()
    {
        // The charge totals the exact lines above; profit = received - paid
        // + fare (63.00 - 49.00 + 129.40), so the wallet delta IS the total.
        Assert.Equal(129.40, Base.DistanceCharge);
        Assert.Equal(new DeliveryReceipt(
            8.2, Base.TierLines, 129.40, 49.00, 63.00, 143.40,
            Base.XpEarned, 100, "", 0, false, 0, 0.0), Base);
    }

    [Fact]
    public void Xp_FollowsDistanceAndStreetKnowledge_NoClassNoRarity()
    {
        // 8.2 / 100 x street knowledge, then diminishing: at 250 XP the
        // rider is 2 full tiers in (0.98).
        Assert.Equal(0.082, Base.XpEarned, precision: 6);
        DeliveryReceipt scaled = DeliveryReceiptBuilder.Build(
            8.2, 0.0, 0.0, DeliveryFareTable.Default, streetKnowledgePct: 50, currentXp: 250.0);
        Assert.Equal(8.2 / 100.0 * 0.5 * 0.98, scaled.XpEarned, precision: 10);
    }

    [Fact]
    public void NoOwnedHub_HidesFriendshipAndCredP()
    {
        // Growing category-less stores still settle; the receipt just runs
        // without the hub blocks.
        Assert.False(Base.HasFriendship);
        Assert.False(Base.HasCredP);
        Assert.Equal("", Base.HubOwnerName);
        Assert.Equal(0, Base.CredPEarned);
    }

    [Fact]
    public void OwnedHub_CarriesFriendshipAndCredP()
    {
        var receipt = DeliveryReceiptBuilder.Build(
            8.2, 49.00, 63.00, DeliveryFareTable.Default, 100, 0.0,
            friendship: new FriendshipResult("Elena Marchetti", 1, false),
            credPEarned: 3, totalCredP: 103.0);
        Assert.True(receipt.HasFriendship);
        Assert.Equal("Elena Marchetti", receipt.HubOwnerName);
        Assert.Equal(1, receipt.FriendshipLevel);
        Assert.False(receipt.FriendshipTierIncreased);
        Assert.True(receipt.HasCredP);
        Assert.Equal(3, receipt.CredPEarned);
        Assert.Equal(103.0, receipt.TotalCredP);
    }

    [Fact]
    public void TopTier_AbsorbsEverythingAboveTheTable()
    {
        // 60 km: the 40 km @ 6 wide tier (122.40 after the first 5 km) plus
        // the open 3/km tail.
        DeliveryReceipt long_ = DeliveryReceiptBuilder.Build(
            60.0, 0.0, 0.0, DeliveryFareTable.Default, 100, 0.0);
        Assert.Equal(5, long_.TierLines.Count);
        double expected = 40.00 + 51.00 + 60.00 + 240.00 + Math.Round(10.0 * 3.0, 2, MidpointRounding.AwayFromZero);
        Assert.Equal(expected, long_.DistanceCharge);
    }

    [Fact]
    public void NoDistance_FreePickup_NoCharge()
    {
        // A zero-distance run (the future driving slice's backfill) still
        // trades the goods, at no fare.
        DeliveryReceipt receipt = DeliveryReceiptBuilder.Build(
            0.0, 49.00, 63.00, DeliveryFareTable.Default, 100, 0.0);
        Assert.Empty(receipt.TierLines);
        Assert.Equal(0.0, receipt.DistanceCharge);
        Assert.Equal(14.00, receipt.TotalEarned);
    }
}
