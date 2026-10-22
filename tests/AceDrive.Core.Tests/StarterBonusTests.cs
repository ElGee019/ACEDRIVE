using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// Pure-formula tests for the zone/car-decaying Business Starter Bonus
/// (Unity ProvincialStarterBonusTests): 1% per zone km, divided by the owned
/// car count.
/// </summary>
public sealed class StarterBonusTests
{
    [Fact]
    public void ZonePercentage_Is100Under1Km()
    {
        Assert.Equal(100.0, StarterBonus.ZonePercentage(0.5));
        Assert.Equal(100.0, StarterBonus.ZonePercentage(0.99));
    }

    [Fact]
    public void ZonePercentage_DropsOnePerKm()
    {
        Assert.Equal(99.0, StarterBonus.ZonePercentage(1.0));
        Assert.Equal(99.0, StarterBonus.ZonePercentage(1.9));
        Assert.Equal(98.0, StarterBonus.ZonePercentage(2.0));
        Assert.Equal(50.0, StarterBonus.ZonePercentage(50.0));
    }

    [Fact]
    public void ZonePercentage_IsZeroAt100KmAndBeyond()
    {
        Assert.Equal(0.0, StarterBonus.ZonePercentage(100.0));
        Assert.Equal(0.0, StarterBonus.ZonePercentage(150.0));
    }

    [Fact]
    public void ZonePercentage_BrokenLengthsReadAsNoZone()
    {
        // A hand-edited save must never push the percentage out of 0-100.
        Assert.Equal(100.0, StarterBonus.ZonePercentage(-5.0));
        Assert.Equal(100.0, StarterBonus.ZonePercentage(double.NaN));
    }

    [Fact]
    public void Percentage_DividesByCarCount()
    {
        Assert.Equal(100.0, StarterBonus.Percentage(0.5, 1), precision: 9);
        Assert.Equal(50.0, StarterBonus.Percentage(0.5, 2), precision: 9);
        Assert.Equal(33.3333, StarterBonus.Percentage(0.5, 3), precision: 4);
        Assert.Equal(47.5, StarterBonus.Percentage(5.0, 2), precision: 9);
    }

    [Fact]
    public void EmptyGarage_CountsAsOneCar()
    {
        Assert.Equal(100.0, StarterBonus.Percentage(0.5, 0), precision: 9);
    }

    [Fact]
    public void Percentage_IsZeroBeyond100KmRegardlessOfCars()
    {
        Assert.Equal(0.0, StarterBonus.Percentage(120.0, 1));
        Assert.Equal(0.0, StarterBonus.Percentage(120.0, 5));
    }

    [Fact]
    public void Amount_ScalesThePreBonusTotal()
    {
        Assert.Equal(47.5, StarterBonus.Amount(100.0, 5.0, 2), precision: 9);
    }

    [Fact]
    public void Amount_IsZeroAt100KmPlus()
    {
        Assert.Equal(0.0, StarterBonus.Amount(100.0, 100.0, 1));
    }

    [Fact]
    public void Amount_NeverChargesANetNegativeRide()
    {
        Assert.Equal(0.0, StarterBonus.Amount(-37.0, 0.0, 1));
    }
}
