using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Receipt + XP golden values for the placeholder ride (8.2 km, tip
/// eligible, one 50 fine, class/rarity 1, street knowledge 100%, fresh XP).</summary>
public sealed class TaxiReceiptTests
{
    private static readonly FareTable Table = FareTable.Default;

    private static TaxiReceipt Placeholder() =>
        TaxiReceiptBuilder.Build(
            8.2, capacity: 1, carClass: 1, carRarity: 1,
            streetKnowledgePct: 100, tipEligible: true, fines: 1,
            Table, currentXp: 0.0);

    [Fact]
    public void PlaceholderRide_MoneyLines()
    {
        TaxiReceipt receipt = Placeholder();
        Assert.Equal(8.2, receipt.DistanceKm);
        Assert.Equal(43.15, receipt.DistanceCharge);
        Assert.Equal(10.0, receipt.Forfait);
        Assert.Equal(53.15, receipt.BaseTotal);
        Assert.Equal(10.63, receipt.Tip);
        Assert.Equal(1, receipt.Fines);
        Assert.Equal(50.0, receipt.FineDeduction);
        Assert.Equal(13.78, receipt.Total);
    }

    [Fact]
    public void PlaceholderRide_TierBreakdown()
    {
        TaxiReceipt receipt = Placeholder();
        Assert.Equal(3, receipt.TierLines.Count);
        Assert.Equal(new ReceiptTier(0.0, 2.0, 6.67, 2.0, 13.34), receipt.TierLines[0]);
        Assert.Equal(new ReceiptTier(2.0, 3.0, 5.67, 3.0, 17.01), receipt.TierLines[1]);
        Assert.Equal(new ReceiptTier(5.0, 5.0, 4.0, 3.2, 12.80), receipt.TierLines[2]);
    }

    [Fact]
    public void PlaceholderRide_XpAward()
    {
        // (8.2 x class 1 x rarity 1) / 100 x SK 100%, no diminishing yet.
        TaxiReceipt receipt = Placeholder();
        Assert.Equal(0.082, receipt.XpEarned, precision: 9);
        Assert.Equal(1, receipt.CarClass);
        Assert.Equal(100, receipt.StreetKnowledgePct);
    }

    [Fact]
    public void HigherCarClass_ScalesXp()
    {
        TaxiReceipt receipt = TaxiReceiptBuilder.Build(
            8.2, 1, carClass: 15, carRarity: 1, 100, true, 1, Table, 0.0);
        Assert.Equal(1.23, receipt.XpEarned, precision: 9);
        // Money is class-blind: the car only moves XP.
        Assert.Equal(13.78, receipt.Total);
    }

    [Fact]
    public void FourSeats_ScalesChargeNotForfaitOrXp()
    {
        // Unity: distanceCharge x capacity, forfait flat, XP class-driven.
        TaxiReceipt receipt = TaxiReceiptBuilder.Build(
            8.2, capacity: 4, carClass: 1, carRarity: 1,
            streetKnowledgePct: 100, tipEligible: true, fines: 1,
            Table, currentXp: 0.0);
        Assert.Equal(4, receipt.Capacity);
        Assert.Equal(172.60, receipt.DistanceCharge);
        Assert.Equal(182.60, receipt.BaseTotal);
        Assert.Equal(36.52, receipt.Tip);
        Assert.Equal(169.12, receipt.Total);
        Assert.Equal(0.082, receipt.XpEarned, precision: 9);
        Assert.Equal(
            [new ReceiptTier(0.0, 2.0, 6.67, 2.0, 53.36),
             new ReceiptTier(2.0, 3.0, 5.67, 3.0, 68.04),
             new ReceiptTier(5.0, 5.0, 4.0, 3.2, 51.20)],
            receipt.TierLines);
    }

    [Fact]
    public void NoTipNoFine_TotalIsBase()
    {
        TaxiReceipt receipt = TaxiReceiptBuilder.Build(
            8.2, 1, 1, 1, 100, tipEligible: false, fines: 0, Table, 0.0);
        Assert.Equal(0.0, receipt.Tip);
        Assert.Equal(0.0, receipt.FineDeduction);
        Assert.Equal(53.15, receipt.Total);
    }

    [Fact]
    public void NoBrand_NoBonus_LinesHidden()
    {
        TaxiReceipt receipt = Placeholder();
        Assert.Equal("", receipt.BrandName);
        Assert.Equal(0.0, receipt.BrandBonus);
        Assert.False(receipt.HasBrandBonus);
    }

    [Fact]
    public void BrandBonus_PaysRateOnBase_AndJoinsTotal()
    {
        // First 8.2 km ride in the brand: 5% of the 53.15 base = 2.66,
        // so the total moves from 13.78 to 16.44.
        TaxiReceipt receipt = TaxiReceiptBuilder.Build(
            8.2, 1, 1, 1, 100, tipEligible: true, fines: 1, Table, 0.0,
            brand: "Mazda", brandDistanceKm: 8.2);
        Assert.Equal("Mazda", receipt.BrandName);
        Assert.Equal(8.2, receipt.BrandDistanceKm);
        Assert.Equal(0.05, receipt.BrandBonusRate, precision: 9);
        Assert.Equal(2.66, receipt.BrandBonus);
        Assert.True(receipt.HasBrandBonus);
        Assert.Equal(16.44, receipt.Total);
    }

    [Fact]
    public void BrandWithoutMileage_PaysNothing()
    {
        TaxiReceipt receipt = TaxiReceiptBuilder.Build(
            8.2, 1, 1, 1, 100, tipEligible: true, fines: 1, Table, 0.0,
            brand: "Mazda", brandDistanceKm: 0.0);
        Assert.Equal(0.0, receipt.BrandBonusRate);
        Assert.Equal(0.0, receipt.BrandBonus);
        Assert.False(receipt.HasBrandBonus);
        Assert.Equal(13.78, receipt.Total);
    }
}
