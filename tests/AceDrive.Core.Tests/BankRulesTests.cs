using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The savings-bank rules pinned against Unity (item 48):
/// rate = streets × consecutive days, the / 10000 divisor, the 1000 saldo
/// floor and the 15/51 POI gates.</summary>
public sealed class BankRulesTests
{
    [Fact]
    public void Rate_MultipliesStreetsByDays()
    {
        Assert.Equal(1, BankRules.Rate(streetCount: 1, daysInARow: 1));
        Assert.Equal(30, BankRules.Rate(streetCount: 6, daysInARow: 5));
        Assert.Equal(0, BankRules.Rate(streetCount: 0, daysInARow: 3));
        // Negative inputs degrade to zero, never negative money.
        Assert.Equal(0, BankRules.Rate(streetCount: -3, daysInARow: 2));
    }

    [Fact]
    public void ComputeInterest_MatchesUnityFormula()
    {
        // 1000 * 1 / 10000 = 0.1 — the fresh driver's first-settle interest.
        Assert.Equal(0.1, BankRules.ComputeInterest(1000.0, 1, 1), precision: 9);
        // 2450.25 * 30 / 10000 = 7.35075 — streets × days on a grown zone.
        Assert.Equal(7.35075, BankRules.ComputeInterest(2450.25, 6, 5), precision: 9);
        // A floored/empty bank adds nothing and never goes negative.
        Assert.Equal(0.0, BankRules.ComputeInterest(0.0, 4, 3));
        Assert.Equal(0.0, BankRules.ComputeInterest(-50.0, 4, 3));
    }

    [Fact]
    public void MaxWithdraw_StopsAtTheSaldoFloor()
    {
        Assert.Equal(500.0, BankRules.MaxWithdraw(1500.0));
        Assert.Equal(0.0, BankRules.MaxWithdraw(1000.0));
        Assert.Equal(0.0, BankRules.MaxWithdraw(900.0)); // never negative
    }

    [Fact]
    public void PlacementGate_FifteenThenFiftyOne()
    {
        Assert.Equal(15, BankRules.RequiredStreetCountFor(0));
        Assert.Equal(66, BankRules.RequiredStreetCountFor(1));
        Assert.Equal(117, BankRules.RequiredStreetCountFor(2));
    }
}
