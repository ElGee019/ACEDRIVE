using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>The poll's accident feed (FleetService.ObserveAccident): a jump in
/// EVO's collision damage is billed to the driven car at its impact-speed share
/// of the catalogue value, the crashes of one ride accumulate until the car is
/// written off, and a free-roam loaner has no row to bill.</summary>
public sealed class FleetAccidentTests
{
    private static readonly DateTime T0 = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

    private static readonly CarSpec Car = new(
        "ks_test_car", 2020, "Test", "Car",
        Passengers: 2, Class: 16, Price: 40000.0, TopSpeedKmh: 240);

    private sealed record Rig(FleetService Fleet, Garage Garage);

    private static Rig New(string? liveCar = null)
    {
        var clock = new FakeClock();
        var garage = new Garage([Car], new GarageState(Car.Key, [new OwnedCarState(Car.Key, 1, 0.0)]));
        var brands = new FakeCarBrandSource
        {
            CurrentIdentity = liveCar is null ? null : new CarIdentity(liveCar, null),
        };
        var fleet = new FleetService(
            garage, [Car], new GameClock(clock, clock.UtcNow),
            new DriverWallet(new DriverState(0.0, 0.0)), InsuranceRules.Default, new NullLogger(),
            brands, new FakeContentSource(TestSession.EmptyContent));
        return new Rig(fleet, garage);
    }

    private static TelemetrySample Sample(double speedKmh, double damage) =>
        new(1, 0f, 0f, 2, 1000, 0f, (float)speedKmh, CarDamage: (float)damage);

    [Fact]
    public void AFirstSampleWithDamage_OnlyPrimesTheBaseline()
    {
        Rig rig = New();

        Assert.Null(rig.Fleet.ObserveAccident(Sample(0.0, 42.0), T0));
        Assert.Equal(0.0, rig.Garage.Current!.BodyDamage, 6);
    }

    // One crash through the feed: a prime sample at the pre-hit speed, then the
    // damaged sample. Advances the running EVO counter by ten units.
    private static AccidentDamage? Crash(
        Rig rig, ref double damage, double speedKmh, DateTime at)
    {
        rig.Fleet.ObserveAccident(Sample(speedKmh, damage), at);
        damage += 10.0;
        return rig.Fleet.ObserveAccident(Sample(0.0, damage), at.AddMilliseconds(100));
    }

    [Fact]
    public void AHalfTopSpeedHit_BanksATwentiethOfTheCarValue()
    {
        Rig rig = New();
        rig.Fleet.ObserveAccident(Sample(120.0, 0.0), T0);

        AccidentDamage? hit = rig.Fleet.ObserveAccident(Sample(0.0, 90.0), T0.AddMilliseconds(100));

        Assert.NotNull(hit);
        Assert.Equal(120.0, hit.ImpactSpeedKmh, 6);
        Assert.Equal(0.05, hit.RepairFraction, 6);
        Assert.Equal(2000.0, hit.RepairCost, 6);
        Assert.Equal(0.05, hit.TotalDamageFraction, 6);
        Assert.Equal(0.05, rig.Garage.Current!.BodyDamage, 6);
    }

    [Fact]
    public void TenTopSpeedHits_TotalTheCar_AndTheEleventhBillsNothing()
    {
        Rig rig = New();
        double damage = 0.0;
        DateTime at = T0;
        for (int i = 0; i < 10; i++)
        {
            Assert.NotNull(Crash(rig, ref damage, 240.0, at));
            at = at.AddSeconds(2);
        }

        AccidentDamage? eleventh = Crash(rig, ref damage, 240.0, at);

        Assert.NotNull(eleventh);
        Assert.Equal(1.0, rig.Garage.Current!.BodyDamage, 6);
        // The car is already written off, so this crash adds nothing to the
        // bill: the total can never pass the car's own price.
        Assert.Equal(0.0, eleventh.RepairCost, 6);
        Assert.Equal(40000.0, Car.Price * rig.Garage.Current!.BodyDamage, 6);
    }

    [Fact]
    public void SeveralHalfSpeedHits_AccumulateAndTheBillFollowsTheDamage()
    {
        Rig rig = New();
        double damage = 0.0;
        DateTime at = T0;
        AccidentDamage first = Crash(rig, ref damage, 120.0, at)!;
        at = at.AddSeconds(2);
        AccidentDamage second = Crash(rig, ref damage, 120.0, at)!;
        at = at.AddSeconds(2);
        AccidentDamage third = Crash(rig, ref damage, 120.0, at)!;

        Assert.Equal(2000.0, first.RepairCost, 6);
        Assert.Equal(2000.0, second.RepairCost, 6);
        Assert.Equal(0.15, third.TotalDamageFraction, 6);
        Assert.Equal(6000.0, first.RepairCost + second.RepairCost + third.RepairCost, 6);
        Assert.Equal(6000.0, Car.Price * rig.Garage.Current!.BodyDamage, 6);
    }

    [Fact]
    public void AFreeRoamLoaner_HasNoGarageRowToBill()
    {
        Rig rig = New("ks_somebody_elses_car");
        rig.Fleet.ObserveAccident(Sample(120.0, 0.0), T0);

        Assert.Null(rig.Fleet.ObserveAccident(Sample(0.0, 90.0), T0.AddMilliseconds(100)));
        Assert.Equal(0.0, rig.Garage.Current!.BodyDamage, 6);
    }
}
