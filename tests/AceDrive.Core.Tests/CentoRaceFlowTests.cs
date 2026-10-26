using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The Cento Miglia entry runs headless (PORTPLAN 49, Unity
/// MilleMigliaManager + MilleMigliaHandler): drive to the start, pass the
/// registration check, race to the finish with the reputation growing with the
/// distance driven, settle. The shell is a fake, so each test sees only the
/// flow's own decisions.</summary>
public sealed class CentoRaceFlowTests
{
    // One poll of the real 10 Hz loop: the dt every flow here is fed.
    private const double PollSeconds = 0.1;

    private static readonly AddressLocation StartDoor =
        new("Street 1", 1, HouseSide.Right);
    private static readonly AddressLocation FinishDoor =
        new("Street 2", 1, HouseSide.Right);

    private sealed record Harness(
        CentoRaceFlow Flow, FakeCentoRaceHost Host, DriverWallet Wallet);

    private static CentoMigliaEvent Race(int registration = 7) =>
        new(1, "Gentlemen Drivers Club", registration, Year: 1924, Week: 3,
            new CentoRoutePlan(StartDoor, FinishDoor, 0.9645));

    private static Harness Create(double reputation = 1.0)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var wallet = new DriverWallet(
            DriverState.Fresh with { ClubReputation = reputation });
        var garage = new Garage(TestRoadCars.Pool);
        garage.Add(TestRoadCars.Pool[0], week: 1);
        var flow = new CentoRaceFlow(
            wallet, garage, new VoiceBeat(clock), new NullLogger());
        var host = new FakeCentoRaceHost();
        flow.Attach(host);
        return new Harness(flow, host, wallet);
    }

    // WHY the speed parameter: 0 is a parked car, anything above it is driving.
    private static TelemetrySample Sample(float speedKmh = 0f) =>
        new(1, 0f, 0f, 1, 900, 0f, speedKmh);

    private static CentoRegisterScreenViewModel Page(Harness harness) =>
        Assert.IsType<CentoRegisterScreenViewModel>(harness.Host.Revealed);

    // The whole way from the built event to the green light: the shell pins the
    // start door and hides the panel so the companion can guide the drive.
    private static CentoRegisterScreenViewModel ArriveAtStart(Harness harness, int registration = 7)
    {
        harness.Flow.Start(Race(registration));
        harness.Host.ParkedAtTarget = true;
        harness.Flow.Advance(Sample(), PollSeconds);
        return Page(harness);
    }

    // Drives the race leg at a steady speed, one 10 Hz poll at a time.
    private static void Drive(Harness harness, double seconds, float speedKmh)
    {
        int polls = (int)Math.Round(seconds / PollSeconds);
        for (int i = 0; i < polls; i++)
        {
            harness.Flow.Advance(Sample(speedKmh), PollSeconds);
        }
    }

    [Fact]
    public void Start_PinsTheStartDoorAndHidesThePanel()
    {
        Harness h = Create();
        CentoMigliaEvent race = Race();

        h.Flow.Start(race);

        Assert.Equal(CentoRacePhase.ToStart, h.Flow.Phase);
        Assert.True(h.Flow.InProgress);
        Assert.Same(race, h.Host.StartPinned);
        Assert.Equal(1, h.Host.HideCalls);
        Assert.Null(h.Host.Revealed);
    }

    [Fact]
    public void Start_IgnoresASecondEntry()
    {
        Harness h = Create();
        CentoMigliaEvent first = Race();
        h.Flow.Start(first);
        h.Flow.Start(Race(registration: 9));

        Assert.Same(first, h.Host.StartPinned);
        Assert.Equal(1, h.Host.HideCalls);
        Assert.Equal(CentoRacePhase.ToStart, h.Flow.Phase);
    }

    [Fact]
    public void ParkingAtTheStart_OpensTheRegistrationPage()
    {
        Harness h = Create();

        CentoRegisterScreenViewModel page = ArriveAtStart(h);

        Assert.Equal(CentoRacePhase.Verification, h.Flow.Phase);
        Assert.True(page.CanType);
        Assert.False(page.ShowContinue);
        Assert.Equal(
            "SELECTED: —",
            page.SelectedLine);
    }

    [Fact]
    public void WrongNumber_CostsAnAttemptAndTheSecondClosesTheEntry()
    {
        Harness h = Create();
        CentoRegisterScreenViewModel page = ArriveAtStart(h, registration: 12);

        page.DigitCommand.Execute(1);
        page.DigitCommand.Execute(3);
        Assert.True(page.HasFeedback);
        Assert.Equal("You got that wrong! Try again.", page.FeedbackLine);
        Assert.True(page.CanType);
        Assert.False(page.ShowContinue);

        page.DigitCommand.Execute(1);
        page.DigitCommand.Execute(4);
        Assert.True(page.IsClosed);
        Assert.False(page.CanType);
        Assert.False(page.ShowContinue);
        Assert.Equal("Wrong again. You are not allowed to enter.", page.FeedbackLine);

        // A closed entry cannot be started.
        page.ContinueCommand.Execute(null);
        Assert.Equal(CentoRacePhase.Verification, h.Flow.Phase);
        Assert.Null(h.Host.FinishPinned);
    }

    [Fact]
    public void AcceptedNumber_StartsTheRaceAndPinsTheFinish()
    {
        Harness h = Create(reputation: 0.0);
        CentoRegisterScreenViewModel page = ArriveAtStart(h);

        page.DigitCommand.Execute(7);
        Assert.True(page.ShowContinue);
        Assert.False(page.CanType);

        page.ContinueCommand.Execute(null);

        Assert.Equal(CentoRacePhase.Racing, h.Flow.Phase);
        Assert.Equal(FinishDoor, h.Host.FinishPinned!.Route.Finish);
        Assert.Equal(2, h.Host.HideCalls);
        // Unity floors a fresh driver's raw 0 at 1 when the ticker starts.
        Assert.Equal(1.0, h.Host.Reputation!.Value, precision: 6);
    }

    [Fact]
    public void Racing_GrowsTheReputationWithTheDistanceDriven()
    {
        Harness h = Create(reputation: 1.25);
        CentoRegisterScreenViewModel page = ArriveAtStart(h);
        page.DigitCommand.Execute(7);
        page.ContinueCommand.Execute(null);

        // Driving: the counter grows one tick per 0.01 km the car covers.
        h.Host.ParkedAtTarget = false;
        Drive(h, seconds: 60, speedKmh: 100f);
        double grown = h.Host.Reputation!.Value;
        Assert.True(grown > 1.25);
        Assert.Null(h.Host.Settled);

        // Arrival at the finish settles the race at the value the companion
        // last showed.
        h.Host.ParkedAtTarget = true;
        h.Flow.Advance(Sample(), PollSeconds);

        Assert.NotNull(h.Host.Settled);
        (CentoMigliaEvent race, double start, double end) = h.Host.Settled!.Value;
        Assert.Equal(FinishDoor, race.Route.Finish);
        Assert.Equal(1.25, start, precision: 6);
        Assert.Equal(grown, end, precision: 9);
        Assert.False(h.Flow.InProgress);
        Assert.Null(h.Host.Reputation);
        Assert.Equal(1, h.Host.ClearGuidanceCalls);
    }

    [Fact]
    public void Racing_StandingStillNeverGrowsTheReputation()
    {
        // The exploit the distance measure closes: a parked car in a live race
        // used to bank the wall-clock ticker; it now banks nothing at all.
        Harness h = Create(reputation: 1.25);
        CentoRegisterScreenViewModel page = ArriveAtStart(h);
        page.DigitCommand.Execute(7);
        page.ContinueCommand.Execute(null);

        h.Host.ParkedAtTarget = false;
        Drive(h, seconds: 60, speedKmh: 0f);

        Assert.Equal(1.25, h.Host.Reputation!.Value, precision: 9);
        Assert.Null(h.Host.Settled);
    }

    [Fact]
    public void Racing_KeepsTheCounterStillWithoutTelemetry()
    {
        Harness h = Create(reputation: 1.25);
        CentoRegisterScreenViewModel page = ArriveAtStart(h);
        page.DigitCommand.Execute(7);
        page.ContinueCommand.Execute(null);

        h.Host.ParkedAtTarget = false;
        for (int i = 0; i < 600; i++)
        {
            h.Flow.Advance(sample: null, PollSeconds);
        }

        Assert.Equal(1.25, h.Host.Reputation!.Value, precision: 6);
        Assert.Null(h.Host.Settled);
    }

    [Fact]
    public void Abandon_LandsOnTheControlPage()
    {
        Harness h = Create();
        CentoRegisterScreenViewModel page = ArriveAtStart(h);

        page.BackCommand.Execute(null);

        Assert.False(h.Flow.InProgress);
        Assert.Same(h.Host.ControlPageObject, h.Host.Screen);
        Assert.Equal(1, h.Host.ClearGuidanceCalls);
        Assert.Equal(2, h.Host.HideCalls);
        Assert.Null(h.Host.Reputation);
        Assert.Null(h.Host.Settled);
    }
}
