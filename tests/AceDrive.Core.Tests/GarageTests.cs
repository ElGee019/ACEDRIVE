using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The garage (PORTPLAN item 30): owned cars, the current car, the
/// catalogue lookup behind each key and what a restart reopens.</summary>
public sealed class GarageTests
{
    private static readonly CarSpec Mx5 = new(
        "ks_mazda_mx5_na", 1990, "Mazda", "mx5 na", Passengers: 1, Class: 9,
        Price: 12000.0, TrunkLitres: 150);

    private static readonly CarSpec Mx5Variant = Mx5 with
    {
        Model = "mx5 na eunos",
        Price = 18000.0,
        Preset = "preset_mx5na_mech_2",
    };

    private static readonly CarSpec Junior = new(
        "ks_alfa_romeo_junior", 2024, "Alfa Romeo", "junior", Passengers: 4, Class: 6,
        Price: 47500.0, TrunkLitres: 400);

    private static Garage New(GarageState? saved = null) =>
        new([Mx5, Mx5Variant, Junior], saved);

    [Fact]
    public void FreshGarage_IsEmpty()
    {
        var garage = New();
        Assert.Empty(garage.Cars);
        Assert.Null(garage.Current);
        Assert.Null(garage.CurrentKey);
        Assert.False(garage.Owns(Mx5.Key));
    }

    [Fact]
    public void FirstCar_BecomesCurrent()
    {
        // Unity GrandmaCarScreen: the starter pick is the car driven.
        var garage = New();
        OwnedCar car = garage.Add(Mx5, week: 3);
        Assert.Equal(Mx5.Key, garage.CurrentKey);
        Assert.Same(car, garage.Current);
        Assert.Equal(0.0, car.Odometer);
        Assert.False(car.HasInsurance);
        Assert.Equal(3, car.PurchaseWeek);
        Assert.True(garage.Owns(Mx5.Key));
    }

    [Fact]
    public void SecondCar_StaysParkedUntilPicked()
    {
        // Unity dealer + BoughtFirstCar: buying adds to the garage, driving
        // the new car is the player's choice on the follow-up screen.
        var garage = New();
        garage.Add(Mx5, week: 1);
        garage.Add(Junior, week: 5);
        Assert.Equal(Mx5.Key, garage.CurrentKey);
        Assert.True(garage.SetCurrent(Junior.Key));
        Assert.Equal(Junior.Key, garage.CurrentKey);
        Assert.Same(Junior.Key, garage.Current!.Key);
    }

    [Fact]
    public void SwitchToUnknownOrCurrentCar_IsRejected()
    {
        var garage = New();
        garage.Add(Mx5, week: 1);
        garage.Add(Junior, week: 2);
        Assert.False(garage.SetCurrent("ks_never_bought"));
        Assert.False(garage.SetCurrent(Mx5.Key)); // already the current car
        Assert.Equal(Mx5.Key, garage.CurrentKey);
    }

    [Fact]
    public void Cars_AreOrderedByKey()
    {
        var garage = New();
        garage.Add(Mx5, week: 1);
        garage.Add(Junior, week: 1);
        garage.Add(Mx5Variant, week: 1);
        Assert.Equal(
            ["ks_alfa_romeo_junior", "ks_mazda_mx5_na", "ks_mazda_mx5_na#preset_mx5na_mech_2"],
            garage.Cars.Select(c => c.Key));
    }

    [Fact]
    public void Keys_AreTheDealerExclusionSet()
    {
        var garage = New();
        garage.Add(Mx5Variant, week: 1);
        Assert.Contains("ks_mazda_mx5_na#preset_mx5na_mech_2", garage.Keys);
        // The other variant of the same folder is still for sale.
        Assert.DoesNotContain("ks_mazda_mx5_na", garage.Keys);
    }

    [Fact]
    public void SpecOf_ResolvesThroughTheCatalogue()
    {
        var garage = New();
        OwnedCar variant = garage.Add(Mx5Variant, week: 1);
        Assert.Equal(18000.0, garage.SpecOf(variant)!.Price);
        Assert.Equal(150, garage.SpecOf(variant)!.TrunkLitres);
    }

    [Fact]
    public void SpecOf_RetiredCar_IsNull()
    {
        // A game update that retires a preset leaves the car owned but
        // unnamed: the page falls back to the raw key, it never throws.
        var garage = New();
        OwnedCar ghost = garage.Add("ks_removed_car", week: 1);
        Assert.Null(garage.SpecOf(ghost));
    }

    [Fact]
    public void Filth_IsPerCar_AndClampsAgainstTheRules()
    {
        // PORTPLAN item 33: Unity keeps one carFilth entry per carId, so dirt
        // on the driven car never touches the parked one.
        var garage = New();
        OwnedCar driven = garage.Add(Mx5, week: 1);
        OwnedCar parked = garage.Add(Junior, week: 2);
        driven.Filth.Add(1);
        driven.Filth.Add(1);
        Assert.Equal(2, driven.Filth.Value);
        Assert.Equal(0, parked.Filth.Value);
        Assert.Equal(FilthRules.Default.MaxFilth, driven.Filth.Max);
    }

    [Fact]
    public void Filth_RidesTheSnapshot()
    {
        var garage = New();
        garage.Add(Mx5, week: 1).Filth.Add(9);
        var resumed = New(garage.Snapshot());
        Assert.Equal(9, resumed.Find(Mx5.Key)!.Filth.Value);
    }

    [Fact]
    public void Filth_ResumesClampedAgainstCustomRules()
    {
        // A pre-rules or hand-edited save clamps instead of resuming broken.
        var rules = new FilthRules(MaxFilth: 5, WashPrice: 40);
        var garage = new Garage([Mx5], new GarageState(null, [
            new OwnedCarState(Mx5.Key, 1, 0.0, false, 90),
        ]), rules);
        Assert.Equal(5, garage.Find(Mx5.Key)!.Filth.Value);
        Assert.Equal(rules, garage.FilthRules);
    }

    [Fact]
    public void Odometer_BanksValidDistanceOnly()
    {
        var garage = New();
        OwnedCar car = garage.Add(Mx5, week: 1);
        car.Drive(12.5);
        car.Drive(0.0);
        car.Drive(-4.0);
        car.Drive(double.NaN);
        car.Drive(double.PositiveInfinity);
        Assert.Equal(12.5, car.Odometer);
    }

    [Fact]
    public void Snapshot_ResumesCarsCurrentAndPerCarState()
    {
        // A restart reopens the same garage (PORTPLAN item 51 interim).
        var garage = New();
        OwnedCar bought = garage.Add(Mx5, week: 2);
        bought.Drive(40.0);
        bought.SetInsurance(true);
        garage.Add(Junior, week: 9);
        garage.SetCurrent(Junior.Key);

        var resumed = New(garage.Snapshot());
        Assert.Equal(
            garage.Cars.Select(c => c.Key), resumed.Cars.Select(c => c.Key));
        Assert.Equal(Junior.Key, resumed.CurrentKey);
        Assert.Equal(40.0, resumed.Find(Mx5.Key)!.Odometer);
        Assert.True(resumed.Find(Mx5.Key)!.HasInsurance);
        Assert.Equal(2, resumed.Find(Mx5.Key)!.PurchaseWeek);
    }

    [Fact]
    public void JobsCompleted_RidesTheSnapshot()
    {
        // The valuation score's per-car job term (PORTPLAN 49, Unity
        // CarJobsService) survives the same restart as the odometer.
        var garage = New();
        OwnedCar car = garage.Add(Mx5, week: 1);
        car.RegisterJob();
        car.RegisterJob();

        var resumed = New(garage.Snapshot());

        OwnedCar again = resumed.Find(Mx5.Key)!;
        Assert.Equal(2, again.JobsCompleted);
        again.RegisterJob();
        Assert.Equal(3, again.JobsCompleted);
    }

    [Fact]
    public void LeaderboardRanking_StartsOnTheTopPlaceholderAndRidesTheSnapshot()
    {
        // The valuation's stretch-board term is per owned car: it starts on the
        // top percentile (no board can measure the car yet), survives a restart
        // and clamps whatever a future board writes.
        var garage = New();
        OwnedCar car = garage.Add(Mx5, week: 1);
        Assert.Equal(OwnedCar.DefaultLeaderboardRanking, car.LeaderboardRanking);

        car.SetLeaderboardRanking(0.42);
        var resumed = New(garage.Snapshot());

        Assert.Equal(0.42, resumed.Find(Mx5.Key)!.LeaderboardRanking, 6);
        // Two copies of one model keep their own value; a bad write degrades to
        // the band the score curve expects.
        OwnedCar variant = resumed.Add(Mx5Variant, week: 2);
        Assert.Equal(OwnedCar.DefaultLeaderboardRanking, variant.LeaderboardRanking);
        variant.SetLeaderboardRanking(4.0);
        Assert.Equal(1.0, variant.LeaderboardRanking);
        variant.SetLeaderboardRanking(double.NaN);
        Assert.Equal(OwnedCar.DefaultLeaderboardRanking, variant.LeaderboardRanking);
    }

    [Fact]
    public void Resume_DropsBrokenRowsAndFixesTheCurrentKey()
    {
        // A hand-edited file (or a store that let something through) degrades
        // to a smaller garage: no key, a duplicate key, a negative odometer,
        // and a current key no car backs.
        var saved = new GarageState("ks_missing", [
            new OwnedCarState("", 1, 0.0, false),
            new OwnedCarState(Mx5.Key, 4, -10.0, false),
            new OwnedCarState(Mx5.Key, 5, 12.0, true),
            new OwnedCarState(Junior.Key, -3, 3.0, false),
        ]);
        var garage = New(saved);
        Assert.Equal([Junior.Key, Mx5.Key], garage.Cars.Select(c => c.Key));
        // The first surviving row of a duplicate key owns the state.
        Assert.Equal(12.0, garage.Find(Mx5.Key)!.Odometer);
        Assert.Equal(0, garage.Find(Junior.Key)!.PurchaseWeek);
        // The current key named a car no row backs, so it fell back to the
        // garage's first car (Unity's garage default).
        Assert.Equal(Junior.Key, garage.CurrentKey);
    }

    [Fact]
    public void Resume_WithoutCurrentKey_KeepsTheFirstCar()
    {
        var garage = New(new GarageState(null, [new OwnedCarState(Mx5.Key, 1, 0.0, false)]));
        Assert.Equal(Mx5.Key, garage.CurrentKey);
    }

    [Fact]
    public void CatalogueWithDuplicateKeys_KeepsTheFirstRow()
    {
        var garage = new Garage([Mx5, Mx5 with { Price = 99000.0 }]);
        Assert.Equal(12000.0, garage.SpecOf(garage.Add(Mx5, week: 1))!.Price);
    }

    [Fact]
    public void Condition_RidesTheSnapshot()
    {
        var garage = New();
        OwnedCar car = garage.Add(Mx5, week: 1);
        car.Drive(3000.0);
        car.Condition.ServicePart("oil_change", odometer: 3000.0);

        var resumed = New(garage.Snapshot());
        OwnedCar back = resumed.Find(Mx5.Key)!;
        // 3000 km on the whole deck: the serviced part resets, the rest is worn.
        Assert.Equal(0.0, back.Condition.PartFraction("oil_change", 3000.0));
        Assert.Equal(1.0, back.Condition.PartFraction("wipers", 3000.0));
        Assert.Equal(3000.0, back.Odometer);
    }

    [Fact]
    public void EngineSchedule_RidesTheSnapshot()
    {
        var garage = New();
        OwnedCar car = garage.Add(Mx5, week: 1);
        Assert.False(car.Engine.IsScheduled);

        car.Engine.EnsureInitialized(new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc));

        var resumed = New(garage.Snapshot());
        Assert.True(resumed.Find(Mx5.Key)!.Engine.IsScheduled);
    }

    [Fact]
    public void Condition_UsesTheGaragesRules()
    {
        // The garage hands the loaded condition table to every car it builds or
        // resumes, so a custom part interval drives the wear.
        var rules = MaintenanceRules.Default with { Parts = [new MaintenancePart("tires", 500.0, 900.0)] };
        var garage = new Garage([Mx5], maintenanceRules: rules);
        OwnedCar car = garage.Add(Mx5, week: 1);
        Assert.Equal(rules, garage.MaintenanceRules);
        Assert.Equal(1.0, car.Condition.PartFraction("tires", 500.0));
    }
}
