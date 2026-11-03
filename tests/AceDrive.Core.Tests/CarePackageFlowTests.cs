using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The care-package run end to end (PORTPLAN item 21): the jobs page's bag
/// button pins the first store, each ENTER picks up and repins the next door,
/// the last pickup pins the community centre, and DELIVER opens the delivered
/// page with the receipt numbers and the run's two reward cards.
/// The run is driven over the headless session, so the HUD, the ledger and the
/// panel are the same collaborators production wires.
/// </summary>
public sealed class CarePackageFlowTests
{
    private static readonly CarePackageState TwoStoreRun = new(
    [
        new CarePackagePlacement("groceries_store", 3),
        new CarePackagePlacement("coffee_shop", 3),
    ]);

    [Fact]
    public void Deliver_ServesEachStoreThenTheCommunityCentreAndShowsTheReceipt()
    {
        var hud = new FakeTelemetryHud();
        TestSessionResult session = Create(hud, TwoStoreRun);
        MainControlScreenViewModel control = OpenControl(session);

        Assert.True(control.ShowDeliverCarePackages);
        Assert.Contains("6", control.CarePackageCountLine, StringComparison.Ordinal);

        control.DeliverCarePackagesCommand.Execute(null);

        // The run hides the panel and pins the first store: the task text names
        // the door, its address and which leg of the run this is.
        Assert.False(session.Vm.IsShown);
        Assert.Equal("groceries_store", hud.Destination?.Id);
        Assert.Equal("HudCarePickupDrive", hud.Copy?.DriveKey);
        Assert.Equal("1", hud.Copy?.DriveArgs?[4]);
        Assert.Equal("2", hud.Copy?.DriveArgs?[5]);

        // PICK UP at the first store: the kits move into the car and the
        // companion repins the second store.
        hud.AtDestination = true;
        hud.RaiseEnter();
        Assert.Equal("coffee_shop", hud.Destination?.Id);
        Assert.Equal("HudCarePickupDrive", hud.Copy?.DriveKey);
        Assert.Equal("2", hud.Copy?.DriveArgs?[4]);
        Assert.Equal(3, session.CarePackages.Carried);

        // PICK UP at the second: the run is collected and the community centre
        // becomes the destination.
        hud.RaiseEnter();
        Assert.Equal(StoryCommunityCentre.Definition.Id, hud.Destination?.Id);
        Assert.Equal("HudCareDropoffDrive", hud.Copy?.DriveKey);
        Assert.Equal(5, session.CarePackages.Carried);

        // DELIVER at the community centre: the delivered page opens with the
        // receipt numbers and the run's two reward cards, and the leftover kit
        // stays on its shelf for a later run.
        hud.RaiseEnter();
        var page = Assert.IsType<CarePackageReceiptScreenViewModel>(session.Vm.CurrentScreen);
        Assert.Null(hud.Destination);
        Assert.Equal(0, session.CarePackages.Carried);
        Assert.Equal(1, session.CarePackages.Total);
        Assert.Contains("5", page.DeliveredLine, StringComparison.Ordinal);
        Assert.Contains("1", page.RemainingLine, StringComparison.Ordinal);
        Assert.True(page.HasStores);
        Assert.Contains("Max's Grocery Store", page.StoresLine, StringComparison.Ordinal);

        // The pair rides the same page (PORTPLAN 61), exactly Unity's
        // CANVASREWARDS on the completion edge: two cards, one claim. Claiming
        // stores the card in the glove compartment and banks it, then CONTINUE
        // lands back on the jobs page.
        Assert.True(page.HasReward);
        Assert.True(page.RewardOpen);
        Assert.Equal(2, page.RewardCards.Count);
        Assert.Contains("0/2", page.GloveboxLine, StringComparison.Ordinal);

        page.ClaimCommand.Execute(page.RewardCards[0]);
        Assert.False(page.RewardOpen);
        Assert.True(session.Trunk.Contains(page.RewardCards[0].Title));
        Assert.Equal(1, session.Glovebox.Count(session.Trunk));

        page.ContinueCommand.Execute(null);
        var back = Assert.IsType<MainControlScreenViewModel>(session.Vm.CurrentScreen);
        Assert.False(back.ShowDeliverCarePackages);
    }

    [Fact]
    public void Begin_WithCarriedKits_ResumesAtTheCommunityCentre()
    {
        var hud = new FakeTelemetryHud();
        // A session that quit after two pickups: the shelf still holds one kit,
        // so the total is three — below the bag threshold, but the car's two
        // kits must stay deliverable.
        TestSessionResult session = Create(
            hud,
            new CarePackageState(
                [new CarePackagePlacement("groceries_store", 1)], Carried: 2));
        MainControlScreenViewModel control = OpenControl(session);

        Assert.True(control.ShowDeliverCarePackages);
        control.DeliverCarePackagesCommand.Execute(null);

        Assert.Equal(StoryCommunityCentre.Definition.Id, hud.Destination?.Id);
        Assert.Equal("HudCareDropoffDrive", hud.Copy?.DriveKey);

        hud.AtDestination = true;
        hud.RaiseEnter();

        var receipt = Assert.IsType<CarePackageReceiptScreenViewModel>(session.Vm.CurrentScreen);
        Assert.Contains("2", receipt.DeliveredLine, StringComparison.Ordinal);
        // The route was runtime-only, so the receipt names no stores; the shelf
        // kit waits for the next run.
        Assert.False(receipt.HasStores);
        Assert.Equal(1, session.CarePackages.Total);
    }

    [Fact]
    public void Deliver_WithFewerThanFiveKits_IsNotOfferedAndStartsNothing()
    {
        var hud = new FakeTelemetryHud();
        TestSessionResult session = Create(
            hud,
            new CarePackageState([new CarePackagePlacement("groceries_store", 4)]));
        MainControlScreenViewModel control = OpenControl(session);

        Assert.False(control.ShowDeliverCarePackages);
        control.DeliverCarePackagesCommand.Execute(null);

        Assert.Null(hud.Destination);
        Assert.True(session.Vm.IsShown);
    }

    private static TestSessionResult Create(FakeTelemetryHud hud, CarePackageState care) =>
        TestSession.Create(new TestSessionOptions
        {
            Hud = hud,
            SavedDriver = DriverState.Fresh with { CarePackages = care },
            PoiDirectory = Directory(),
            // No starter tips: the only kits are the ones the test seeds.
            Todos = new TodoTable([]),
            // Two stored-card rewards: every delivered-page test then claims a
            // real card instead of landing on the slot or cash branch by chance.
            Rewards = RewardCards,
            RewardRng = new Random(0),
        });

    // The delivered page's pinned catalogue: both rewards are glovebox cards.
    private static readonly RewardTable RewardCards = new(
    [
        new RewardCard("DOUBLE NEXT TIP", "Your next customer tip will be doubled"),
        new RewardCard("NEXT FINE WAIVED", "Your next fine will be waived"),
    ]);

    private static MainControlScreenViewModel OpenControl(TestSessionResult session)
    {
        session.Story.FinishDialogue();
        session.Vm.Show();
        var menu = Assert.IsType<MenuScreenViewModel>(session.Vm.CurrentScreen);
        menu.JobsCommand.Execute(null);
        return Assert.IsType<MainControlScreenViewModel>(session.Vm.CurrentScreen);
    }

    private static PoiDirectory Directory() =>
        new(
        [
            new PointOfInterest(
                "groceries_store", "Max's Grocery Store",
                new AddressLocation("Street 1", 3, HouseNumberPlan.SideOf(3)),
                PoiTypes.GroceryStore),
            new PointOfInterest(
                "coffee_shop", "Coffee Shop",
                new AddressLocation("Street 1", 4, HouseNumberPlan.SideOf(4)),
                PoiTypes.CoffeeShop),
            new PointOfInterest(
                StoryCommunityCentre.Definition.Id, StoryCommunityCentre.Definition.Name,
                new AddressLocation("Street 1", 5, HouseNumberPlan.SideOf(5)),
                PoiTypes.CommunityCentre),
        ]);
}
