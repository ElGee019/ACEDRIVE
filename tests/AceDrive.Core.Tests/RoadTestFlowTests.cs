using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The road test end to end (PORTPLAN 49), driven the way the root drives it:
/// the Tuesday row sends the player to La Strada, the companion carries the
/// drive and then the legs, ENTER at the door opens the test centre page, its
/// 0-60 button times a standing launch and its 100-0 button measures the
/// braking distance from 100 km/h to a halt. Both finish on the same page and
/// CONTINUE hands the test centre back. Once both legs ran, the photo mission
/// puts the panel down and the companion's PHOTOSHOOT button takes the shot
/// wherever the car stands: the receipt is priced, banked and shown, and the
/// pick moves on there (Unity's MarkCurrentAsTested). The pick stays on offer
/// until the whole test, photo included, has been driven.
/// </summary>
public sealed class RoadTestFlowTests
{
    private static readonly AddressLocation LaStradaDoor =
        new(AddressMap.Default.Streets[0].Name, 2, HouseSide.Left);

    // A car that is never the week's pick: the road-test pool draws its own rows,
    // so this announcement is always a mismatch.
    private static readonly CarIdentity MazdaIdentity = new("ks_mazda_mx5_na", null);

    private static readonly string TimerPattern = @"^\d+\.\d{3}$";
    private static readonly string RowTimePattern = @"^\d+\.\d{3}s$";
    private static readonly string MetresPattern = @"^\d+\.\d m$";

    // A career past the introduction, with the tour's La Strada door banked —
    // the one address the road-test leg pins.
    private static OnboardingState FinishedCareer() => OnboardingState.Fresh with
    {
        BirthdayDone = true,
        CommunityCentreDone = true,
        LaStradaAddress = LaStradaDoor,
    };

    [Fact]
    public void CalendarTap_PinsLaStradaAndKeepsThePick()
    {
        var (session, hud, _, roads, bank) = Create();
        string? pick = roads.Current?.Key;

        TapTheRoadTestRow(session);

        Assert.Equal(RoadTestPhase.Driving, session.RoadTest.Phase);
        Assert.Equal("HudRoadTestDrive", hud.Copy?.DriveKey);
        Assert.Equal(StoryLaStrada.Definition.Id, hud.Destination?.Id);
        Assert.Equal(1, hud.ShowTaskTextCalls);
        Assert.False(session.Vm.IsShown);
        // The pick is only banked once the whole test is driven, so the row and
        // the disk both keep naming the same car.
        Assert.Equal(pick, roads.Current?.Key);
        Assert.Empty(bank.Saved);
    }

    [Fact]
    public void EnterAtTheDoor_OpensTheTestCentrePage()
    {
        var (session, hud, _, roads, _) = Create();
        TapTheRoadTestRow(session);
        hud.AtDestination = true;

        hud.RaiseEnter();

        var page = Assert.IsType<RoadTestScreenViewModel>(session.Vm.CurrentScreen);
        Assert.True(session.Vm.IsShown);
        Assert.True(page.IsReady);
        Assert.Equal(CarTitles.Of(roads.Current!), page.CarName);
        Assert.Equal("0-60", page.AccelLabel);
        Assert.Equal("100-0", page.BrakeLabel);
        // The photo mission is Unity's both-legs reward: hidden on arrival.
        Assert.False(page.ShowPhoto);
        // The companion's guidance is consumed by the arrival.
        Assert.Null(hud.Destination);
        Assert.Null(hud.RoadTest);
        Assert.Equal(RoadTestPhase.AtTestCentre, session.RoadTest.Phase);
    }

    [Fact]
    public void Start_ShowsTheCounterOnTheCompanionThenTheTimer()
    {
        var (session, hud, _, _, _) = Create();
        RoadTestScreenViewModel page = OpenTestCentre(session, hud);

        page.AccelCommand.Execute(null);

        Assert.Equal(RoadTestPhase.Countdown, session.RoadTest.Phase);
        Assert.False(session.Vm.IsShown);
        Assert.Equal("HudRoadTestTask", hud.RoadTest?.TaskKey);
        Assert.Equal("5", hud.RoadTest?.ClockText);

        Advance(session, 1.1);

        Assert.Equal("4", hud.RoadTest?.ClockText);

        // The counter's five seconds plus the longest reaction hold: the green
        // light has definitely come, so the clock line is the stopwatch now.
        Advance(session, 8.5);

        Assert.Equal(RoadTestPhase.Timing, session.RoadTest.Phase);
        Assert.Matches(TimerPattern, hud.RoadTest?.ClockText ?? string.Empty);
    }

    [Fact]
    public void StartWhileRolling_HoldsTheCounterAndRestartsItFromFiveOnceHalted()
    {
        var (session, hud, board, _, _) = Create();
        RoadTestScreenViewModel page = OpenTestCentre(session, hud);
        session.Tel.Next = session.Tel.Next with { SpeedKmh = 20f };

        page.AccelCommand.Execute(null);
        Advance(session, 0.2);

        // Rolling: the counter parks at five and the block asks for the halt.
        Assert.Equal(RoadTestPhase.Countdown, session.RoadTest.Phase);
        Assert.Equal("HudRoadTestHalt", hud.RoadTest?.TaskKey);
        Assert.Equal("5", hud.RoadTest?.ClockText);

        // A long roll never runs the counter down to a green the player could
        // not have used.
        Advance(session, 4.0);

        Assert.Equal(RoadTestPhase.Countdown, session.RoadTest.Phase);
        Assert.Equal("5", hud.RoadTest?.ClockText);
        Assert.Empty(board.Pushed);

        session.Tel.Next = session.Tel.Next with { SpeedKmh = 0f };
        Advance(session, 0.2);

        // Halted: the target line is back and the count starts over at five.
        Assert.Equal(RoadTestPhase.Countdown, session.RoadTest.Phase);
        Assert.Equal("HudRoadTestTask", hud.RoadTest?.TaskKey);
        Assert.Equal("5", hud.RoadTest?.ClockText);

        Advance(session, 1.1);
        Assert.Equal("4", hud.RoadTest?.ClockText);

        // The restarted count runs to the green light like any standing start.
        Advance(session, 8.5);
        Assert.Equal(RoadTestPhase.Timing, session.RoadTest.Phase);
    }

    [Fact]
    public void ReachingSixty_OpensTheResultPageAndPushesTheRun()
    {
        var (session, hud, board, roads, bank) = Create();
        string pick = roads.Current!.Key;
        RoadTestScreenViewModel page = OpenTestCentre(session, hud);
        page.AccelCommand.Execute(null);
        Advance(session, 8.5); // past the five-second counter and the longest hold
        session.Tel.Next = session.Tel.Next with { SpeedKmh = 55f };
        Advance(session, 0.1);
        session.Tel.Next = session.Tel.Next with { SpeedKmh = 64f };
        Advance(session, 0.1);

        page = Assert.IsType<RoadTestScreenViewModel>(session.Vm.CurrentScreen);
        Assert.Equal(RoadTestPhase.Result, session.RoadTest.Phase);
        Assert.True(page.IsResults);
        Assert.True(session.Vm.IsShown);
        Assert.Null(hud.RoadTest);
        Assert.Contains("0 to 60 km/h", page.ResultText, StringComparison.Ordinal);
        Assert.Contains(page.CarName, page.ResultText, StringComparison.Ordinal);
        // The local row is up before the board read lands (Unity's order).
        RoadTestRow row = Assert.Single(page.Rows);
        Assert.True(row.IsOwn);
        Assert.Matches(RowTimePattern, row.Result);
        // The all-cars book paints the same run under the car's name while its
        // own read is still out.
        RoadTestRow carRow = Assert.Single(page.AllCarsRows);
        Assert.True(carRow.IsOwn);
        Assert.Equal(page.CarName, carRow.Name);
        Assert.Matches(RowTimePattern, carRow.Result);

        var pushed = Assert.Single(board.Pushed);
        Assert.Equal(RoadTestMetric.AccelerationTime, pushed.Metric);
        Assert.Equal(pick, pushed.CarKey);
        // The run is measured from the green light, so it is shorter than the
        // eight seconds the leg was watched for but never zero.
        Assert.InRange(pushed.Value, 0.001, 6.0);
        // The pick stays; only a complete test may move it.
        Assert.Equal(pick, roads.Current?.Key);
        Assert.Empty(bank.Saved);
    }

    [Fact]
    public void ImprovedRun_UpdatesTheCarBoardBeforeThePushLands()
    {
        var (session, hud, board, roads, _) = Create();
        string pick = roads.Current!.Key;
        board.OwnUid = "uid-1";
        // The stored best this run beats, and a rival still ahead of it: the
        // push has not landed, so the read answers the old 9.9 s.
        board.Board =
        [
            new RoadTestEntry("uid-2", "Rival", pick, 0.001, "2026-01-02T00:00:00Z"),
            new RoadTestEntry("uid-1", "Ace", pick, 9.9, "2026-01-01T00:00:00Z"),
        ];
        RoadTestScreenViewModel page = OpenTestCentre(session, hud);
        page.AccelCommand.Execute(null);
        Advance(session, 8.5);
        session.Tel.Next = session.Tel.Next with { SpeedKmh = 55f };
        Advance(session, 0.1);
        session.Tel.Next = session.Tel.Next with { SpeedKmh = 64f };
        Advance(session, 0.1);

        Advance(session, 0.1);

        // The improved run replaces the stored slower best at its own place, so
        // the leaderboard the player sees is the one they just drove.
        page = Assert.IsType<RoadTestScreenViewModel>(session.Vm.CurrentScreen);
        Assert.Equal(2, page.Rows.Count);
        Assert.False(page.Rows[0].IsOwn);
        Assert.Equal("2", page.Rows[1].PositionText);
        Assert.True(page.Rows[1].IsOwn);
        Assert.Matches(RowTimePattern, page.Rows[1].Result);
        Assert.NotEqual("9.900s", page.Rows[1].Result);
    }

    [Fact]
    public void Result_SettlesTheAllCarsRecordBookOnTheNextPoll()
    {
        var (session, hud, board, roads, _) = Create();
        string pick = roads.Current!.Key;
        // The book the read will answer with: another player's faster car, then
        // this one's stored best.
        board.Cars =
        [
            new RoadTestEntry("uid-2", "Rival", "ks_other", 0.05, "2026-01-02T00:00:00Z"),
            new RoadTestEntry("uid-1", "Ace", pick, 5.4, "2026-01-01T00:00:00Z"),
        ];
        RoadTestScreenViewModel page = OpenTestCentre(session, hud);
        page.AccelCommand.Execute(null);
        Advance(session, 8.5);
        session.Tel.Next = session.Tel.Next with { SpeedKmh = 55f };
        Advance(session, 0.1);
        session.Tel.Next = session.Tel.Next with { SpeedKmh = 64f };
        Advance(session, 0.1);

        page = Assert.IsType<RoadTestScreenViewModel>(session.Vm.CurrentScreen);
        Assert.Single(page.AllCarsRows);

        Advance(session, 0.1);

        // The book arrives: every car, its best, and the tested car keeps the
        // highlight wherever it stands. A car outside the catalogue still reads
        // as something instead of blanking its row.
        Assert.Equal(2, page.AllCarsRows.Count);
        Assert.Equal("KS_OTHER", page.AllCarsRows[0].Name);
        Assert.False(page.AllCarsRows[0].IsOwn);
        RoadTestRow tested = Assert.Single(page.AllCarsRows, row => row.IsOwn);
        Assert.Equal(page.CarName, tested.Name);
        Assert.Matches(RowTimePattern, tested.Result);
    }

    [Fact]
    public void BrakeLeg_MeasuresFromHundredToTheHaltAndBanksMetres()
    {
        var (session, hud, board, roads, bank) = Create();
        string pick = roads.Current!.Key;
        RoadTestScreenViewModel page = OpenTestCentre(session, hud);

        page.BrakeCommand.Execute(null);

        // Pressing 100-0 arms the leg, but nothing is measured until the car is
        // at 100: the companion names the start and the clock is still at zero.
        Assert.Equal(RoadTestPhase.BrakeApproach, session.RoadTest.Phase);
        Assert.False(session.Vm.IsShown);
        Assert.Equal("HudRoadTestBrake", hud.RoadTest?.TaskKey);
        Assert.Matches(MetresPattern, hud.RoadTest?.ClockText ?? string.Empty);

        session.Tel.Next = session.Tel.Next with { SpeedKmh = 90f };
        Advance(session, 0.5);

        Assert.Equal(RoadTestPhase.BrakeApproach, session.RoadTest.Phase);
        Assert.Equal("0.0 m", hud.RoadTest?.ClockText);

        // 100 km/h starts the run; the ticks below integrate down to the halt.
        session.Tel.Next = session.Tel.Next with { SpeedKmh = 100f };
        Advance(session, 0.1);
        Assert.Equal(RoadTestPhase.BrakeTiming, session.RoadTest.Phase);

        session.Tel.Next = session.Tel.Next with { SpeedKmh = 80f };
        Advance(session, 0.1);
        session.Tel.Next = session.Tel.Next with { SpeedKmh = 40f };
        Advance(session, 0.1);

        Assert.Equal(RoadTestPhase.BrakeTiming, session.RoadTest.Phase);
        Assert.Matches(MetresPattern, hud.RoadTest?.ClockText ?? string.Empty);

        session.Tel.Next = session.Tel.Next with { SpeedKmh = 0f };
        Advance(session, 0.1);

        page = Assert.IsType<RoadTestScreenViewModel>(session.Vm.CurrentScreen);
        Assert.Equal(RoadTestPhase.Result, session.RoadTest.Phase);
        Assert.True(page.IsResults);
        Assert.Null(hud.RoadTest);
        Assert.Contains("0 in 4.7 metres", page.ResultText, StringComparison.Ordinal);
        Assert.Equal("4.7 m", Assert.Single(page.Rows).Result);
        // The all-cars book carries the same reading in the brake board's unit.
        Assert.Equal("4.7 m", Assert.Single(page.AllCarsRows).Result);

        var pushed = Assert.Single(board.Pushed);
        Assert.Equal(RoadTestMetric.BrakeDistance, pushed.Metric);
        Assert.Equal(pick, pushed.CarKey);
        Assert.InRange(pushed.Value, 4.6, 4.8);
        Assert.Equal(pick, roads.Current?.Key);
        Assert.Empty(bank.Saved);
    }

    [Fact]
    public void BrakeLeg_ReadsTheStartSpeedInTheCompanionUnit()
    {
        var (session, hud, _, _, _) = Create(units: MileageUnit.Miles);
        OpenTestCentre(session, hud).BrakeCommand.Execute(null);

        // The flow hands the HUD the brake task and the 100 km/h gate; the HUD
        // is the one owner that renders it in the player's unit (62 mph).
        Assert.Equal("HudRoadTestBrake", hud.RoadTest?.TaskKey);
        Assert.Equal(RoadTestRules.BrakeStartSpeedKmh, hud.RoadTest?.TargetKmh);
    }

    [Fact]
    public void ContinueAfterTheRun_ReturnsToTheTestCentreWithTheOtherLegOpen()
    {
        var (session, hud, _, _, _) = Create();
        RoadTestScreenViewModel page = OpenTestCentre(session, hud);
        page.AccelCommand.Execute(null);
        Advance(session, 8.5);
        session.Tel.Next = session.Tel.Next with { SpeedKmh = 55f };
        Advance(session, 0.1);
        session.Tel.Next = session.Tel.Next with { SpeedKmh = 64f };
        Advance(session, 0.1);

        page = Assert.IsType<RoadTestScreenViewModel>(session.Vm.CurrentScreen);
        page.ContinueCommand.Execute(null);

        // The visit stays: the leg just driven is banked, the brake leg is next,
        // and the same page instance is revealed again.
        Assert.Equal(RoadTestPhase.AtTestCentre, session.RoadTest.Phase);
        Assert.True(session.Vm.IsShown);
        Assert.Same(page, session.Vm.CurrentScreen);
        Assert.True(page.IsReady);
        Assert.False(page.CanAccel);
        Assert.True(page.CanBrake);
        Assert.Null(hud.RoadTest);
        // Both boards belonged to the result view.
        Assert.Empty(page.AllCarsRows);
    }

    [Fact]
    public void PhotoMission_AfterBothLegs_OffersTheShutterOnTheCompanion()
    {
        var wallet = new DriverWallet();
        var (session, hud, _, _, _) = Create(wallet: wallet);
        RoadTestScreenViewModel page = OpenTestCentre(session, hud);

        // Only the accel leg so far: operator's both-legs rule keeps it hidden,
        // and the flow refuses a call the page would not have offered.
        session.RoadTest.PhotoMission();
        Assert.Null(hud.RoadTest);
        Assert.False(page.ShowPhoto);

        RunBothLegs(session, page);

        Assert.True(page.ShowPhoto);
        page.PhotoCommand.Execute(null);

        // The mission: the panel steps aside and the companion carries the
        // drive-anywhere brief with the PHOTOSHOOT button, never a clock.
        Assert.Equal(RoadTestPhase.PhotoMission, session.RoadTest.Phase);
        Assert.False(session.Vm.IsShown);
        Assert.Equal("HudRoadTestPhoto", hud.RoadTest?.TaskKey);
        Assert.True(hud.RoadTest?.Photoshoot);
        Assert.Equal(string.Empty, hud.RoadTest?.ClockText);

        // The shutter without a reading cannot be priced: the mission stays up
        // and the companion says why.
        session.Tel.Next = session.Tel.Next with { NormalizedPosition = null };
        Advance(session, 0.1);
        hud.RaisePhotoshoot();

        Assert.Equal(RoadTestPhase.PhotoMission, session.RoadTest.Phase);
        Assert.Equal(Strings.Get("RoadTestPhotoNoPosition"), hud.Notice);
    }

    [Fact]
    public void Photoshoot_PricesTheIssueBanksItAndShowsTheReceipt()
    {
        var wallet = new DriverWallet();
        var (session, hud, _, roads, bank) = Create(wallet: wallet);
        string pick = roads.Current!.Key;
        double before = wallet.Cash;
        RoadTestScreenViewModel page = OpenTestCentre(session, hud);
        RunBothLegs(session, page);
        page.PhotoCommand.Execute(null);

        // Parked on the first street's first houses: the shoot resolves a door.
        session.Tel.Next = session.Tel.Next with
        {
            SpeedKmh = 0f,
            NormalizedPosition = 0.05f,
            Track = new TrackInfo("Eifel", "", 20_000.0),
        };
        Advance(session, 0.1);

        hud.RaisePhotoshoot();

        // The receipt page is up and the visit is over: the pick moved on, the
        // issue's forfeit landed in the wallet and the record was banked.
        var receipt = Assert.IsType<RoadTestReceiptScreenViewModel>(
            session.Vm.CurrentScreen);
        Assert.Equal(RoadTestPhase.Receipt, session.RoadTest.Phase);
        Assert.Null(hud.RoadTest);
        Assert.True(session.Vm.IsShown);
        Assert.True(receipt.HasPhotoLocation);
        Assert.Equal(
            before + RoadTestAudienceRules.Default.BaseForfeit, wallet.Cash, precision: 2);
        Assert.NotEqual(pick, roads.Current?.Key);
        Assert.Contains(pick, roads.TestedIds);
        Assert.Equal(1, roads.Record.NaturalReputation);
        RoadTestSnapshot saved = Assert.Single(bank.Saved);
        Assert.Equal(1, saved.Record?.NaturalReputation);
        Assert.Equal("Street 1", saved.Record?.PhotoStreet);

        // CONTINUE is the way out: the main page comes back, no new visit.
        receipt.ContinueCommand.Execute(null);

        Assert.Equal(RoadTestPhase.Idle, session.RoadTest.Phase);
        Assert.IsType<MenuScreenViewModel>(session.Vm.CurrentScreen);
    }

    [Fact]
    public void TappingTheRowAgainMidLeg_AbandonsTheRunAndReopensThePage()
    {
        var (session, hud, board, _, _) = Create();
        RoadTestScreenViewModel page = OpenTestCentre(session, hud);
        page.AccelCommand.Execute(null);
        Advance(session, 1.0);
        Assert.Equal(RoadTestPhase.Countdown, session.RoadTest.Phase);

        // The calendar row is the way out of a leg that never crosses: no
        // result is banked and the page comes back ready for another try.
        session.RoadTest.Begin();

        Assert.Equal(RoadTestPhase.AtTestCentre, session.RoadTest.Phase);
        Assert.Null(hud.RoadTest);
        Assert.IsType<RoadTestScreenViewModel>(session.Vm.CurrentScreen);
        Assert.True(session.Vm.IsShown);
        Assert.Empty(board.Pushed);
    }

    [Fact]
    public void EnterWithoutABegunTest_OpensNothing()
    {
        var (session, hud, _, _, _) = Create();
        hud.AtDestination = true;

        hud.RaiseEnter();

        // A stray visit to La Strada is not a test: the handler answers null and
        // the door keeps whatever handling it already had.
        Assert.Equal(RoadTestPhase.Idle, session.RoadTest.Phase);
        Assert.IsNotType<RoadTestScreenViewModel>(session.Vm.CurrentScreen);
    }

    [Fact]
    public void WrongCarAtTheDoor_RefusesBothLegsAndNamesThePick()
    {
        var (session, hud, board, roads, _) = Create(
            brands: new FakeCarBrandSource { CurrentIdentity = MazdaIdentity });
        string pick = roads.Current!.Key;
        RoadTestScreenViewModel page = OpenTestCentre(session, hud);

        // The board rows are the pick's, so another car may not set them: the
        // page names the car to bring and neither leg leaves the test centre.
        Assert.False(page.DrivesThePick);
        Assert.True(page.ShowPickRequired);
        Assert.False(page.ShowBack);
        Assert.False(page.AccelCommand.CanExecute(null));
        Assert.False(page.BrakeCommand.CanExecute(null));
        Assert.Contains(page.CarName, page.PickRequiredText, StringComparison.Ordinal);
        Assert.Contains("Change into that car.", page.PickRequiredText, StringComparison.Ordinal);

        session.RoadTest.StartAccel();
        session.RoadTest.StartBrake();

        Assert.Equal(RoadTestPhase.AtTestCentre, session.RoadTest.Phase);
        Assert.Null(hud.RoadTest);
        Assert.Empty(board.Pushed);
        Assert.Equal(page.PickRequiredText, hud.Notice);
        Assert.NotEqual(pick, MazdaIdentity.KsId);
    }

    [Fact]
    public void SwappingToThePickAtTheDoor_ReArmsThePageWithoutLeavingTheVisit()
    {
        var brands = new FakeCarBrandSource { CurrentIdentity = MazdaIdentity };
        var (session, hud, board, roads, _) = Create(brands: brands);
        CarSpec pick = roads.Current!;
        RoadTestScreenViewModel page = OpenTestCentre(session, hud);

        Assert.True(page.ShowPickRequired);
        Assert.False(page.ShowBack);

        // The player fetches the pick with the page still up: the poll sees the
        // new car and hands the page back ready to run, no re-entry needed.
        brands.CurrentIdentity = new CarIdentity(pick.Id, pick.Preset);
        Advance(session, 0.1);

        Assert.Equal(RoadTestPhase.AtTestCentre, session.RoadTest.Phase);
        Assert.True(page.DrivesThePick);
        Assert.False(page.ShowPickRequired);
        Assert.True(page.CanAccel);
        Assert.True(page.CanBrake);
        Assert.True(page.ShowBack);
        Assert.Null(hud.Notice);
        Assert.Empty(board.Pushed);

        page.AccelCommand.Execute(null);

        Assert.Equal(RoadTestPhase.Countdown, session.RoadTest.Phase);
    }

    [Fact]
    public void SwappingAwayFromThePickAtTheDoor_FlipsThePageToThePickLine()
    {
        var brands = new FakeCarBrandSource();
        var (session, hud, board, roads, _) = Create(brands: brands);
        CarSpec pick = roads.Current!;
        brands.CurrentIdentity = new CarIdentity(pick.Id, pick.Preset);
        RoadTestScreenViewModel page = OpenTestCentre(session, hud);
        Assert.True(page.CanAccel);

        brands.CurrentIdentity = MazdaIdentity;
        Advance(session, 0.1);

        // The page follows the live car in both directions, so a swap before
        // the press is already visible instead of waiting for a refused start.
        Assert.Equal(RoadTestPhase.AtTestCentre, session.RoadTest.Phase);
        Assert.True(page.ShowPickRequired);
        Assert.False(page.CanAccel);
        Assert.False(page.ShowBack);
        Assert.Empty(board.Pushed);
    }

    [Fact]
    public void BackWhileThePickIsRequired_KeepsTheVisitOpen()
    {
        var (session, hud, _, _, _) = Create(
            brands: new FakeCarBrandSource { CurrentIdentity = MazdaIdentity });
        RoadTestScreenViewModel page = OpenTestCentre(session, hud);
        Assert.False(page.ShowBack);

        session.RoadTest.Back();

        // The wrong-car page has no way out: leaving would throw away the one
        // visit from which the test can still be driven.
        Assert.Equal(RoadTestPhase.AtTestCentre, session.RoadTest.Phase);
        Assert.True(session.Vm.IsShown);
        Assert.Same(page, session.Vm.CurrentScreen);
    }

    [Fact]
    public void SwappingCarMidAccelRun_VoidsTheRunInsteadOfBankingIt()
    {
        var brands = new FakeCarBrandSource();
        var (session, hud, board, roads, _) = Create(brands: brands);
        CarSpec pick = roads.Current!;
        // Arrived in the pick: the leg is allowed to run.
        brands.CurrentIdentity = new CarIdentity(pick.Id, pick.Preset);
        RoadTestScreenViewModel page = OpenTestCentre(session, hud);
        Assert.True(page.DrivesThePick);
        page.AccelCommand.Execute(null);
        Advance(session, 8.5);

        // A car swap while the clock runs: the crossing belongs to another car.
        brands.CurrentIdentity = MazdaIdentity;
        session.Tel.Next = session.Tel.Next with { SpeedKmh = 55f };
        Advance(session, 0.1);
        session.Tel.Next = session.Tel.Next with { SpeedKmh = 64f };
        Advance(session, 0.1);

        Assert.Equal(RoadTestPhase.AtTestCentre, session.RoadTest.Phase);
        Assert.Empty(board.Pushed);
        Assert.Null(hud.RoadTest);
        Assert.True(Assert.IsType<RoadTestScreenViewModel>(session.Vm.CurrentScreen).ShowPickRequired);
        Assert.Equal(page.PickRequiredText, hud.Notice);
    }

    [Fact]
    public void SwappingCarMidBrakeRun_VoidsTheRunInsteadOfBankingIt()
    {
        var brands = new FakeCarBrandSource();
        var (session, hud, board, roads, _) = Create(brands: brands);
        CarSpec pick = roads.Current!;
        brands.CurrentIdentity = new CarIdentity(pick.Id, pick.Preset);
        RoadTestScreenViewModel page = OpenTestCentre(session, hud);
        page.BrakeCommand.Execute(null);
        session.Tel.Next = session.Tel.Next with { SpeedKmh = 100f };
        Advance(session, 0.1);
        Assert.Equal(RoadTestPhase.BrakeTiming, session.RoadTest.Phase);

        // The measuring car is swapped for another before the halt: the metres
        // would be the pick's record with another car's brakes.
        brands.CurrentIdentity = MazdaIdentity;
        session.Tel.Next = session.Tel.Next with { SpeedKmh = 0f };
        Advance(session, 0.1);

        Assert.Equal(RoadTestPhase.AtTestCentre, session.RoadTest.Phase);
        Assert.Empty(board.Pushed);
        Assert.Null(hud.RoadTest);
        Assert.True(Assert.IsType<RoadTestScreenViewModel>(session.Vm.CurrentScreen).ShowPickRequired);
        Assert.Equal(page.PickRequiredText, hud.Notice);
    }

    [Fact]
    public void SwappingCarBeforeTheRun_RefusesTheStartAndFlipsThePage()
    {
        var brands = new FakeCarBrandSource();
        var (session, hud, board, roads, _) = Create(brands: brands);
        CarSpec pick = roads.Current!;
        brands.CurrentIdentity = new CarIdentity(pick.Id, pick.Preset);
        RoadTestScreenViewModel page = OpenTestCentre(session, hud);
        Assert.True(page.CanAccel);

        // A swap while the page was being read: the press must not start a leg
        // whose time would land on the pick's row.
        brands.CurrentIdentity = MazdaIdentity;
        session.RoadTest.StartAccel();

        Assert.Equal(RoadTestPhase.AtTestCentre, session.RoadTest.Phase);
        Assert.Null(hud.RoadTest);
        Assert.False(page.CanAccel);
        Assert.False(page.CanBrake);
        Assert.True(page.ShowPickRequired);
        Assert.Equal(page.PickRequiredText, hud.Notice);
        Assert.Empty(board.Pushed);
    }

    // Both legs, the quick way: a standing 0-60 launch and a 100-0 run down to
    // the halt, closed with CONTINUE after each so the page is back on the test
    // centre with both legs banked.
    private static void RunBothLegs(TestSessionResult session, RoadTestScreenViewModel page)
    {
        page.AccelCommand.Execute(null);
        Advance(session, 8.5);
        session.Tel.Next = session.Tel.Next with { SpeedKmh = 55f };
        Advance(session, 0.1);
        session.Tel.Next = session.Tel.Next with { SpeedKmh = 64f };
        Advance(session, 0.1);
        page.ContinueCommand.Execute(null);

        page.BrakeCommand.Execute(null);
        session.Tel.Next = session.Tel.Next with { SpeedKmh = 100f };
        Advance(session, 0.1);
        session.Tel.Next = session.Tel.Next with { SpeedKmh = 0f };
        Advance(session, 0.1);
        page.ContinueCommand.Execute(null);
    }

    private static (TestSessionResult Session, FakeTelemetryHud Hud,
        FakeRoadTestStandings Board, RoadTestLedger Roads, FakeRoadTestStore Bank)
        Create(
            FakeRoadTestStandings? board = null,
            MileageUnit units = MileageUnit.Kilometres,
            FakeCarBrandSource? brands = null,
            DriverWallet? wallet = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var hud = new FakeTelemetryHud { Units = units };
        var standings = board ?? new FakeRoadTestStandings();
        var roads = TestRoadCars.Ledger();
        var bank = new FakeRoadTestStore();
        TestSessionResult session = TestSession.Create(new TestSessionOptions
        {
            SpeedKmh = 0f,
            Hud = hud,
            RoadTestStandings = standings,
            RoadTestStore = bank,
            Roads = roads,
            // The wallet the receipt pays into: the default session wallet is
            // fine for the leg tests, a suite that reads the payout hands one in.
            Wallet = wallet,
            // No announced car unless a case pins one: an unknown car is not a
            // mismatch, which keeps every other case on the normal path.
            CarBrands = brands,
            RoadTestRng = new Random(0),
            Progress = FinishedCareer(),
            // TEMPORARY (testing) in production too: the shipping gate is the
            // calendar's, which CalendarScreenTests covers.
            IgnoreEventGates = true,
        });
        return (session, hud, standings, roads, bank);
    }

    // The Tuesday row, opened the way the player reaches it: a call, the menu's
    // CALENDAR tile, then the road-test day.
    private static void TapTheRoadTestRow(TestSessionResult session)
    {
        session.Vm.Show();
        var menu = Assert.IsType<MenuScreenViewModel>(session.Vm.CurrentScreen);
        menu.CalendarCommand.Execute(null);
        var calendar = Assert.IsType<CalendarScreenViewModel>(session.Vm.CurrentScreen);
        int roadTestDay = ControlCalendar.Week
            .Single(day => day.EventId == ControlCalendar.RoadTest).DayOfWeek;
        calendar.DayCommand.Execute(roadTestDay);
    }

    private static RoadTestScreenViewModel OpenTestCentre(
        TestSessionResult session, FakeTelemetryHud hud)
    {
        TapTheRoadTestRow(session);
        hud.AtDestination = true;
        hud.RaiseEnter();
        return Assert.IsType<RoadTestScreenViewModel>(session.Vm.CurrentScreen);
    }

    // Real time only: the poll reads the fake clock, so advancing it and calling
    // the poll is exactly what one 10 Hz tick does live.
    private static void Advance(TestSessionResult session, double seconds)
    {
        session.Clock.Advance(TimeSpan.FromSeconds(seconds));
        session.Vm.Poll();
    }
}
