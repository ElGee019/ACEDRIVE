using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The crash that ends a job (Unity's accident arrival): the ride is
/// abandoned without a receipt, the customer loses one friendship, and the
/// companion's REPAIR THE CAR button pays the bill, banks the insured share for
/// the broker and hands the panel back to the main page.</summary>
public sealed class JobAccidentTests
{
    private static readonly CarSpec Car = new(
        "ks_test_car", 2020, "Test", "Car", Passengers: 2, Class: 16,
        Price: 40000.0, TopSpeedKmh: 240);

    private static Garage GarageWith(bool insured) =>
        new([Car], new GarageState(Car.Key, [
            new OwnedCarState(Car.Key, 1, 0.0, HasInsurance: insured),
        ]));

    private static TestSessionResult New(
        Garage garage, FakeTelemetryHud hud, FakeFriendshipStore friends,
        DriverWallet wallet) =>
        MainViewModelTests.Create(
            speedKmh: 0f, hud: hud, friendships: friends, wallet: wallet,
            cars: [Car], carKsId: Car.Key, garage: garage);

    // One crash: a poll at speed, then a poll whose damage counter jumped. The
    // detector prices the hit off the speed of the sample before it.
    private static void Crash(MainViewModel vm, FakeClock clock, FakeTelemetry tel)
    {
        tel.Next = tel.Next with { SpeedKmh = 120f, CarDamage = 0f };
        clock.Advance(TimeSpan.FromSeconds(0.1));
        vm.Poll();
        tel.Next = tel.Next with { SpeedKmh = 0f, CarDamage = 50f };
        clock.Advance(TimeSpan.FromSeconds(0.1));
        vm.Poll();
    }

    [Fact]
    public void AccidentDuringARide_EndsTheJobAndOffersTheRepair()
    {
        var hud = new FakeTelemetryHud();
        var friends = new FakeFriendshipStore();
        TestSessionResult rig = New(GarageWith(insured: false), hud, friends, new DriverWallet());
        TaxiFlow.Propose(rig.Vm);
        TaxiFlow.Pickup(rig.Vm, rig.Clock);

        Crash(rig.Vm, rig.Clock, rig.Tel);

        Assert.Null(hud.JobGuidance); // the drive line went away
        Assert.Equal("HudAccidentRideLost", hud.TaskPrompt!.TextKey);
        Assert.Equal("HudAccidentRepair", hud.TaskPrompt.ActionKey);
        // The prompt carries the impact speed, the customer who lost the
        // friendship and the bill the button takes (120 of 240 km/h on a
        // 40 000 CR car: half its value).
        Assert.Equal("120", hud.TaskPrompt.Args![0]);
        var (customer, delta) = Assert.Single(friends.Deltas);
        Assert.Equal(customer, hud.TaskPrompt.Args[1]);
        Assert.Equal("20000.00", hud.TaskPrompt.Args[2]);
        Assert.Equal(-1, delta);
        // The ride cannot be resumed: both job commands guard on the phase, and
        // the crash dropped it to None.
        Assert.False(rig.Vm.DropoffCommand.CanExecute(null));
        Assert.False(rig.Vm.PickupCommand.CanExecute(null));
    }

    [Fact]
    public void RepairAfterAnInsuredAccident_TakesTheCash_ClaimsNinetyPercent_AndShowsTheMenu()
    {
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet(new DriverState(50000.0, 0.0));
        Garage garage = GarageWith(insured: true);
        TestSessionResult rig = New(garage, hud, new FakeFriendshipStore(), wallet);
        TaxiFlow.Propose(rig.Vm);
        TaxiFlow.Pickup(rig.Vm, rig.Clock);
        Crash(rig.Vm, rig.Clock, rig.Tel); // 120 of 240 km/h: half the car's value

        hud.RaiseTaskAction();

        Assert.Equal(30000.0, wallet.Cash, 6);              // 40000 × 0.5 paid
        Assert.Equal(18000.0, wallet.PendingInsurance, 6);  // 90 % of the bill
        Assert.Equal(0.0, garage.Current!.BodyDamage, 6);
        Assert.Null(hud.TaskPrompt);
        Assert.IsType<MenuScreenViewModel>(rig.Vm.CurrentScreen);
        Assert.True(rig.Vm.IsShown); // the panel comes back on the main page
    }

    [Fact]
    public void RepairOnAnUninsuredCar_TakesTheWholeBillAndClaimsNothing()
    {
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet(new DriverState(50000.0, 0.0));
        TestSessionResult rig = New(GarageWith(insured: false), hud, new FakeFriendshipStore(), wallet);
        TaxiFlow.Propose(rig.Vm);
        TaxiFlow.Pickup(rig.Vm, rig.Clock);
        Crash(rig.Vm, rig.Clock, rig.Tel);

        hud.RaiseTaskAction();

        Assert.Equal(30000.0, wallet.Cash, 6);
        Assert.Equal(0.0, wallet.PendingInsurance, 6);
        Assert.IsType<MenuScreenViewModel>(rig.Vm.CurrentScreen);
    }

    [Fact]
    public void RepairWithShortCash_TakesWhatThereIsAndStillRepairs()
    {
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet(new DriverState(1000.0, 0.0));
        Garage garage = GarageWith(insured: true);
        TestSessionResult rig = New(garage, hud, new FakeFriendshipStore(), wallet);
        TaxiFlow.Propose(rig.Vm);
        TaxiFlow.Pickup(rig.Vm, rig.Clock);
        Crash(rig.Vm, rig.Clock, rig.Tel);

        hud.RaiseTaskAction();

        Assert.Equal(0.0, wallet.Cash, 6);                 // the ledger never goes negative
        Assert.Equal(900.0, wallet.PendingInsurance, 6);   // 90 % of what was paid
        Assert.Equal(0.0, garage.Current!.BodyDamage, 6);
    }

    [Fact]
    public void AccidentOutsideAJob_KeepsThePlainNotice()
    {
        var hud = new FakeTelemetryHud();
        TestSessionResult rig = New(GarageWith(insured: false), hud, new FakeFriendshipStore(), new DriverWallet());

        Crash(rig.Vm, rig.Clock, rig.Tel);

        Assert.Null(hud.TaskPrompt);
        Assert.NotNull(hud.Notice);
    }
}
