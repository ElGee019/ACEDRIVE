using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The trip-start breakdown session (PORTPLAN item 39): a triggered
/// trip is cut before it pays, the mechanic charges the tow fee, the per-kind
/// consequences land, and a stranded engine always fires the engine variant.</summary>
public sealed class BreakdownFlowTests
{
    private static readonly CarSpec Car = new(
        "ks_test_car", 2020, "Test", "Car", Passengers: 2, Class: 16, Price: 12000.0, EngineCc: 2000);

    private static readonly DeliveryStore Post =
        new("adenau-post", "Adenau Post", "Hahn", 12, 40, 2.50);

    private static OnboardingState Done => OnboardingState.Fresh with { BirthdayDone = true };

    // One part and a 100% ceiling: a fully worn car rolls a guaranteed
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

    [Fact]
    public void TaxiBreakdown_CutsTheJob_AndChargesTheTowFee()
    {
        MaintenanceRules rules = CertainBreakdown;
        Garage garage = WornGarage(rules);
        var friends = new FakeFriendshipStore();
        var wallet = new DriverWallet(new DriverState(1000.0, 0.0));
        var (vm, _, _, _) = MainViewModelTests.Create(
            speedKmh: 0f, garage: garage, wallet: wallet, friendships: friends,
            breakdowns: new BreakdownStrategy(rules, seed: 0), progress: Done);

        vm.TaxiShiftCommand.Execute(null);
        var proposal = Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen);
        double before = wallet.Cash;
        proposal.AcceptCommand.Execute(null);
        vm.PickupCommand.Execute(null);

        Assert.IsType<BreakdownScreenViewModel>(vm.CurrentScreen); // no receipt
        Assert.Equal(before - 100.0, wallet.Cash, 6);              // the tow fee
        Assert.Equal(-1, Assert.Single(friends.Deltas).Delta);     // the customer lost 1
    }

    [Fact]
    public void BeforeOnboarding_NoBreakdownRoll()
    {
        MaintenanceRules rules = CertainBreakdown;
        Garage garage = WornGarage(rules);
        var friends = new FakeFriendshipStore();
        var (vm, clock, tel, _) = MainViewModelTests.Create(
            speedKmh: 0f, garage: garage, friendships: friends,
            breakdowns: new BreakdownStrategy(rules, seed: 0)); // not onboarded

        TaxiFlow.AcceptAndDrive(vm, clock, tel);

        Assert.IsType<TaxiReceiptScreenViewModel>(vm.CurrentScreen); // the ride paid
        Assert.Empty(friends.Deltas);
    }

    [Fact]
    public void StrandedEngine_ForcesTheEngineFailureVariant()
    {
        var garage = new Garage([Car], new GarageState(Car.Key, [
            new OwnedCarState(Car.Key, 1, 0.0, false, 0, null, new EngineConditionState(0.0, Broken: true)),
        ]));
        var (vm, _, _, _) = MainViewModelTests.Create(speedKmh: 0f, garage: garage, progress: Done);

        vm.TaxiShiftCommand.Execute(null);
        Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen).AcceptCommand.Execute(null);
        vm.PickupCommand.Execute(null);

        var page = Assert.IsType<BreakdownScreenViewModel>(vm.CurrentScreen);
        Assert.True(page.FailureVisible);
        Assert.Contains("engine has failed", page.Message);
    }

    [Fact]
    public void DeliveryBreakdown_LosesTheCargo()
    {
        var catalog = new DeliveryCatalog([Post], 0.7, 100);
        MaintenanceRules rules = CertainBreakdown;
        Garage garage = WornGarage(rules);
        var wallet = new DriverWallet(new DriverState(1000.0, 0.0));
        var (vm, _, _, _) = MainViewModelTests.Create(
            speedKmh: 0f, catalog: catalog, garage: garage, wallet: wallet,
            breakdowns: new BreakdownStrategy(rules, seed: 0), progress: Done);

        vm.FinishDialogue();
        // The intro lands on the menu; Jobs opens the career page.
        var menu = Assert.IsType<MenuScreenViewModel>(vm.CurrentScreen);
        menu.JobsCommand.Execute(null);
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        control.DeliveryCommand.Execute(null);
        var board = Assert.IsType<DeliveryJobScreenViewModel>(vm.CurrentScreen);
        double before = wallet.Cash;
        board.ToggleOrderCommand.Execute(board.Rows.Single(r => r.Store.Key == "adenau-post"));
        board.MainButtonCommand.Execute(null);

        Assert.IsType<BreakdownScreenViewModel>(vm.CurrentScreen); // no receipt
        Assert.Equal(before - 100.0, wallet.Cash, 6);              // the tow fee
    }

    [Fact]
    public void OwnerFor_ResolvesTheProducingHubOwner()
    {
        // The read-only owner lookup a failed delivery run charges familiarity
        // against; it never credits the ramp the way RegisterDelivery does.
        var hubs = new HubCatalog([
            new HubTier(1, true, [new HubCareer("groceries", "GROCERIES", [new HubLevel("Grocers and Sons", 100)])]),
        ], OwnerNames: ["Anna Berger"]);
        var career = new DeliveryCareer(hubs, startCredP: 100);

        Assert.Equal("", career.OwnerFor("groceries")); // no hub yet
        Assert.True(career.TryOpenCareer(1, 0, 0));
        string owner = career.OwnerFor("groceries");
        Assert.NotEmpty(owner);
        Assert.Equal(owner, career.OwnerFor("groceries")); // stable
    }
}
