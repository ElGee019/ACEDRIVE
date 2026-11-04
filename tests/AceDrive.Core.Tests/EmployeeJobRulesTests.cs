using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The employee job balance numbers: a ride is its drive plus the two-hour
/// handling block and pays its distance, while a delivery pays one CR per unit
/// its refill adds. Ported from LATESTDRIVEBS2 EmployeeService.
/// </summary>
public sealed class EmployeeJobRulesTests
{
    [Fact]
    public void RideMinutes_AddsTheHandlingBlockToTheDrive()
    {
        // 4.2 km at 30 km/h is 8.4 minutes, rounded to 8, plus 120 handling.
        Assert.Equal(128, EmployeeJobRules.RideMinutes(4.2));
    }

    [Fact]
    public void RideMinutes_NeverDropsBelowTheMinimum()
    {
        Assert.Equal(
            EmployeeJobRules.HandlingMinutes,
            EmployeeJobRules.RideMinutes(0.0));
    }

    [Fact]
    public void RidePay_PaysTheDistance()
    {
        Assert.Equal(4.2, EmployeeJobRules.RidePay(4.2), precision: 10);
        Assert.Equal(0.0, EmployeeJobRules.RidePay(-3.0));
    }

    [Fact]
    public void RidePay_IgnoresANonFiniteDistance()
    {
        Assert.Equal(0.0, EmployeeJobRules.RidePay(double.NaN));
    }

    [Fact]
    public void DeliveryPay_IsOneCrPerUnitAdded()
    {
        Assert.Equal(15.0, EmployeeJobRules.DeliveryPay(15), precision: 10);
        Assert.Equal(0.0, EmployeeJobRules.DeliveryPay(-2));
    }

    [Fact]
    public void IsRefillPreferred_OnlyWhileTheStoreIsAtMostEightyPercentFull()
    {
        var atCap = Store(stocked: 20);
        var eighty = Store(stocked: 16);
        var overEighty = Store(stocked: 17);
        var empty = Store(stocked: 0);

        Assert.False(EmployeeJobRules.CanRefill(atCap));
        Assert.True(EmployeeJobRules.IsRefillPreferred(eighty));
        Assert.False(EmployeeJobRules.IsRefillPreferred(overEighty));
        Assert.True(EmployeeJobRules.CanRefill(overEighty));
        Assert.True(EmployeeJobRules.IsRefillPreferred(empty));
    }

    private static DeliveryStore Store(int stocked) =>
        new("store", "Store", "Owner", stocked, UnitCapacity: 20, PricePerUnit: 1.0);
}
