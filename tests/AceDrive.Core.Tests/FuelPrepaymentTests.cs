using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The roadside repair's prepaid tank (PORTPLAN 38): EVO's tank is
/// read-only, so the paid litres come off the next fill for the same car and
/// never fuel another one.</summary>
public sealed class FuelPrepaymentTests
{
    [Fact]
    public void Apply_SplitsTheFillBetweenTheCreditAndThePump()
    {
        FuelPrepayResult split = FuelPrepayment.Apply(
            fillLitres: 50.0, prepaidLitres: 35.0, "car-a", "car-a");

        Assert.Equal(15.0, split.BillableLitres, 6);
        Assert.Equal(35.0, split.PrepaidLitres, 6);
    }

    [Fact]
    public void Apply_CapsTheCreditAtTheFill()
    {
        FuelPrepayResult split = FuelPrepayment.Apply(
            fillLitres: 10.0, prepaidLitres: 35.0, "car-a", "car-a");

        Assert.Equal(0.0, split.BillableLitres, 6);
        Assert.Equal(10.0, split.PrepaidLitres, 6);
    }

    [Fact]
    public void Apply_DoesNotFuelAnotherCarForFree()
    {
        FuelPrepayResult split = FuelPrepayment.Apply(
            fillLitres: 40.0, prepaidLitres: 35.0, "car-a", "car-b");

        Assert.Equal(40.0, split.BillableLitres, 6);
        Assert.Equal(0.0, split.PrepaidLitres, 6);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void Apply_LeavesTheWholeFillBillableWithoutACredit(double prepaid)
    {
        FuelPrepayResult split = FuelPrepayment.Apply(40.0, prepaid, "car-a", "car-a");

        Assert.Equal(40.0, split.BillableLitres, 6);
        Assert.Equal(0.0, split.PrepaidLitres, 6);
    }

    [Fact]
    public void ReceiptBuilder_TakesThePrepaidLitresOffTheBill()
    {
        FuelReceipt receipt = FuelReceiptBuilder.Build(
            litres: 50.0, pricePerLitre: 1.65, free: false, friendship: null,
            prepaidLitres: 50.0);

        Assert.Equal(0.0, receipt.Total, 6);
        Assert.True(receipt.HasPrepaid);
        Assert.False(receipt.Free);
    }

    [Fact]
    public void ReceiptBuilder_BillsOnlyTheRemainder()
    {
        FuelReceipt receipt = FuelReceiptBuilder.Build(
            litres: 50.0, pricePerLitre: 2.0, free: false, friendship: null,
            prepaidLitres: 20.0);

        Assert.Equal(60.0, receipt.Total, 6);
        Assert.Equal(20.0, receipt.PrepaidLitres, 6);
    }
}
