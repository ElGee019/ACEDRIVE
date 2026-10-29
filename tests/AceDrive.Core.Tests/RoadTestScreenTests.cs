using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// La Strada's road-test page (Unity RoadTestScreen): the car under the title,
/// the two legs with their briefs, the photo mission Unity only offers once both
/// are done, then the result view's sentence and board — the own row
/// highlighted, repeated under Unity's separator when it fell outside the slice,
/// in the board's own unit (seconds or metres). The page computes no number: it
/// paints the standings the flow hands it.
/// </summary>
public sealed class RoadTestScreenTests
{
    private static readonly CarSpec Car = new("ks_car", 1990, "Mazda", "MX-5 NA", 1);

    [Fact]
    public void TestCentre_NamesTheCarAndOffersBothLegsWithTheirBriefs()
    {
        int accel = 0;
        int brake = 0;
        var page = CreatePage(onAccel: () => accel++, onBrake: () => brake++);

        Assert.True(page.IsReady);
        Assert.False(page.IsResults);
        Assert.Equal("ROAD TEST", page.Title);
        Assert.Equal("1990 MAZDA MX-5 NA", page.CarName);
        Assert.Equal("0-60", page.AccelLabel);
        Assert.Equal("100-0", page.BrakeLabel);
        Assert.Equal("PHOTO MISSION", page.PhotoLabel);
        Assert.Equal(
            "Set a 0-60 time we can publish in our magazine. Launch the car from 0. " +
            "You will see the countdown timer on the mini window.",
            page.AccelText);
        Assert.Equal(
            "Take a 100-0 braking test. Bring the car to 100, watch the countdown " +
            "timer and brake.",
            page.BrakeText);
        Assert.Equal(
            "Drive anywhere across the map so the photographer can get his shots. " +
            "Switching up the location from our last shoot will keep things " +
            "interesting for our readers.",
            page.PhotoText);
        Assert.Empty(page.Rows);
        Assert.True(page.ShowBack);
        Assert.False(page.ShowPhoto);
        Assert.True(page.AccelCommand.CanExecute(null));
        Assert.True(page.BrakeCommand.CanExecute(null));

        page.AccelCommand.Execute(null);
        page.BrakeCommand.Execute(null);

        Assert.Equal(1, accel);
        Assert.Equal(1, brake);
    }

    [Fact]
    public void Result_ShowsTheTopSliceWithTheOwnRowHighlighted()
    {
        var page = CreatePage();

        page.ShowResult(
            "You went from 0 to 60 km/h in 5.400 seconds.", Standings(), Cars(),
            RoadTestMetric.AccelerationTime);

        Assert.True(page.IsResults);
        Assert.False(page.IsReady);
        Assert.True(page.HasBoard);
        Assert.True(page.IsWaiting);
        Assert.Equal(2, page.Rows.Count);
        Assert.Equal("1", page.Rows[0].PositionText);
        Assert.True(page.Rows[0].IsOwn);
        Assert.Equal("5.400s", page.Rows[0].Result);
        Assert.False(page.Rows[1].IsOwn);
        // The board read settles the waiting line without dropping the rows.
        page.ShowBoard(Standings());
        Assert.False(page.IsWaiting);
        Assert.Equal(2, page.Rows.Count);
    }

    [Fact]
    public void Result_ShowsTheAllCarsRecordBookWithTheTestedCarHighlighted()
    {
        var page = CreatePage();

        page.ShowResult(
            "You went from 0 to 60 km/h in 5.400 seconds.", Standings(), Cars(),
            RoadTestMetric.AccelerationTime);

        // Unity's overall standings: every tested car, one row each, the car
        // under test highlighted instead of the player's own row.
        Assert.True(page.HasAllCarsBoard);
        Assert.Equal("ALL CARS", page.AllCarsTitle);
        Assert.Equal(2, page.AllCarsRows.Count);
        Assert.Equal("1", page.AllCarsRows[0].PositionText);
        Assert.Equal("1990 MAZDA MX-5 NA", page.AllCarsRows[0].Name);
        Assert.Equal("5.400s", page.AllCarsRows[0].Result);
        Assert.True(page.AllCarsRows[0].IsOwn);
        Assert.Equal("2001 PORSCHE 911", page.AllCarsRows[1].Name);
        Assert.False(page.AllCarsRows[1].IsOwn);

        // The live answer replaces the local row without touching the car's board.
        page.ShowAllCarsBoard(
        [
            new RoadTestCarStanding(1, "2001 PORSCHE 911", 4.9, IsTestedCar: false),
            new RoadTestCarStanding(2, "1990 MAZDA MX-5 NA", 5.4, IsTestedCar: true),
        ]);

        Assert.Equal(2, page.AllCarsRows.Count);
        Assert.Equal("2", page.AllCarsRows[1].PositionText);
        Assert.True(page.AllCarsRows[1].IsOwn);
        // The all-cars answer never touch the car's own board.
        Assert.Equal("1", page.Rows[0].PositionText);
    }

    [Fact]
    public void BrakeResult_PaintsTheBoardInMetres()
    {
        var page = CreatePage();

        page.ShowResult(
            "You went from 100 km/h to 0 in 40.4 metres driving the 1990 MAZDA MX-5 NA.",
            [new RoadTestStanding(1, "Ace", 40.44, IsOwn: true)],
            [new RoadTestCarStanding(1, "1990 MAZDA MX-5 NA", 40.44, IsTestedCar: true)],
            RoadTestMetric.BrakeDistance);

        // The brake board's own unit: metres at one decimal, never seconds.
        Assert.Equal("40.4 m", Assert.Single(page.Rows).Result);
        Assert.Equal("40.4 m", page.AllCarsRows[0].Result);
    }

    [Fact]
    public void Result_RepeatsAnOwnRowOutsideTheSliceUnderASeparator()
    {
        var page = CreatePage();
        page.ShowResult(
            "You went from 0 to 60 km/h in 5.400 seconds.", Standings(), Cars(),
            RoadTestMetric.AccelerationTime);

        page.ShowBoard(
        [
            new RoadTestStanding(1, "Rival", 4.1, IsOwn: false),
            new RoadTestStanding(2, "Other", 4.4, IsOwn: false),
            new RoadTestStanding(9, "Ace", 9.9, IsOwn: true),
        ]);

        Assert.Equal(4, page.Rows.Count);
        Assert.True(page.Rows[2].IsSeparator);
        Assert.Equal("...", page.Rows[2].PositionText);
        Assert.Equal("---", page.Rows[2].Name);
        Assert.Equal("9", page.Rows[3].PositionText);
        Assert.True(page.Rows[3].IsOwn);
    }

    [Fact]
    public void Continue_IsTheOnlyVerbAfterTheRun()
    {
        int continues = 0;
        var page = CreatePage(onContinue: () => continues++);
        page.ShowResult(
            "You went from 0 to 60 km/h in 5.400 seconds.", Standings(), Cars(),
            RoadTestMetric.AccelerationTime);

        Assert.False(page.AccelCommand.CanExecute(null));
        Assert.False(page.BrakeCommand.CanExecute(null));
        Assert.True(page.ContinueCommand.CanExecute(null));

        page.ContinueCommand.Execute(null);

        Assert.Equal(1, continues);
    }

    [Fact]
    public void TestCentreBack_ReArmsTheOtherLegAndBanksTheDrivenOne()
    {
        var page = CreatePage();
        page.ShowResult(
            "You went from 0 to 60 km/h in 5.400 seconds.", Standings(), Cars(),
            RoadTestMetric.AccelerationTime);

        page.ShowTestCentre();

        // CONTINUE leaves the finished leg banked for the visit: the brake leg
        // is next, and the accel button cannot be taken twice.
        Assert.True(page.IsReady);
        Assert.False(page.IsResults);
        Assert.False(page.CanAccel);
        Assert.True(page.CanBrake);
        Assert.False(page.ShowPhoto);
        Assert.True(page.ShowBack);
        Assert.Empty(page.Rows);
        Assert.Empty(page.AllCarsRows);
    }

    [Fact]
    public void PhotoMission_AppearsOnlyAfterBothLegsWereDriven()
    {
        int photos = 0;
        var page = CreatePage(onPhoto: () => photos++);
        page.ShowResult(
            "You went from 0 to 60 km/h in 5.400 seconds.", Standings(), Cars(),
            RoadTestMetric.AccelerationTime);
        page.ShowTestCentre();

        Assert.False(page.ShowPhoto);
        Assert.False(page.PhotoCommand.CanExecute(null));

        page.ShowResult(
            "You went from 100 km/h to 0 in 40.4 metres driving the 1990 MAZDA MX-5 NA.",
            [new RoadTestStanding(1, "Ace", 40.44, IsOwn: true)],
            [new RoadTestCarStanding(1, "1990 MAZDA MX-5 NA", 40.44, IsTestedCar: true)],
            RoadTestMetric.BrakeDistance);
        page.ShowTestCentre();

        // Unity's rule: the photo button is revealed by the two finished legs.
        Assert.True(page.ShowPhoto);
        Assert.True(page.PhotoCommand.CanExecute(null));

        page.PhotoCommand.Execute(null);

        Assert.Equal(1, photos);
    }

    [Fact]
    public void VoiceActions_FollowThePhase()
    {
        var page = CreatePage();
        Assert.Contains("0-60", page.VoiceActions.Keys);
        Assert.Contains("100-0", page.VoiceActions.Keys);

        page.ShowResult(
            "You went from 0 to 60 km/h in 5.400 seconds.", Standings(), Cars(),
            RoadTestMetric.AccelerationTime);

        Assert.DoesNotContain("0-60", page.VoiceActions.Keys);
        Assert.Contains("CONTINUE", page.VoiceActions.Keys);
    }

    [Fact]
    public void WrongCar_NamesThePickAndRefusesBothLegs()
    {
        int accel = 0;
        int brake = 0;
        var page = CreatePage(onAccel: () => accel++, onBrake: () => brake++,
            drivesThePick: false);

        // The board rows are the pick's: another car may not set them, and the
        // page says which car to enter instead of failing on the press. The way
        // out is gone too: leaving would throw the visit away, and the player
        // still has to swap cars and come back to this page.
        Assert.True(page.ShowPickRequired);
        Assert.False(page.CanAccel);
        Assert.False(page.CanBrake);
        Assert.False(page.ShowPhoto);
        Assert.False(page.ShowBack);
        Assert.False(page.AccelCommand.CanExecute(null));
        Assert.False(page.BrakeCommand.CanExecute(null));
        Assert.Contains("1990 MAZDA MX-5 NA", page.PickRequiredText, StringComparison.Ordinal);
        Assert.DoesNotContain("0-60", page.VoiceActions.Keys);
        Assert.DoesNotContain("100-0", page.VoiceActions.Keys);
        Assert.DoesNotContain("BACK", page.VoiceActions.Keys);

        page.AccelCommand.Execute(null);
        page.BrakeCommand.Execute(null);

        Assert.Equal(0, accel);
        Assert.Equal(0, brake);
    }

    [Fact]
    public void MarkPickArrived_RestoresTheLegsAndTheWayOut()
    {
        var page = CreatePage(drivesThePick: false);

        page.MarkPickArrived();

        // The car swap the page was waiting for: the legs come back with the
        // normal way out, so nothing has to be left and re-entered.
        Assert.True(page.DrivesThePick);
        Assert.False(page.ShowPickRequired);
        Assert.True(page.CanAccel);
        Assert.True(page.CanBrake);
        Assert.True(page.ShowBack);
        Assert.Contains("0-60", page.VoiceActions.Keys);
        Assert.Contains("100-0", page.VoiceActions.Keys);
        Assert.Contains("BACK", page.VoiceActions.Keys);
    }

    [Fact]
    public void MarkPickRequired_FlipsALivePageBackToThePickLine()
    {
        var page = CreatePage();
        Assert.True(page.CanAccel);

        page.MarkPickRequired();

        Assert.True(page.ShowPickRequired);
        Assert.False(page.CanAccel);
        Assert.False(page.BrakeCommand.CanExecute(null));
        Assert.Contains("1990 MAZDA MX-5 NA", page.PickRequiredText, StringComparison.Ordinal);
    }

    private static RoadTestScreenViewModel CreatePage(
        Action? onAccel = null, Action? onBrake = null, Action? onPhoto = null,
        Action? onContinue = null, Action? onBack = null, bool drivesThePick = true)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        return new RoadTestScreenViewModel(
            Car, drivesThePick, onAccel ?? NoOp, onBrake ?? NoOp, onPhoto ?? NoOp,
            onContinue ?? NoOp, new RelayCommand(onBack ?? NoOp), new VoiceBeat(new FakeClock()));
    }

    private static void NoOp()
    {
    }

    private static List<RoadTestStanding> Standings() =>
    [
        new(1, "Ace", 5.4, IsOwn: true),
        new(2, "Rival", 6.1, IsOwn: false),
    ];

    // The all-cars record book the flow hands the page: the tested car first.
    private static List<RoadTestCarStanding> Cars() =>
    [
        new(1, "1990 MAZDA MX-5 NA", 5.4, IsTestedCar: true),
        new(2, "2001 PORSCHE 911", 6.1, IsTestedCar: false),
    ];
}
