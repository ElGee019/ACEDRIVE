using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The dry-tank quote (PORTPLAN 38, Unity's
/// CalculateOutOfFuelRepairCost): a full tank at the pump price plus the
/// mechanic's flat fee, clamped and rounded for the CR ledger.</summary>
public sealed class FuelOutageTests
{
    [Fact]
    public void Quote_PricesTheMissingLitresAndTheServiceFee()
    {
        FuelOutage outage = FuelOutage.Quote(
            tankLitres: 50.0, fuelLitres: 0.0, pricePerLitre: 1.65, serviceFee: 100.0);

        Assert.Equal(50.0, outage.LitresNeeded, 6);
        Assert.Equal(182.50, outage.RepairCost, 6);
    }

    [Fact]
    public void Quote_OnlyChargesWhatTheTankStillNeeds()
    {
        FuelOutage outage = FuelOutage.Quote(
            tankLitres: 50.0, fuelLitres: 12.5, pricePerLitre: 2.0, serviceFee: 100.0);

        Assert.Equal(37.5, outage.LitresNeeded, 6);
        Assert.Equal(175.0, outage.RepairCost, 6);
    }

    [Theory]
    [InlineData(-5.0)]
    [InlineData(double.NaN)]
    public void Quote_ClampsAnImpossibleReadingToZeroLitres(double fuel)
    {
        FuelOutage outage = FuelOutage.Quote(50.0, fuel, 1.65, 100.0);

        Assert.Equal(50.0, outage.LitresNeeded, 6);
        Assert.Equal(182.50, outage.RepairCost, 6);
    }

    [Fact]
    public void RepairCost_RoundsAwayFromZeroLikeTheFuelReceipt()
    {
        FuelOutage outage = new(LitresNeeded: 0.333, PricePerLitre: 1.65, ServiceFee: 100.0);

        Assert.Equal(100.55, outage.RepairCost, 6);
    }
}
