using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The ToDo collaborator (PORTPLAN 46): the career snapshot it assembles, the
/// door-driven activation, the persisted ledger and the DELIVER command.
/// </summary>
public sealed class TodoBoardTests
{
    private static (TodoBoard Board, FakeTodoProgressStore Store, DriverWallet Wallet,
        FakeOnboardingStore Onboarding, PoiDirectory Directory, VisitLedger Visits,
        JobLedger Jobs, DestinationPins Pins) Create()
    {
        var store = new FakeTodoProgressStore();
        var wallet = new DriverWallet();
        var onboarding = new FakeOnboardingStore();
        var directory = new PoiDirectory();
        var visits = new VisitLedger();
        var jobs = new JobLedger();
        var pins = new DestinationPins();
        var board = new TodoBoard(
            TodoTable.Default, store, directory, onboarding, wallet, jobs,
            new OperatingZone(null, seed: 0), new Garage(TestRoadCars.Pool),
            pins, visits, new DeliveryCareer(HubCatalog.Default, 0), new NullLogger());
        return (board, store, wallet, onboarding, directory, visits, jobs, pins);
    }

    private static PointOfInterest Door(string id, string name, string type) =>
        new(id, name,
            new AddressLocation(AddressMap.Default.Streets[0].Name, 1, HouseSide.Left),
            type);

    [Fact]
    public void Boot_SyncsTheSavedCareerAndBanksIt()
    {
        var (board, store, wallet, _, _, _, _, _) = Create();
        Assert.False(board.HasCompleted);

        // A second session over an earned career: the boot sync flags it.
        var resumed = new TodoBoard(
            TodoTable.Default, store, new PoiDirectory(), new FakeOnboardingStore(),
            new DriverWallet(new DriverState(0.0, 0.0, CreditEarned: 500.0)),
            new JobLedger(), new OperatingZone(null, seed: 0), new Garage(TestRoadCars.Pool),
            new DestinationPins(), new VisitLedger(),
            new DeliveryCareer(HubCatalog.Default, 0), new NullLogger());

        Assert.True(resumed.HasCompleted);
        Assert.Equal("TodoEarnCr", Assert.Single(resumed.CompletedLines).CopyKey);
        Assert.NotNull(store.Saved);
    }

    [Fact]
    public void PlacedDoor_ActivatesTheVisitTipAndItsEntryCompletesIt()
    {
        var (board, _, _, _, directory, visits, _, _) = Create();

        // No car wash door: the tip stays inactive even when the flag is set.
        visits.MarkCarWash();
        Assert.DoesNotContain(
            "VisitCarWash",
            board.Sync().Completed);

        Assert.True(directory.Add(Door("carwash", "Ace's Carwash", PoiTypes.CarWash)));
        Assert.Equal(
            [TodoSubject.VisitCarWash],
            board.Sync().Completed);
        Assert.True(board.HasCompleted);
        Assert.Equal("Ace's Carwash", board.CompletedLines[0].Placeholder);
    }

    [Fact]
    public void AcknowledgeCommand_ClearsTheBlockAndPersists()
    {
        var (board, store, _, _, _, _, _, _) = Create();
        var board2 = new TodoBoard(
            TodoTable.Default, store, new PoiDirectory(), new FakeOnboardingStore(),
            new DriverWallet(new DriverState(0.0, 0.0, CreditEarned: 500.0)),
            new JobLedger(), new OperatingZone(null, seed: 0), new Garage(TestRoadCars.Pool),
            new DestinationPins(), new VisitLedger(),
            new DeliveryCareer(HubCatalog.Default, 0), new NullLogger());
        int saves = store.Saves;

        board2.AcknowledgeCommand.Execute(null);

        Assert.False(board2.HasCompleted);
        Assert.Equal(saves + 1, store.Saves);
    }

    [Fact]
    public void RotateOnJob_CountsTheSettledJob()
    {
        var (board, store, _, _, _, _, _, _) = Create();

        for (int i = 0; i < TodoLedger.TurnLimit; i++)
        {
            board.RotateOnJob();
        }

        Assert.DoesNotContain("TodoEarnCr", board.Slots.Select(slot => slot.CopyKey));
        Assert.NotEmpty(store.Saved!.Queue);
    }

    [Fact]
    public void Sync_CompletesAQueuedSubjectToo()
    {
        // Unity checks every subject, not only the four on display: a queued
        // one still completes (and gets its care package) when it is met.
        var pins = new DestinationPins(
            new DriverState(0.0, 0.0, ManuallySetDestinations: 18));
        var board = new TodoBoard(
            TodoTable.Default, new FakeTodoProgressStore(), new PoiDirectory(),
            new FakeOnboardingStore(), new DriverWallet(), new JobLedger(),
            new OperatingZone(null, seed: 0), new Garage(TestRoadCars.Pool),
            pins, new VisitLedger(), new DeliveryCareer(HubCatalog.Default, 0),
            new NullLogger());

        Assert.True(board.HasCompleted);
        Assert.Equal(
            "TodoSetDestinations",
            Assert.Single(board.CompletedLines).CopyKey);
    }

    [Fact]
    public void OwningACareer_CompletesTheFollowUp_OnceTheGroceryGateIsMet()
    {
        var hubs = new HubCatalog(
            [new HubTier(1, true, [
                new HubCareer("groceries", "GROCERIES", [new HubLevel("Grocers and Sons", 100)])])]);
        var career = new DeliveryCareer(hubs, 100);
        var onboarding = new FakeOnboardingStore();
        onboarding.Save(OnboardingState.Fresh with { MaxDeliveryIntroSeen = true });
        var board = new TodoBoard(
            TodoTable.Default, new FakeTodoProgressStore(), new PoiDirectory(),
            onboarding, new DriverWallet(), new JobLedger(),
            new OperatingZone(null, seed: 0), new Garage(TestRoadCars.Pool),
            new DestinationPins(), new VisitLedger(), career, new NullLogger());

        // The boot sync met the grocery gate: the follow-up took its slot.
        Assert.Contains(board.Slots, slot => slot.CopyKey == "TodoOpenDeliveryCareer");

        // Opening a career is the reading the next sync picks up.
        Assert.True(career.TryOpenCareer(1, 0, 0));
        _ = board.Sync();
        Assert.Contains(
            "TodoOpenDeliveryCareer",
            board.CompletedLines.Select(line => line.CopyKey));
    }
}
