using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The trip-start breakdown both job flows take (PORTPLAN 39): the roll's
/// gates (onboarding, a car at all, a stranded engine) and the tow fee the
/// mechanic charges when the trip is cut.
/// </summary>
public sealed class TripBreakdownsTests
{
    private static readonly CarSpec Car = new(
        "ks_test_car", 2020, "Test", "Car", Passengers: 2, Class: 16, Price: 12000.0, EngineCc: 2000);

    // One part and a 100 % ceiling: a fully worn car rolls a guaranteed
    // breakdown, so the trip start is deterministic.
    private static MaintenanceRules CertainBreakdown => MaintenanceRules.Default with
    {
        Parts = [new MaintenancePart("tires", 1000.0, 900.0)],
        MaxBreakdownChance = 1.0,
    };

    private static Garage WornGarage(MaintenanceRules rules)
    {
        var garage = new Garage([Car], maintenanceRules: rules);
        garage.Add(Car, week: 1);
        garage.Current!.Drive(1000.0); // the only part reaches full wear → chance 1.0
        return garage;
    }

    private static OnboardingState Done => OnboardingState.Fresh with { BirthdayDone = true };

    [Fact]
    public void Roll_BeforeTheIntroduction_DoesNothing()
    {
        // Unity's firstSceneComplete gate: the mechanic's tow has no part in a
        // career that has not started yet.
        MaintenanceRules rules = CertainBreakdown;
        var clock = new FakeClock();
        Garage garage = WornGarage(rules);
        var breakdowns = Build(rules, garage, new FakeOnboardingStore(), clock, out _, out _);

        Assert.Null(breakdowns.Roll(BreakdownKind.Taxi));
    }

    [Fact]
    public void Roll_WithAWornCar_FiresTheWearDerivedChance()
    {
        MaintenanceRules rules = CertainBreakdown;
        var clock = new FakeClock();
        Garage garage = WornGarage(rules);
        var onboarding = new FakeOnboardingStore();
        onboarding.Save(Done);
        var breakdowns = Build(rules, garage, onboarding, clock, out _, out _);

        BreakdownOutcome? outcome = breakdowns.Roll(BreakdownKind.Delivery);

        Assert.NotNull(outcome);
        Assert.Equal(BreakdownKind.Delivery, outcome.Kind);
        Assert.False(outcome.EngineFailure);
        Assert.Equal(rules.PickupFee, outcome.PickupFee);
    }

    [Fact]
    public void Roll_WithAStrandedEngine_ForcesTheEngineFailureVariant()
    {
        var garage = new Garage([Car], new GarageState(Car.Key, [
            new OwnedCarState(Car.Key, 1, 0.0, false, 0, null, new EngineConditionState(0.0, Broken: true)),
        ]));
        var onboarding = new FakeOnboardingStore();
        onboarding.Save(Done);
        var breakdowns = Build(
            MaintenanceRules.Default, garage, onboarding, new FakeClock(), out _, out _);

        BreakdownOutcome? outcome = breakdowns.Roll(BreakdownKind.Taxi);

        Assert.NotNull(outcome);
        Assert.True(outcome.EngineFailure);
        Assert.Equal(0.0, outcome.Fraction);
    }

    [Fact]
    public void Interrupt_ChargesTheTowFee_AndBanksTheDriver()
    {
        var clock = new FakeClock();
        var wallet = new DriverWallet(new DriverState(1000.0, 0.0));
        var drivers = new FakeDriverStore();
        var garage = new Garage([]);
        var breakdowns = Build(
            MaintenanceRules.Default, garage, new FakeOnboardingStore(), clock,
            out _, out var rivals, wallet, drivers);
        var outcome = new BreakdownOutcome(BreakdownKind.Taxi, Fraction: 0.42, 100.0, EngineFailure: false);

        var page = breakdowns.Interrupt(outcome, new RelayCommand(() => { }));

        Assert.Equal(900.0, wallet.Cash, 6);
        DriverState banked = Assert.Single(drivers.Saved);
        Assert.Equal(900.0, banked.Cash, 6);
        Assert.Empty(rivals.RefreshedAt);
        Assert.False(page.FailureVisible);
        Assert.Contains("100", page.FeeLine);
    }

    private static TripBreakdowns Build(
        MaintenanceRules rules, Garage garage, FakeOnboardingStore onboarding,
        FakeClock clock, out GameStateSaver saver, out FakeRivals rivals,
        DriverWallet? wallet = null, FakeDriverStore? drivers = null)
    {
        var walletInstance = wallet ?? new DriverWallet();
        rivals = new FakeRivals();
        saver = TestSaveState.Saver(clock, walletInstance, garage, drivers);
        return new TripBreakdowns(
            new BreakdownStrategy(rules, seed: 0), garage, onboarding, walletInstance,
            rivals, saver, new VoiceBeat(clock), new NullLogger());
    }
}
