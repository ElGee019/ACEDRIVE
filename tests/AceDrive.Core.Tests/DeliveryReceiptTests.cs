using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Delivery receipt math (PORTPLAN 16, Unity
/// DeliveryJobHandler.ApplyStoreDeliveryEffects + DeliveryReceiptScreen):
/// tiered distance charge, goods profit and the taxi-rate XP at a fifth,
/// headless and deterministic against the built-in table fixture.</summary>
public sealed class DeliveryReceiptTests
{
    // 8.2 km walk the first three tiers of the Unity delivery table:
    // 2 @ 20 = 40.00, 3 @ 17 = 51.00, 3.2 @ 12 = 38.40 -> 129.40.
    private static readonly DeliveryReceipt Base = DeliveryReceiptBuilder.Build(
        8.2, paidForGoods: 49.00, receivedForGoods: 63.00,
        DeliveryFareTable.Default, carClass: 10, carRarity: 1,
        streetKnowledgePct: 100, currentXp: 0.0);

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
            Base.XpEarned, 10, 1, 100, "", 0, false, 0, 0.0), Base);
    }

    [Fact]
    public void Xp_IsTheTaxiRateAtAFifth()
    {
        // (8.2 x class 10 x rarity 1) / 100 x SK 100% x 0.2, then diminishing:
        // at 250 XP the rider is 2 full tiers in (0.98).
        Assert.Equal(0.164, Base.XpEarned, precision: 6);
        DeliveryReceipt scaled = DeliveryReceiptBuilder.Build(
            8.2, 0.0, 0.0, DeliveryFareTable.Default, carClass: 10, carRarity: 1,
            streetKnowledgePct: 50, currentXp: 250.0);
        Assert.Equal(8.2 * 10 / 100.0 * 0.5 * 0.2 * 0.98, scaled.XpEarned, precision: 10);
    }

    [Fact]
    public void NoAwardedXp_KeepsTheRunMoneyButZerosXp()
    {
        // An unowned car still settles the goods and fare, but the caller
        // withholds the player XP award.
        DeliveryReceipt receipt = DeliveryReceiptBuilder.Build(
            8.2, 49.00, 63.00, DeliveryFareTable.Default, 10, 1, 100, 0.0,
            awardsXp: false);
        Assert.Equal(143.40, receipt.TotalEarned);
        Assert.Equal(0.0, receipt.XpEarned);
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
            8.2, 49.00, 63.00, DeliveryFareTable.Default, 10, 1, 100, 0.0,
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
            60.0, 0.0, 0.0, DeliveryFareTable.Default, 10, 1, 100, 0.0);
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
            0.0, 49.00, 63.00, DeliveryFareTable.Default, 10, 1, 100, 0.0);
        Assert.Empty(receipt.TierLines);
        Assert.Equal(0.0, receipt.DistanceCharge);
        Assert.Equal(14.00, receipt.TotalEarned);
    }

    [Fact]
    public void ProfitBoost_ScalesTheGoodsOnly_AndFlagsTheCard()
    {
        // Delivery Profit Boost: 20% on the goods payout, never on the fare.
        DeliveryReceipt boosted = DeliveryReceiptBuilder.Build(
            8.2, 49.00, 63.00, DeliveryFareTable.Default, 10, 1, 100, 0.0,
            profitBoostMultiplier: 1.2);

        Assert.True(boosted.ProfitBoostApplied);
        Assert.True(boosted.HasProfitBoost);
        Assert.Equal(0.2, boosted.ProfitBoostRate, precision: 9);
        Assert.Equal(75.60, boosted.ReceivedForGoods);
        Assert.Equal(129.40, boosted.DistanceCharge);
        Assert.Equal(156.00, boosted.TotalEarned);
    }

    [Fact]
    public void StarterBonus_PaysTheZoneRateOnTheRunsProfit()
    {
        // Unity's delivery starter bonus (PORTPLAN 47): the taxi rate on the
        // profit base (141.40 above) rather than the wholesale gross. A 1.929 km
        // zone pays 99%, so the run totals 143.40 + 141.97.
        DeliveryReceipt receipt = DeliveryReceiptBuilder.Build(
            8.2, 49.00, 63.00, DeliveryFareTable.Default, 10, 1, 100, 0.0,
            zoneKm: 1.929, ownedCars: 1);

        Assert.True(receipt.HasStarterBonus);
        Assert.Equal(99.0, receipt.StarterBonusRate, precision: 9);
        Assert.Equal(141.97, receipt.StarterBonus);
        Assert.Equal(285.37, receipt.TotalEarned);
    }

    [Fact]
    public void StarterBonus_SplitsByOwnedCars()
    {
        // The same run in a two-car garage: 99% / 2 = 49.5% of 143.40.
        DeliveryReceipt receipt = DeliveryReceiptBuilder.Build(
            8.2, 49.00, 63.00, DeliveryFareTable.Default, 10, 1, 100, 0.0,
            zoneKm: 1.929, ownedCars: 2);

        Assert.Equal(49.5, receipt.StarterBonusRate, precision: 9);
        Assert.Equal(70.98, receipt.StarterBonus);
        Assert.Equal(214.38, receipt.TotalEarned);
    }

    [Fact]
    public void StarterBonus_NeverPaysOnAWholesaleLoss()
    {
        // Received 63, paid 100, no fare: the profit base clamps at 0, so a
        // losing run earns no boost and the loss stays a loss.
        DeliveryReceipt receipt = DeliveryReceiptBuilder.Build(
            0.0, 100.00, 63.00, DeliveryFareTable.Default, 10, 1, 100, 0.0,
            zoneKm: 1.929, ownedCars: 1);

        Assert.False(receipt.HasStarterBonus);
        Assert.Equal(0.0, receipt.StarterBonus);
        Assert.Equal(-37.00, receipt.TotalEarned);
    }

    [Fact]
    public void WithoutZone_NoStarterBonus()
    {
        // No zone yet (null): the row stays off and the total is the plain
        // profit, exactly like the taxi receipt's without-zone case.
        Assert.False(Base.HasStarterBonus);
        Assert.Equal(0.0, Base.StarterBonusRate);
        Assert.Equal(0.0, Base.StarterBonus);
        Assert.Equal(143.40, Base.TotalEarned);
    }
}
