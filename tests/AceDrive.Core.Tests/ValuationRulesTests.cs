using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Weekly valuation schedule and TTL helpers.</summary>
public sealed class ValuationRulesTests
{
    [Fact]
    public void Wipe_IsDueOnlyOnMondayMidnight()
    {
        Assert.True(ValuationRules.IsWipeDue(dayOfWeek: 2, hour: 0, minute: 0));
        Assert.False(ValuationRules.IsWipeDue(dayOfWeek: 2, hour: 0, minute: 1));
        Assert.False(ValuationRules.IsWipeDue(dayOfWeek: 3, hour: 0, minute: 0));
    }

    [Fact]
    public void Reset_IsOverdue_WhenAMondayPassedWithoutAWipe()
    {
        // Current game day 10 is a Wednesday; the most recent Monday is day 8.
        Assert.True(ValuationRules.IsResetOverdue(
            lastResetGameDay: 3, currentGameDay: 10, currentDayOfWeek: 4));
        Assert.False(ValuationRules.IsResetOverdue(
            lastResetGameDay: 8, currentGameDay: 10, currentDayOfWeek: 4));
        Assert.False(ValuationRules.IsResetOverdue(
            lastResetGameDay: 0, currentGameDay: 0, currentDayOfWeek: 4));
    }

    [Fact]
    public void HouseEntry_PicksOneOfTheProvidedCars()
    {
        string? car = ValuationRules.PickRandomCarId(
            ["a", "b", "c"], _ => 1);
        Assert.Equal("b", car);
        Assert.Null(ValuationRules.PickRandomCarId([], _ => 0));
    }

    [Fact]
    public void Expiry_IsEntryPlusFortyEightHours()
    {
        var entered = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(entered.AddHours(48), ValuationRules.ComputeExpiryUtc(entered));
    }
}
