using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Car filth (PORTPLAN item 33, port of Unity CarWashService +
/// PlayerData.carFilth): +1 per completed job clamped at 25, a wash clears it,
/// the pickup refusal at the cap, and the MapScreen HUD bands.</summary>
public sealed class FilthTests
{
    [Fact]
    public void Default_KeepsTheUnityNumbers()
    {
        Assert.Equal(25, FilthRules.Default.MaxFilth);
        Assert.Equal(50, FilthRules.Default.WashPrice);
    }

    [Fact]
    public void FreshCar_IsClean()
    {
        var filth = new CarFilth(FilthRules.Default);
        Assert.Equal(0, filth.Value);
        Assert.Equal(25, filth.Max);
        Assert.False(filth.RefusesPickup);
        Assert.False(filth.IsAmber);
    }

    [Fact]
    public void Add_IsOnePerJob_ClampedAtTheCap()
    {
        var filth = new CarFilth(FilthRules.Default);
        for (int i = 1; i <= 30; i++)
        {
            filth.Add(1);
            Assert.Equal(Math.Min(i, 25), filth.Value);
        }
    }

    [Fact]
    public void Add_IgnoresNonPositiveAmounts()
    {
        var filth = new CarFilth(FilthRules.Default);
        filth.Add(0);
        filth.Add(-3);
        Assert.Equal(0, filth.Value);
    }

    [Fact]
    public void Wash_ClearsTheCar()
    {
        var filth = new CarFilth(FilthRules.Default);
        filth.Add(12);
        filth.Wash();
        Assert.Equal(0, filth.Value);
        Assert.False(filth.RefusesPickup);
        Assert.False(filth.IsAmber);
    }

    [Fact]
    public void RefusesPickup_OnlyAtTheCap()
    {
        // Unity TaxiJobLifecycleHandler: GetCarFilth >= 25 refuses the pickup.
        var filth = new CarFilth(FilthRules.Default);
        filth.Add(24);
        Assert.False(filth.RefusesPickup);
        filth.Add(1);
        Assert.True(filth.RefusesPickup);
    }

    [Fact]
    public void IsAmber_KeepsUnitysHudBand()
    {
        // Unity MapScreen.UpdateFilthDisplay: 15..24 orange, 25 mint.
        Assert.False(new CarFilth(FilthRules.Default, 14).IsAmber);
        Assert.True(new CarFilth(FilthRules.Default, 15).IsAmber);
        Assert.True(new CarFilth(FilthRules.Default, 24).IsAmber);
        Assert.False(new CarFilth(FilthRules.Default, 25).IsAmber);
        Assert.True(new CarFilth(FilthRules.Default, 25).RefusesPickup);
    }

    [Fact]
    public void Resume_ClampsTheSavedReading()
    {
        // A hand-edited or pre-rules save never resumes negative or past the cap.
        Assert.Equal(0, new CarFilth(FilthRules.Default, -3).Value);
        Assert.Equal(25, new CarFilth(FilthRules.Default, 90).Value);
    }

    [Fact]
    public void CustomRules_DriveTheCapAndWashPrice()
    {
        var rules = new FilthRules(MaxFilth: 3, WashPrice: 10);
        var filth = new CarFilth(rules);
        filth.Add(5);
        Assert.Equal(3, filth.Value);
        Assert.Equal(3, filth.Max);
        Assert.Equal(10, rules.WashPrice);
        Assert.True(filth.RefusesPickup);
    }
}
