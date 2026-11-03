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
        JobLedger Jobs) Create()
    {
        var store = new FakeTodoProgressStore();
        var wallet = new DriverWallet();
        var onboarding = new FakeOnboardingStore();
        var directory = new PoiDirectory();
        var visits = new VisitLedger();
        var jobs = new JobLedger();
        var board = new TodoBoard(
            TodoTable.Default, store, directory, onboarding, wallet, jobs,
            new OperatingZone(null, seed: 0, AddressMap.Default), new Garage(TestRoadCars.Pool),
            visits, new DeliveryCareer(HubCatalog.Default, 0, TestNames.For()), new CarePackageLedger(),
            new Random(0), () => { }, new NullLogger());
        return (board, store, wallet, onboarding, directory, visits, jobs);
    }

    private static PointOfInterest Door(string id, string name, string type) =>
        new(id, name,
            new AddressLocation(AddressMap.Default.Streets[0].Name, 1, HouseSide.Left),
            type);

    [Fact]
    public void Boot_SyncsTheSavedCareerAndBanksIt()
    {
        var (board, store, wallet, _, _, _, _) = Create();
        Assert.False(board.HasCompleted);

        // A second session over an earned career: the boot sync flags it.
        var resumed = new TodoBoard(
            TodoTable.Default, store, new PoiDirectory(), new FakeOnboardingStore(),
            new DriverWallet(new DriverState(0.0, 0.0, CreditEarned: 500.0)),
            new JobLedger(), new OperatingZone(null, seed: 0, AddressMap.Default), new Garage(TestRoadCars.Pool),
            new VisitLedger(),
            new DeliveryCareer(HubCatalog.Default, 0, TestNames.For()), new CarePackageLedger(),
            new Random(0), () => { }, new NullLogger());

        Assert.True(resumed.HasCompleted);
        Assert.Equal("TodoEarnCr", Assert.Single(resumed.CompletedLines).CopyKey);
        Assert.NotNull(store.Saved);
    }

    [Fact]
    public void PlacedDoor_ActivatesTheVisitTipAndItsEntryCompletesIt()
    {
        var (board, _, _, _, directory, visits, _) = Create();

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
    public void CompletedTip_GrantsOneCareKitAtAKeptStore()
    {
        var directory = new PoiDirectory(
            [Door("groceries_store", "Max's Grocery Store", PoiTypes.GroceryStore)]);
        var care = new CarePackageLedger();
        int saves = 0;
        var board = new TodoBoard(
            TodoTable.Default, new FakeTodoProgressStore(), directory,
            new FakeOnboardingStore(),
            new DriverWallet(new DriverState(0.0, 0.0, CreditEarned: 500.0)),
            new JobLedger(), new OperatingZone(null, seed: 0, AddressMap.Default),
            new Garage(TestRoadCars.Pool), new VisitLedger(),
            new DeliveryCareer(HubCatalog.Default, 0, TestNames.For()), care, new Random(0),
            () => saves++, new NullLogger());

        // The boot sync met Earn 500 CR: one kit waited at the one shop, and the
        // grant banked through the driver save right away.
        Assert.True(board.HasCompleted);
        Assert.Equal(1, care.Total);
        Assert.Equal("groceries_store", Assert.Single(care.Stores).StoreId);
        Assert.Equal(1, saves);
    }

    [Fact]
    public void CompletedTip_WithoutAStoreDoor_IsOwedUntilAStoreOpens()
    {
        var directory = new PoiDirectory();
        var care = new CarePackageLedger();
        var board = new TodoBoard(
            TodoTable.Default, new FakeTodoProgressStore(), directory,
            new FakeOnboardingStore(),
            new DriverWallet(new DriverState(0.0, 0.0, CreditEarned: 500.0)),
            new JobLedger(), new OperatingZone(null, seed: 0, AddressMap.Default),
            new Garage(TestRoadCars.Pool), new VisitLedger(),
            new DeliveryCareer(HubCatalog.Default, 0, TestNames.For()), care, new Random(0),
            () => { }, new NullLogger());

        // The tip still completes and lists; the kit is owed, not dropped.
        Assert.True(board.HasCompleted);
        Assert.Equal(0, care.Total);
        Assert.Equal(1, care.Unplaced);

        // A shop opens: the next cold-edge sync places the owed kit there.
        Assert.True(directory.Add(
            Door("groceries_store", "Max's Grocery Store", PoiTypes.GroceryStore)));
        board.Sync();

        Assert.Equal(1, care.Total);
        Assert.Equal(0, care.Unplaced);
        Assert.Equal("groceries_store", Assert.Single(care.Stores).StoreId);
    }

    [Fact]
    public void CompletedTip_GrantsTheKitWhenTheStoreStreetIsOutsideTheZone()
    {
        // The reported bug: Max's shop sits on Street 2, the taxi zone had only
        // the home street, and the grant was dropped even though the player had
        // just driven there. The door is discovered, so it can hold the kit.
        AddressMap map = AddressMap.Default;
        AddressStreet home = map.Streets[0];
        var zone = new OperatingZone(
            DriverState.Fresh with { StreetIds = [home.Id] }, seed: 0, map,
            homeStreetId: home.Id, initialKm: AddressBook.MapLengthKm(map));
        var directory = new PoiDirectory(
            [new PointOfInterest(
                "max-grocery", "Max's Grocery Store",
                new AddressLocation(map.Streets[1].Name, 1, HouseSide.Right),
                PoiTypes.MaxsGrocery)]);
        var care = new CarePackageLedger();

        _ = new TodoBoard(
            TodoTable.Default, new FakeTodoProgressStore(), directory,
            new FakeOnboardingStore(),
            new DriverWallet(new DriverState(0.0, 0.0, CreditEarned: 500.0)),
            new JobLedger(), zone, new Garage(TestRoadCars.Pool), new VisitLedger(),
            new DeliveryCareer(HubCatalog.Default, 0, TestNames.For()), care, new Random(0),
            () => { }, new NullLogger());

        Assert.Equal(1, care.Total);
        Assert.Equal(0, care.Unplaced);
        Assert.Equal("max-grocery", Assert.Single(care.Stores).StoreId);
    }

    [Fact]
    public void OwedBacklog_AndANewCompletion_AreBothBankedOnce()
    {
        // A save with one dropped kit can still meet a fresh tip: the debt and
        // the new kit must both be banked, and neither counted twice.
        string[] completed =
        [
            "TaxiJobs", "BankStandings", "VisitCarDealer", "GoToMaxGroceryStore",
            "OpenDeliveryCareer",
        ];
        var store = new FakeTodoProgressStore();
        store.Save(TodoLedger.Fresh(TodoTable.Default).Snapshot() with
        {
            Awaiting = completed,
            CompletedTips = completed.ToDictionary(
                subject => subject,
                subject => new TodoTipSnapshot("TodoEarnCr", 1, 1)),
        });
        var care = new CarePackageLedger(
            new CarePackageState([new CarePackagePlacement("groceries_store", 4)]));
        var directory = new PoiDirectory();

        var board = new TodoBoard(
            TodoTable.Default, store, directory, new FakeOnboardingStore(),
            // The wallet meets the Earn CR tip on the boot sync: one new kit.
            new DriverWallet(new DriverState(0.0, 0.0, CreditEarned: 500.0)),
            new JobLedger(), new OperatingZone(null, seed: 0, AddressMap.Default),
            new Garage(TestRoadCars.Pool), new VisitLedger(),
            new DeliveryCareer(HubCatalog.Default, 0, TestNames.For()), care, new Random(0),
            () => { }, new NullLogger());

        Assert.Equal(4, care.Total);
        Assert.Equal(2, care.Unplaced);

        Assert.True(directory.Add(
            Door("groceries_store", "Max's Grocery Store", PoiTypes.GroceryStore)));
        board.Sync();

        Assert.Equal(6, care.Total);
        Assert.Equal(0, care.Unplaced);

        // And a third sync adds nothing.
        board.Sync();
        Assert.Equal(6, care.Total);
    }

    [Fact]
    public void CompletedBlockOutrunningTheBags_ReconcilesTheDroppedKit()
    {
        // The save the old build left behind: five completed tips, four kits and
        // no record of the fifth. The block is acknowledged only when a run
        // starts, so its rows are the completions this save has not delivered;
        // the difference is the kit the old build dropped, and the sync owes it
        // back.
        string[] completed =
        [
            "CrEarnings", "TaxiJobs", "BankStandings", "VisitCarDealer",
            "GoToMaxGroceryStore",
        ];
        var store = new FakeTodoProgressStore();
        store.Save(TodoLedger.Fresh(TodoTable.Default).Snapshot() with
        {
            Awaiting = completed,
            CompletedTips = completed.ToDictionary(
                subject => subject,
                subject => new TodoTipSnapshot("TodoEarnCr", 1, 1)),
        });
        var care = new CarePackageLedger(
            new CarePackageState([new CarePackagePlacement("groceries_store", 4)]));
        var directory = new PoiDirectory(
            [Door("groceries_store", "Max's Grocery Store", PoiTypes.GroceryStore)]);

        var board = new TodoBoard(
            TodoTable.Default, store, directory, new FakeOnboardingStore(),
            new DriverWallet(), new JobLedger(),
            new OperatingZone(null, seed: 0, AddressMap.Default),
            new Garage(TestRoadCars.Pool), new VisitLedger(),
            new DeliveryCareer(HubCatalog.Default, 0, TestNames.For()), care, new Random(0),
            () => { }, new NullLogger());

        Assert.Equal(5, care.Total);
        Assert.Equal(0, care.Unplaced);

        // Idempotent: the next boot cannot owe the same kit twice.
        board.Sync();
        Assert.Equal(5, care.Total);
    }

    [Fact]
    public void AcknowledgeCommand_ClearsTheBlockAndPersists()
    {
        var (board, store, _, _, _, _, _) = Create();
        var board2 = new TodoBoard(
            TodoTable.Default, store, new PoiDirectory(), new FakeOnboardingStore(),
            new DriverWallet(new DriverState(0.0, 0.0, CreditEarned: 500.0)),
            new JobLedger(), new OperatingZone(null, seed: 0, AddressMap.Default), new Garage(TestRoadCars.Pool),
            new VisitLedger(),
            new DeliveryCareer(HubCatalog.Default, 0, TestNames.For()), new CarePackageLedger(),
            new Random(0), () => { }, new NullLogger());
        int saves = store.Saves;

        board2.AcknowledgeCommand.Execute(null);

        Assert.False(board2.HasCompleted);
        Assert.Equal(saves + 1, store.Saves);
    }

    [Fact]
    public void RotateOnJob_CountsTheSettledJob()
    {
        var (board, store, _, _, _, _, _) = Create();

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
        // one still completes when it is met. (The kit grant needs a store door,
        // so the grant itself is covered by its own test below.)
        var board = new TodoBoard(
            TodoTable.Default, new FakeTodoProgressStore(), new PoiDirectory(),
            new FakeOnboardingStore(), new DriverWallet(),
            new JobLedger(new DriverState(0.0, 0.0, JobsCompleted: 18)),
            new OperatingZone(null, seed: 0, AddressMap.Default), new Garage(TestRoadCars.Pool),
            new VisitLedger(), new DeliveryCareer(HubCatalog.Default, 0, TestNames.For()),
            new CarePackageLedger(), new Random(0), () => { }, new NullLogger());

        Assert.True(board.HasCompleted);
        Assert.Equal(
            "TodoSetDestinations",
            Assert.Single(board.CompletedLines).CopyKey);
    }

    [Fact]
    public void EverySettledJob_CountsAsADestinationReachedWithoutGps()
    {
        // The port has no GPS, so every drive is a drive without GPS: the
        // destination tip advances on the settle itself, with no address-page
        // pin anywhere in the path.
        var jobs = new JobLedger(
            new DriverState(0.0, 0.0, JobsCompleted: 17, TaxiJobsCompleted: 17));
        var board = new TodoBoard(
            TodoTable.Default, new FakeTodoProgressStore(), new PoiDirectory(),
            new FakeOnboardingStore(), new DriverWallet(), jobs,
            new OperatingZone(null, seed: 0, AddressMap.Default), new Garage(TestRoadCars.Pool),
            new VisitLedger(), new DeliveryCareer(HubCatalog.Default, 0, TestNames.For()),
            new CarePackageLedger(), new Random(0), () => { }, new NullLogger());

        Assert.DoesNotContain(
            "TodoSetDestinations",
            board.CompletedLines.Select(line => line.CopyKey));

        jobs.RegisterTaxi();
        _ = board.Sync();

        Assert.Contains(
            "TodoSetDestinations",
            board.CompletedLines.Select(line => line.CopyKey));
    }

    [Fact]
    public void OwningACareer_CompletesTheFollowUp_OnceTheGroceryGateIsMet()
    {
        var hubs = new HubCatalog(
            [new HubTier(1, true, [
                new HubCareer("groceries", "GROCERIES", [new HubLevel("Grocers and Sons", 100)])])]);
        var career = new DeliveryCareer(hubs, 100, TestNames.For());
        var onboarding = new FakeOnboardingStore();
        onboarding.Save(OnboardingState.Fresh with { MaxDeliveryIntroSeen = true });
        var board = new TodoBoard(
            TodoTable.Default, new FakeTodoProgressStore(), new PoiDirectory(),
            onboarding, new DriverWallet(), new JobLedger(),
            new OperatingZone(null, seed: 0, AddressMap.Default), new Garage(TestRoadCars.Pool),
            new VisitLedger(), career, new CarePackageLedger(),
            new Random(0), () => { }, new NullLogger());

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
