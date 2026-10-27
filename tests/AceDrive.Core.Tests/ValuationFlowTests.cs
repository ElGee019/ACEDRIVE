using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The weekly market valuation end to end (PORTPLAN 49), driven the way the
/// root drives it: the Monday poll draws the venue, the calendar row starts the
/// drive, parking opens the showcase, ENTER pushes one entry and draws an
/// opponent, and the result pays only on CONTINUE. The live watcher and the
/// restart catch-up run on the same fake board.
/// </summary>
public sealed class ValuationFlowTests
{
    // A car with real money/power columns, so the jury score is not a row of
    // zeros and the assertions can name the Unity terms.
    private static readonly CarSpec TestCar = new(
        "ks_test_car", 2020, "Test", "One", Passengers: 2, Class: 5,
        Price: 20000.0, Horsepower: 150, EngineCc: 1600, TopSpeedKmh: 200);

    [Fact]
    public void MondayPoll_DrawsTheVenue_AndTheCalendarTapStartsTheDrive()
    {
        var board = new FakeValuationBoard();
        var hud = new FakeTelemetryHud();
        TestSessionResult session = Create(board: board, hud: hud);
        session.Story.FinishDialogue();

        session.Vm.Poll();
        Assert.True(session.Ledger.HasVenue);
        Assert.Null(hud.Destination);

        OpenCalendar(session).DayCommand.Execute(2);

        Assert.True(session.Ledger.Driving);
        Assert.False(session.Vm.IsShown);
        Assert.Equal("HudValuationDrive", hud.Copy?.DriveKey);
        Assert.Equal(PoiTypes.Valuation, hud.Destination?.Type);
    }

    [Fact]
    public void ParkingAtTheVenue_OpensTheShowcaseAndConsumesTheVenue()
    {
        var board = new FakeValuationBoard();
        var hud = new FakeTelemetryHud();
        TestSessionResult session = Create(board: board, hud: hud);

        ValuationScreenViewModel page = OpenShowcase(session, hud);

        Assert.False(session.Ledger.Driving);
        Assert.False(session.Ledger.HasVenue);
        Assert.Equal(1, session.Ledger.EventsCompleted);
        Assert.True(session.Vm.IsShown);
        Assert.Null(hud.Destination);
        Assert.NotEmpty(page.VenueLine);
    }

    [Fact]
    public void Enter_PushesOneEntryAndPaysOnlyOnContinue()
    {
        var board = new FakeValuationBoard { OwnUid = "me" };
        board.Entries = [Entry("rival", "Rival", score: 1.0)];
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet();
        Garage garage = GarageWithTestCar();
        TestSessionResult session = Create(
            board: board, hud: hud, wallet: wallet, garage: garage);
        ValuationScreenViewModel page = OpenShowcase(session, hud);

        page.EnterCommand.Execute(null);

        // One push, with the Unity score: class + speed/2 + hp/10 + price/1000
        // + the car's settled jobs + the fresh engine's 100 points.
        var push = Assert.Single(board.Pushes);
        Assert.Equal(TestCar.Key, push.CarKey);
        Assert.Equal(1, push.Jobs);
        Assert.Equal(0.0, push.Broken);
        Assert.Equal(ValuationRules.Score(TestCar, 1, 0.0), push.Score, 6);
        Assert.True(session.Ledger.HasEntry(CurrentWeekAtStart()));

        double xpBefore = wallet.Xp;
        session.Vm.Poll();

        Assert.True(page.HasCompetitor);
        Assert.Equal("Rival", page.OpponentName);
        Assert.Equal("YOU WON", page.ResultText);
        Assert.Equal(xpBefore, wallet.Xp, 6);

        page.ContinueCommand.Execute(null);

        // The verdict pays on CONTINUE, once: a second tap has nothing left.
        double expected = (ValuationRules.Score(TestCar, 1, 0.0) - 1.0)
            / ValuationRules.WinXpDivisor;
        Assert.Equal(xpBefore + expected, wallet.Xp, 6);
        page.ContinueCommand.Execute(null);
        Assert.Equal(xpBefore + expected, wallet.Xp, 6);
        Assert.IsType<MainControlScreenViewModel>(session.Vm.CurrentScreen);
    }

    [Fact]
    public void Enter_OnAnEmptyBoard_FallsBackToTheHouseEntry()
    {
        var board = new FakeValuationBoard { OwnUid = "me", Entries = [] };
        var hud = new FakeTelemetryHud();
        TestSessionResult session = Create(
            board: board, hud: hud, garage: GarageWithTestCar());
        ValuationScreenViewModel page = OpenShowcase(session, hud);

        page.EnterCommand.Execute(null);
        session.Vm.Poll();

        Assert.True(page.HasCompetitor);
        Assert.Equal(ValuationRules.HouseEntryName, page.OpponentName);
        // The house car comes from the same catalogue, so its score is the
        // formula with no jobs on a fresh engine.
        Assert.Contains(
            ValuationRules.Score(TestCar, 0, 0.0).ToString("0.00", CultureInfo.CurrentCulture),
            page.OpponentScoreText, StringComparison.Ordinal);
    }

    [Fact]
    public void LiveSnapshot_PaysEachNewcomerOnceAndRemembersIt()
    {
        var board = new FakeValuationBoard { OwnUid = "me", Entries = [] };
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet();
        TestSessionResult session = Create(
            board: board, hud: hud, wallet: wallet, garage: GarageWithTestCar());
        ValuationScreenViewModel page = OpenShowcase(session, hud);
        page.EnterCommand.Execute(null);
        session.Vm.Poll();

        double own = session.Ledger.OwnScore;
        ValuationEntry ownDoc = Entry("me", "Me", own);
        ValuationEntry winner = Entry("winner", "Winner", own - 10.0);
        ValuationEntry loser = Entry("loser", "Loser", own + 10.0);
        double before = wallet.Xp;

        board.RaiseChanged([ownDoc, winner, loser]);
        session.Vm.Poll();

        // Unity's listener pays a win the gap/500 and a loss the gap/1000, one
        // 4 s notice at a time; the first lands on this very poll.
        Assert.Contains("Winner", hud.Notice, StringComparison.Ordinal);
        Assert.Equal(before + 10.0 / ValuationRules.WinXpDivisor, wallet.Xp, 6);
        Assert.Contains("winner", session.Ledger.SeenUids);

        session.Clock.Advance(TimeSpan.FromSeconds(4.1));
        session.Vm.Poll();
        Assert.Contains("Loser", hud.Notice, StringComparison.Ordinal);
        Assert.Equal(
            before + (10.0 / ValuationRules.WinXpDivisor) + (10.0 / ValuationRules.LossXpDivisor),
            wallet.Xp, 6);
        Assert.Contains("loser", session.Ledger.SeenUids);

        // The same snapshot again is already settled: no notice, no XP.
        double after = wallet.Xp;
        board.RaiseChanged([ownDoc, winner, loser]);
        session.Vm.Poll();
        session.Clock.Advance(TimeSpan.FromSeconds(4.1));
        session.Vm.Poll();
        Assert.Equal(after, wallet.Xp, 6);
    }

    [Fact]
    public void RestartInsideTheWeek_CatchesUpWinsOnly()
    {
        int week = CurrentWeekAtStart();
        var board = new FakeValuationBoard { OwnUid = "me" };
        board.Entries =
        [
            Entry("me", "Me", 100.0),
            Entry("winner", "Winner", 90.0),
            Entry("loser", "Loser", 110.0),
            Entry("tie", "Tie", 100.0),
        ];
        var hud = new FakeTelemetryHud();
        var wallet = new DriverWallet();
        TestSessionResult session = Create(
            board: board, hud: hud, wallet: wallet, saved: SavedWeek(week));

        session.Story.FinishDialogue();
        session.Vm.Poll();

        Assert.Contains("While you were away you won 1", hud.Notice, StringComparison.Ordinal);
        Assert.Equal(10.0 / ValuationRules.WinXpDivisor, wallet.Xp, 6);
        Assert.Contains("winner", session.Ledger.SeenUids);
        Assert.Contains("loser", session.Ledger.SeenUids);
        Assert.Contains("tie", session.Ledger.SeenUids);
        Assert.True(session.Ledger.HasEntry(week));
    }

    [Fact]
    public void CatchUp_WithoutTheOwnDocument_ReseedsTheSettledSet()
    {
        int week = CurrentWeekAtStart();
        var board = new FakeValuationBoard { OwnUid = "me" };
        board.Entries = [Entry("rival", "Rival", 90.0)];
        TestSessionResult session = Create(
            board: board, saved: SavedWeek(week));

        session.Story.FinishDialogue();
        session.Vm.Poll();

        // The doc vanished (TTL, wipe): nothing can be won any more, so the
        // flow forgets the entry and settles what is left silently.
        Assert.False(session.Ledger.HasEntry(week));
        Assert.Null(session.Ledger.EntryCarKey);
        Assert.Contains("rival", session.Ledger.SeenUids);
        Assert.False(board.Watching);
    }

    [Fact]
    public void MondayMidnight_WipesTheEntryTheVenueAndTheOwnDocument()
    {
        var board = new FakeValuationBoard { OwnUid = "me", Entries = [] };
        var hud = new FakeTelemetryHud();
        TestSessionResult session = Create(
            board: board, hud: hud, garage: GarageWithTestCar());
        ValuationScreenViewModel page = OpenShowcase(session, hud);
        page.EnterCommand.Execute(null);
        session.Vm.Poll();
        int week = CurrentWeekAtStart();
        Assert.True(session.Ledger.HasEntry(week));

        // Monday noon to the next Monday 00:01 game time.
        double gameMinutes = (6 * 24 * 60) + (12 * 60) + 1;
        session.Clock.Advance(TimeSpan.FromSeconds(
            gameMinutes * GameClock.RealSecondsPerGameMinute));
        session.Vm.Poll();

        Assert.Equal(week + 1, session.Ledger.LastResetWeek);
        Assert.False(session.Ledger.HasEntry(week + 1));
        Assert.Null(session.Ledger.EntryCarKey);
        Assert.False(session.Ledger.HasVenue);
        Assert.Empty(session.Ledger.SeenUids);
        Assert.Equal(1, board.Wipes);
    }

    private static TestSessionResult Create(
        FakeValuationBoard? board = null, FakeTelemetryHud? hud = null,
        DriverWallet? wallet = null, Garage? garage = null,
        DriverState? saved = null) =>
        TestSession.Create(new TestSessionOptions
        {
            SpeedKmh = 0f,
            ValuationBoard = board ?? new FakeValuationBoard(),
            ValuationRng = new Random(0),
            Hud = hud,
            Wallet = wallet,
            Garage = garage,
            Cars = [TestCar],
            SavedDriver = saved,
        });

    private static Garage GarageWithTestCar()
    {
        var garage = new Garage([TestCar]);
        garage.Add(TestCar.Key, week: 1);
        // One settled job with the car: the score's job term must reach the push.
        garage.Find(TestCar.Key)!.RegisterJob();
        return garage;
    }

    // The Monday the default fake clock (2026-10-12 12:00) opens on: the week
    // every assertion above counts in.
    private static int CurrentWeekAtStart() =>
        ValuationRules.WeekKey(new DateTime(2026, 10, 12, 12, 0, 0));

    private static DriverState SavedWeek(int week) => DriverState.Fresh with
    {
        ValuationEntryWeek = week,
        ValuationEntryCarKey = TestCar.Key,
        ValuationOwnScore = 100.0,
        ValuationSeenUids = ["old"],
        LastValuationResetWeek = week,
    };

    private static CalendarScreenViewModel OpenCalendar(TestSessionResult session)
    {
        session.Vm.Show();
        var menu = Assert.IsType<MenuScreenViewModel>(session.Vm.CurrentScreen);
        menu.CalendarCommand.Execute(null);
        return Assert.IsType<CalendarScreenViewModel>(session.Vm.CurrentScreen);
    }

    private static ValuationScreenViewModel OpenShowcase(
        TestSessionResult session, FakeTelemetryHud hud)
    {
        session.Story.FinishDialogue();
        session.Vm.Poll();
        OpenCalendar(session).DayCommand.Execute(2);
        hud.AtDestination = true;
        session.Vm.Poll();
        return Assert.IsType<ValuationScreenViewModel>(session.Vm.CurrentScreen);
    }

    private static ValuationEntry Entry(string uid, string name, double score) =>
        new(uid, name, TestCar.Key, 1, 0.0, score);
}
