using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The road test (PORTPLAN item 49, port of Unity RoadTestScreen's two legs):
/// the calendar row sends the player to La Strada, ENTER at the door opens the
/// test centre page, and each leg hands the screen to the companion. The 0-60
/// leg runs the five-second start counter and reaction hold and times the run
/// from the green light to 60 km/h; a rolling car holds the counter at five
/// (the companion asks for a halt) and restarts it once the car is still. The
/// 100-0 leg waits for 100 km/h and then integrates the braking distance to a
/// halt. The finish sentence, the car's board and the all-cars record book open
/// on the same page, and CONTINUE hands the test centre back for the other leg;
/// once both have been driven the photo mission puts the panel down and offers
/// the companion's PHOTOSHOOT button. The shot is taken at the car's live
/// position, priced by <see cref="RoadTestSettlement"/> into the receipt page
/// (the issue's crowd, variety, journalist reputation, magazine price and
/// earnings) and the pick is banked as tested there, exactly like Unity's
/// MarkCurrentAsTested.
///
/// Everything here is session state (Unity parity): a restart, a breakdown or
/// leaving the page throws the visit away, and the next calendar tap draws a
/// fresh one. The timer reads real time through <see cref="IClock"/>; the only
/// loop is the coordinator's 10 Hz poll, which calls <see cref="Advance"/>.
/// </summary>
public sealed class RoadTestFlow
{
    // The board's visible slice (Unity's top 10), then the own row when the run
    // fell outside it.
    private const int BoardRows = 10;

    // The accel block's resource key: two placeholders, the target speed and its
    // unit, so the companion can repaint it in the player's own unit.
    private const string TaskKey = "HudRoadTestTask";

    // The same block while the car is still rolling: the counter is the window
    // to stop, and naming the target before the car is halted would invite a
    // run that is not a standing start.
    private const string HaltTaskKey = "HudRoadTestHalt";

    // The brake leg's block: the same target/unit pair, naming the 100 km/h
    // start, so a miles player reads it in miles like the speedometer.
    private const string BrakeTaskKey = "HudRoadTestBrake";

    // The photo mission's block: the drive-anywhere ask the companion prints
    // while the shutter waits. WHY not the page's two-sentence brief: the 200 px
    // strip fits the instruction, and the "keep it interesting" half belongs to
    // the page the player read before driving off.
    private const string PhotoTaskKey = "HudRoadTestPhoto";

    // The companion's one-shot notice beat, the same length the valuation
    // results and the missing-person hints use.
    private const double NoticeSeconds = 4.0;

    private readonly IOnboardingStore _onboarding;
    private readonly GameClock _game;
    private readonly IClock _clock;
    private readonly RoadTestLedger _roads;
    private readonly ITelemetryHud? _hud;
    private readonly IRoadTestStandings _standings;
    private readonly IProfileStore _profiles;
    private readonly RoadTestSettlement _settlement;
    // The roster the all-cars board names its rows from: the board itself only
    // stores car keys, and the overlay is the layer that owns the display name.
    private readonly IReadOnlyDictionary<string, CarSpec> _catalog;
    // The address map the photo mission resolves the shoot's door on: the one
    // geometry the HUD's own address line reads, handed in rather than opened.
    private readonly AddressMap _map;
    private readonly VoiceBeat _beat;
    private readonly Random _rng;
    private readonly ILog _log;
    // The car the game says is being driven: the board row is the week's pick's,
    // so the leg only runs when that car is the one at the door. Read live (the
    // watcher announces a car swap) and null while it has not caught up, which
    // counts as "no mismatch" — the HUD's wrong-car warning rule.
    private readonly Func<CarIdentity?> _liveCar;

    // Weekday number of the road-test event, read off the calendar data the
    // strip already owns: no Unity-day literal here.
    private readonly int _eventDay = EventDay();

    // TEMPORARY (testing): true opens the window gate on any day and hour, so
    // the leg can be driven and replayed. The app passes true; the tests keep
    // the shipping reading.
    private readonly bool _ignoreEventGates;

    // WHY late: the host is the coordinator, which is built after this flow.
    // The attach closes in the coordinator's constructor, before any calendar
    // tap or arrival can reach it.
    private IRoadTestHost _host = null!;

    private RoadTestPhase _phase = RoadTestPhase.Idle;
    private PointOfInterest? _door;
    private CarSpec? _pick;
    private RoadTestScreenViewModel? _screen;
    private RoadTestClock? _run;
    private string _runCarKey = string.Empty;
    // The crossing is interpolated between two polls, so the previous reading
    // has to survive the tick. NaN means "no reading yet" (the green light's
    // first sample only seeds the pair).
    private double _lastSpeedKmh = double.NaN;
    private TimeSpan _lastElapsed;
    // True while the live car is still rolling off the line during the counter.
    // Kept as the last known state, because a lost sample cannot show the car
    // stopped.
    private bool _moving;
    // True once the counter was stopped by a rolling car: the countdown holds
    // instead of running to a green the player could not have used, and the
    // halt restarts it from five.
    private bool _countdownHeld;
    // The brake leg's run: metres banked so far, the previous reading for the
    // trapezoid and the tick's start time (dt comes from the clock, never from
    // a poll count).
    private double _brakeMeters;
    private double _brakePreviousSpeedKmh = double.NaN;
    private DateTime _brakeLastAt;
    private RoadTestEntry _localEntry;
    private IReadOnlyList<RoadTestStanding> _localRows = [];
    // The all-cars record book's row for this run: the same entry painted under
    // its car's name and position 1 (the true place arrives with the board).
    private IReadOnlyList<RoadTestCarStanding> _localCarRows = [];

    // The latest live sample while the photo mission runs: the shutter is
    // pressed between two polls, so the flow keeps the car's own position
    // instead of opening shared memory itself. Null until one lands.
    private TelemetrySample? _shotSample;

    // The board read the finish started; drained on the next poll so the page
    // never waits on the network (Unity paints the local row first too).
    private Task<IReadOnlyList<RoadTestEntry>?>? _boardTask;

    // The all-cars record book read, started beside the car's board and drained
    // the same way: one answer per leg.
    private Task<IReadOnlyList<RoadTestEntry>?>? _carsTask;

    public RoadTestFlow(
        IOnboardingStore onboarding, GameClock game, IClock clock,
        RoadTestLedger roads, ITelemetryHud? hud, IRoadTestStandings standings,
        IProfileStore profiles, RoadTestSettlement settlement,
        IReadOnlyList<CarSpec> catalog, AddressMap map,
        VoiceBeat beat, Func<CarIdentity?> liveCar, Random rng, ILog log,
        bool ignoreEventGates = false)
    {
        ArgumentNullException.ThrowIfNull(onboarding);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(roads);
        ArgumentNullException.ThrowIfNull(standings);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(liveCar);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(log);
        _onboarding = onboarding;
        _game = game;
        _clock = clock;
        _roads = roads;
        _hud = hud;
        _standings = standings;
        _profiles = profiles;
        _settlement = settlement;
        var byKey = new Dictionary<string, CarSpec>(StringComparer.Ordinal);
        foreach (CarSpec car in catalog)
        {
            byKey.TryAdd(car.Key, car);
        }
        _catalog = byKey;
        _map = map;
        _beat = beat;
        _liveCar = liveCar;
        _rng = rng;
        _log = log;
        _ignoreEventGates = ignoreEventGates;
    }

    /// <summary>Attaches the coordinator that owns the panel and its policy.</summary>
    public void Attach(IRoadTestHost host) => _host = host;

    /// <summary>Where the test is; session state, never persisted.</summary>
    public RoadTestPhase Phase => _phase;

    /// <summary>True from the calendar tap until the page is left again.</summary>
    public bool InFlight => _phase != RoadTestPhase.Idle;

    /// <summary>
    /// The calendar row's tap (Unity's job-centre button): pins La Strada and
    /// sends the player off. A page already built is reopened instead of drawing
    /// a second visit, and an entry gate is re-checked here so a caller can never
    /// jump it.
    /// </summary>
    public void Begin()
    {
        DateTime now = _game.GameNow;
        if (!_ignoreEventGates
            && (ControlCalendar.ToDayOfWeek(now) != _eventDay
                || !ControlCalendar.IsEventWindow(now.Hour)))
        {
            _log.LogInfo("road test ignored: outside the event's day and window");
            return;
        }

        if (_phase is RoadTestPhase.Countdown or RoadTestPhase.Timing
            or RoadTestPhase.BrakeApproach or RoadTestPhase.BrakeTiming
            or RoadTestPhase.PhotoMission)
        {
            // A leg that never ends (a car that cannot reach its target) must
            // not trap the session: tapping the row again abandons the run
            // without a result and hands the page back, ready for another try.
            // Checked before the "page is already built" branch, because the
            // page instance does exist while its leg runs. The photo mission
            // rides the same rule: the shoot is not taken, and the test centre
            // comes back with the mission still on offer.
            _run = null;
            _shotSample = null;
            _hud?.SetRoadTest(null);
            _phase = _screen is null ? RoadTestPhase.Idle : RoadTestPhase.AtTestCentre;
            if (_screen is { } page)
            {
                _host.RevealPage(page);
            }
            _log.LogInfo("road test abandoned: the leg was still running");
            return;
        }

        // The visit is over once the receipt is up: its CONTINUE is the one way
        // out, and a second call must not draw a new visit over the page.
        if (_phase == RoadTestPhase.Receipt)
        {
            _log.LogInfo("road test ignored: the receipt is still up");
            return;
        }

        if (_screen is not null)
        {
            _host.RevealPage(_screen);
            return;
        }

        if (_phase is RoadTestPhase.Driving && _door is not null)
        {
            // A second tap re-pins the same door instead of drawing a visit.
            PinDoor(_door);
            _host.HidePanel();
            return;
        }

        if (!EnsureDoor() || _door is not { } door)
        {
            return;
        }

        _phase = RoadTestPhase.Driving;
        PinDoor(door);
        _host.HidePanel();
        _log.LogInfo(
            $"road test begun: driving to {AddressFormatter.Format(door.Address)} " +
            $"for {DescribePick()}");
    }

    /// <summary>
    /// ENTER at La Strada (reached through <see cref="RoadTestPoiHandler"/>;
    /// the companion's button, voice included, raises it): the visit opens the
    /// test centre page. Only a begun test owns the door — a stray visit falls
    /// through to the ordinary no-interior reading.
    /// </summary>
    public object? Enter(PointOfInterest? poi)
    {
        if (_screen is not null)
        {
            return _screen;
        }
        if (_phase != RoadTestPhase.Driving || poi is null || !IsDoor(poi))
        {
            return null;
        }
        if (_roads.Current is not { } car)
        {
            _log.LogWarning("road test entered without a car to test");
            return null;
        }

        _phase = RoadTestPhase.AtTestCentre;
        _pick = car;
        bool drivesThePick = RoadTestRules.DrivesThePick(_liveCar(), car);
        // The arrival is the page now: the companion's drive-to line and its
        // ENTER have done their job, and the test centre is not a place to be
        // guided back into.
        _hud?.SetDestination(null);
        _screen = new RoadTestScreenViewModel(
            car, drivesThePick, StartAccel, StartBrake, PhotoMission,
            Continue, new RelayCommand(Back), _beat);
        _log.LogInfo(
            $"road test at the test centre: {CarTitles.Of(car)}"
            + (drivesThePick ? string.Empty : " (the car at the door is not this week's pick)"));
        return _screen;
    }

    /// <summary>
    /// The page's 0-60 button: the start counter runs on the companion, the
    /// panel goes away so the player can drive, and the leg's clock owns the
    /// green light from here.
    /// </summary>
    public void StartAccel()
    {
        if (_phase != RoadTestPhase.AtTestCentre || _pick is not { } car
            || !PickIsAtTheDoor(car))
        {
            return;
        }

        DateTime now = _clock.UtcNow;
        _run = new RoadTestClock(now, DrawHold());
        _runCarKey = car.Key;
        _lastSpeedKmh = double.NaN;
        _lastElapsed = TimeSpan.Zero;
        // The first sample decides: until it lands the block keeps naming the
        // target, and the next poll holds the counter if the car is rolling.
        _moving = false;
        _countdownHeld = false;
        _phase = RoadTestPhase.Countdown;
        _host.HidePanel();
        PushReadout();
        _log.LogInfo(
            $"road test 0-60 started: {CarTitles.Of(car)}, hold {_run.HoldSeconds:0.00} s");
    }

    /// <summary>
    /// The page's 100-0 button: the panel goes away and the companion asks the
    /// player to bring the car to 100 km/h; the run measures the distance from
    /// there to the halt. Nothing is timed before the car reaches 100.
    /// </summary>
    public void StartBrake()
    {
        if (_phase != RoadTestPhase.AtTestCentre || _pick is not { } car
            || !PickIsAtTheDoor(car))
        {
            return;
        }

        _runCarKey = car.Key;
        _brakeMeters = 0.0;
        _brakePreviousSpeedKmh = double.NaN;
        _phase = RoadTestPhase.BrakeApproach;
        _host.HidePanel();
        PushBrakeReadout();
        _log.LogInfo($"road test 100-0 started: {CarTitles.Of(car)}");
    }

    /// <summary>
    /// One poll while a leg is live: the accel counter/stopwatch or the brake
    /// approach/measurement. Speed arrives as the live sample so the flow never
    /// opens shared memory itself; a lost sample only freezes a measurement,
    /// never the accel clock (its readout keeps counting real time).
    /// </summary>
    public void Advance(TelemetrySample? sample)
    {
        DrainBoard();
        DrainCars();
        SyncPick();
        DateTime now = _clock.UtcNow;
        switch (_phase)
        {
            case RoadTestPhase.Countdown:
            case RoadTestPhase.Timing:
                AdvanceAccel(sample, now);
                return;
            case RoadTestPhase.BrakeApproach:
                AdvanceBrakeApproach(sample, now);
                return;
            case RoadTestPhase.BrakeTiming:
                AdvanceBrakeTiming(sample, now);
                return;
            case RoadTestPhase.PhotoMission:
                // The shoot is taken from the car's live position; nothing is
                // timed while the photographer drives along.
                TrackShot(sample);
                return;
        }
    }

    // The accel leg's poll: the counter (held while rolling, restarted on the
    // halt), then the stopwatch, then the crossing.
    private void AdvanceAccel(TelemetrySample? sample, DateTime now)
    {
        if (_run is not { } run)
        {
            return;
        }

        if (_phase == RoadTestPhase.Countdown)
        {
            TrackStandstill(sample);
            if (_moving)
            {
                // A rolling car cannot make a standing 0-60: the counter stops
                // instead of running to a green the player could not use, and
                // the block asks for the halt. It restarts from five once the
                // car is still, so the wait is never charged to the run.
                HoldCountdown();
                return;
            }

            if (_countdownHeld)
            {
                RestartCountdown(now);
                return;
            }

            if (!run.IsGreen(now))
            {
                PushReadout();
                return;
            }

            // The timer replaced the counter: its first sample only seeds the
            // crossing pair, so a car already above the target cannot bank a
            // 0.000 s run.
            _phase = RoadTestPhase.Timing;
            _lastElapsed = TimeSpan.Zero;
            _lastSpeedKmh = double.NaN;
        }

        TimeSpan elapsed = run.Elapsed(now);
        if (sample is { } live)
        {
            double speed = Math.Abs(live.SpeedKmh);
            if (RoadTestRules.ReachedTarget(speed)
                && double.IsFinite(_lastSpeedKmh)
                && _lastSpeedKmh < RoadTestRules.TargetSpeedKmh)
            {
                Finish(RoadTestTiming.CrossingSeconds(
                    _lastElapsed.TotalSeconds, _lastSpeedKmh, elapsed.TotalSeconds, speed));
                return;
            }

            _lastSpeedKmh = speed;
        }

        _lastElapsed = elapsed;
        PushReadout();
    }

    // The brake leg waits for the car to reach 100 km/h; the tick that sees it
    // starts the measurement with that reading as the pair's first half.
    private void AdvanceBrakeApproach(TelemetrySample? sample, DateTime now)
    {
        if (sample is not { } live
            || !RoadTestRules.ReachedBrakeStart(live.SpeedKmh))
        {
            return;
        }

        _brakeMeters = 0.0;
        _brakePreviousSpeedKmh = Math.Abs(live.SpeedKmh);
        _brakeLastAt = now;
        _phase = RoadTestPhase.BrakeTiming;
        PushBrakeReadout();
        _log.LogInfo(
            $"road test 100-0 measuring from {_brakePreviousSpeedKmh:0.0} km/h");
    }

    // The brake leg's poll: the trapezoid between this sample and the last,
    // down to a standstill, which ends the run.
    private void AdvanceBrakeTiming(TelemetrySample? sample, DateTime now)
    {
        if (sample is not { } live)
        {
            return;
        }

        double speed = Math.Abs(live.SpeedKmh);
        double seconds = (now - _brakeLastAt).TotalSeconds;
        _brakeMeters = RoadTestBrake.AddDistance(
            _brakeMeters, _brakePreviousSpeedKmh, speed, seconds);
        _brakePreviousSpeedKmh = speed;
        _brakeLastAt = now;
        if (RoadTestRules.IsStandstill(speed))
        {
            FinishBrake();
            return;
        }

        PushBrakeReadout();
    }

    /// <summary>
    /// The result page's CONTINUE: the finished leg stays banked and the test
    /// centre comes back, so the other leg (or the photo mission) is next. The
    /// visit itself ends on BACK, never here.
    /// </summary>
    public void Continue()
    {
        if (_phase != RoadTestPhase.Result || _screen is not { } screen)
        {
            return;
        }

        // The board belonged to the result view: drop the read so it cannot
        // paint over the next leg's result.
        ForgetBoardReads();
        _phase = RoadTestPhase.AtTestCentre;
        _hud?.SetRoadTest(null);
        screen.ShowTestCentre();
        _host.RevealPage(screen);
        _log.LogInfo("road test result closed: back at the test centre");
    }

    /// <summary>
    /// The photo mission, offered once both legs ran: the panel goes down and
    /// the companion hands the player the drive-anywhere brief with the
    /// PHOTOSHOOT button. The shot is taken wherever the car is parked, so the
    /// leg never pins a destination — Unity's photo op lets the player pick any
    /// spot for the same reason.
    /// </summary>
    public void PhotoMission()
    {
        if (_phase != RoadTestPhase.AtTestCentre || _screen is not { CanPhoto: true }
            || _pick is not { } car || !PickIsAtTheDoor(car))
        {
            return;
        }

        _phase = RoadTestPhase.PhotoMission;
        _shotSample = null;
        _host.HidePanel();
        _hud?.SetRoadTest(new HudRoadTestReadout(
            PhotoTaskKey, string.Empty, TargetKmh: 0.0, Photoshoot: true));
        _log.LogInfo($"road test photo mission started: {CarTitles.Of(car)}");
    }

    /// <summary>
    /// The companion's PHOTOSHOOT button: the shutter fires at the car's live
    /// position, the receipt is priced and banked, and the visit ends on its
    /// page (Unity's RESULTS). A sample with no address cannot be published, so
    /// the mission stays up and the companion says why.
    /// </summary>
    public void Photoshoot()
    {
        if (_phase != RoadTestPhase.PhotoMission)
        {
            return;
        }

        if (!TryTakeShot(out RoadTestShot shot))
        {
            _hud?.ShowNotice(
                Strings.Get("RoadTestPhotoNoPosition"), TimeSpan.FromSeconds(NoticeSeconds));
            _log.LogInfo("road test photo refused: no address at the live position");
            return;
        }

        RoadTestReceiptScreenViewModel receipt =
            _settlement.Settle(shot, new RelayCommand(FinishReceipt));
        _phase = RoadTestPhase.Receipt;
        _screen = null;
        _shotSample = null;
        _hud?.SetRoadTest(null);
        _host.ShowReceipt(receipt);
    }

    // The shutter reads the car's own position, resolved on the shared address
    // map, so the receipt can name the door the shoot was taken at and measure
    // the variety from the same lap offset. False when no live reading or no
    // address is available: an unpublishable shoot is never priced.
    private bool TryTakeShot(out RoadTestShot shot)
    {
        shot = null!;
        if (_pick is not { } car || _shotSample is not { } sample
            || sample.NormalizedPosition is not { } position)
        {
            return false;
        }

        double length = sample.Track is { LengthMeters: > 0.0 } track
            ? track.LengthMeters
            : _map.TrackLengthMeters;
        if (AddressResolver.Resolve(position, length, _map) is not { } address)
        {
            return false;
        }

        double offset = LapGeometry.OffsetMeters(position, length);
        if (!double.IsFinite(offset))
        {
            return false;
        }

        shot = new RoadTestShot(
            CarTitles.Of(car), address.StreetName, address.HouseNumber, offset, length);
        return true;
    }

    // A lost sample keeps the last position the car was seen at: the shutter is
    // pressed between two polls, and one torn read must not throw the mission's
    // only reading away.
    private void TrackShot(TelemetrySample? sample)
    {
        if (sample is not null)
        {
            _shotSample = sample;
        }
    }

    // The receipt page's CONTINUE: the visit is complete (the pick rotated when
    // the shot settled), so the player lands on the menu — the page a call
    // opens — instead of the jobs page the receipt was opened over.
    private void FinishReceipt()
    {
        if (_phase != RoadTestPhase.Receipt)
        {
            return;
        }

        Reset();
        _host.ShowMenu();
        _log.LogInfo("road test receipt closed");
    }

    // Both legs are the pick's board: a press in another car names the car to
    // enter instead of starting a run that could never land on it.
    private bool PickIsAtTheDoor(CarSpec car)
    {
        if (RoadTestRules.DrivesThePick(_liveCar(), car))
        {
            return true;
        }

        _log.LogInfo(
            $"road test refused: driving {_liveCar()?.KsId ?? "an unknown car"}, " +
            $"this week's test is {car.Key}");
        _screen?.MarkPickRequired();
        _hud?.ShowNotice(PickRequiredLine(car), TimeSpan.FromSeconds(NoticeSeconds));
        return false;
    }

    /// <summary>The page's BACK before a run: no attempt, no result.</summary>
    public void Back()
    {
        // Only a page that offers the way out may take it: while the pick is
        // required the page keeps the visit open, so a stale call must not end
        // the player's one chance to drive the test from this visit.
        if (_phase != RoadTestPhase.AtTestCentre || _screen is not { DrivesThePick: true })
        {
            return;
        }

        Leave("left without a test");
    }

    // The test centre stays open while the player fetches the pick: the wait
    // must survive the car change (BACK would throw the visit away), so every
    // poll re-reads the live car and flips the page the moment the right one
    // arrives — or the pick leaves before a run. A transition only: the page's
    // setters guard against their own repeated call.
    private void SyncPick()
    {
        if (_phase != RoadTestPhase.AtTestCentre || _screen is not { } screen
            || _pick is not { } pick)
        {
            return;
        }

        bool drivesThePick = RoadTestRules.DrivesThePick(_liveCar(), pick);
        if (drivesThePick == screen.DrivesThePick)
        {
            return;
        }

        if (drivesThePick)
        {
            screen.MarkPickArrived();
            _log.LogInfo($"road test ready: {pick.Key} is at the door");
            return;
        }

        screen.MarkPickRequired();
        _log.LogInfo(
            $"road test waiting for {pick.Key}: driving {_liveCar()?.KsId ?? "an unknown car"}");
    }

    // The accel finish: the run is banked to the board (best-guarded), the page
    // opens on the sentence and the row the player just drove, and the board
    // read keeps running behind it on the poll.
    private void Finish(double seconds)
    {
        if (_phase != RoadTestPhase.Timing || _screen is not { } screen)
        {
            return;
        }

        // A car swapped while the clock ran is not the car under test: the run
        // is thrown away instead of landing on the pick's record, and the page
        // goes back to asking for the right car.
        if (_pick is { } pick && !PickWasKept())
        {
            VoidLeg(screen, pick);
            return;
        }

        _phase = RoadTestPhase.Result;
        _run = null;
        _hud?.SetRoadTest(null);
        BuildLocalBoard(seconds);
        screen.ShowResult(
            ResultLine(seconds, screen.CarName), _localRows, _localCarRows,
            RoadTestMetric.AccelerationTime);
        _host.RevealPage(screen);
        SafeTask.Forget(
            _standings.PushAsync(
                RoadTestMetric.AccelerationTime, _runCarKey, seconds, PlayerName()),
            _log, "road test push");
        _boardTask = _standings.TopAsync(
            RoadTestMetric.AccelerationTime, _runCarKey, BoardRows);
        _carsTask = _standings.TopCarsAsync(RoadTestMetric.AccelerationTime);
        _log.LogInfo(
            $"road test 0-60 done: {_runCarKey} in {seconds:0.000} s " +
            $"(board {(_standings.IsOnline ? "live" : "local")})");
    }

    // The brake finish: same board/result order as the accel leg, with metres
    // in place of seconds.
    private void FinishBrake()
    {
        if (_phase != RoadTestPhase.BrakeTiming || _screen is not { } screen)
        {
            return;
        }

        if (_pick is { } pick && !PickWasKept())
        {
            VoidLeg(screen, pick);
            return;
        }

        _phase = RoadTestPhase.Result;
        _hud?.SetRoadTest(null);
        BuildLocalBoard(_brakeMeters);
        screen.ShowResult(
            BrakeResultLine(_brakeMeters, screen.CarName), _localRows, _localCarRows,
            RoadTestMetric.BrakeDistance);
        _host.RevealPage(screen);
        SafeTask.Forget(
            _standings.PushAsync(
                RoadTestMetric.BrakeDistance, _runCarKey, _brakeMeters, PlayerName()),
            _log, "road test brake push");
        _boardTask = _standings.TopAsync(
            RoadTestMetric.BrakeDistance, _runCarKey, BoardRows);
        _carsTask = _standings.TopCarsAsync(RoadTestMetric.BrakeDistance);
        _log.LogInfo(
            $"road test 100-0 done: {_runCarKey} in {_brakeMeters:0.0} m " +
            $"(board {(_standings.IsOnline ? "live" : "local")})");
    }

    private bool PickWasKept() =>
        _pick is null || RoadTestRules.DrivesThePick(_liveCar(), _pick);

    // A run driven in another car cannot land on the pick's row: the visit goes
    // back to the test centre asking for the pick instead of banking it.
    private void VoidLeg(RoadTestScreenViewModel screen, CarSpec pick)
    {
        _phase = RoadTestPhase.AtTestCentre;
        _run = null;
        _hud?.SetRoadTest(null);
        screen.MarkPickRequired();
        _host.RevealPage(screen);
        _hud?.ShowNotice(PickRequiredLine(pick), TimeSpan.FromSeconds(NoticeSeconds));
        _log.LogInfo(
            $"road test voided: {_liveCar()?.KsId ?? "an unknown car"} replaced " +
            $"{pick.Key} mid-run");
    }

    // The local row Unity paints before the server answers: the player's own run
    // at the top, so the page is never empty while the board is out. The
    // all-cars book gets the same treatment, under the car's name, so its
    // section is up from the first frame instead of appearing on the answer.
    private void BuildLocalBoard(double value)
    {
        string playerName = PlayerName();
        _localEntry = new RoadTestEntry(
            _standings.OwnUid ?? string.Empty, playerName, _runCarKey, value,
            _clock.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        _localRows = [new RoadTestStanding(1, playerName, value, true)];
        _localCarRows = [new RoadTestCarStanding(1, CarTitle(_runCarKey), value, true)];
    }

    // Drops the reads the result view started: continuing to the test centre
    // (or leaving) must not let an old answer repaint the next result.
    private void ForgetBoardReads()
    {
        ForgetTask(_boardTask, "road test board read");
        ForgetTask(_carsTask, "road test all-cars read");
        _boardTask = null;
        _carsTask = null;
    }

    private void ForgetTask(Task? task, string operation)
    {
        if (task is { IsCompleted: false } pending)
        {
            SafeTask.Forget(pending, _log, operation);
        }
    }

    // The board read the finish started. Null (offline, unreachable, or a failed
    // request) keeps the local row; a live answer is merged with the local entry
    // so a push that landed after the query still shows the run.
    private void DrainBoard()
    {
        if (_boardTask is not { IsCompleted: true } task)
        {
            return;
        }

        _boardTask = null;
        if (task.IsFaulted)
        {
            _log.LogWarning(
                $"road test board read failed: {task.Exception?.GetBaseException().Message}");
            _screen?.ShowBoard(_localRows);
            return;
        }
        if (task.Status != TaskStatus.RanToCompletion || task.Result is not { } entries)
        {
            _screen?.ShowBoard(_localRows);
            return;
        }

        _screen?.ShowBoard(RoadTestBoard.Standings(
            RoadTestBoard.KeepLocalRow(entries, _localEntry),
            _standings.OwnUid,
            BoardRows));
    }

    // The all-cars read the finish started: the same contract as the car's
    // board, with this run merged in so a push that has not landed yet still
    // shows. An unreachable book keeps the local row rather than blanking it.
    private void DrainCars()
    {
        if (_carsTask is not { IsCompleted: true } task)
        {
            return;
        }

        _carsTask = null;
        if (task.IsFaulted)
        {
            _log.LogWarning(
                "road test all-cars read failed: "
                + $"{task.Exception?.GetBaseException().Message}");
            return;
        }
        if (task.Status != TaskStatus.RanToCompletion || task.Result is not { } entries)
        {
            return;
        }

        _screen?.ShowAllCarsBoard(CarStandings(
            RoadTestBoard.KeepLocalCar(entries, _localEntry)));
    }

    // The board's rows named for the page: the car title is resolved here
    // because the catalogue lookup is the overlay's, while the page only paints.
    private List<RoadTestCarStanding> CarStandings(List<RoadTestEntry> entries)
    {
        var rows = new List<RoadTestCarStanding>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            RoadTestEntry entry = entries[i];
            rows.Add(new RoadTestCarStanding(
                i + 1,
                CarTitle(entry.CarKey),
                entry.Value,
                string.Equals(entry.CarKey, _runCarKey, StringComparison.Ordinal)));
        }
        return rows;
    }

    private string CarTitle(string carKey) =>
        CarTitles.Of(carKey, _catalog.GetValueOrDefault(carKey));

    // The car's live state for the counter: a lost sample keeps the last
    // reading, because the flow must never claim a stop it cannot see.
    private void TrackStandstill(TelemetrySample? sample)
    {
        if (sample is { } live)
        {
            _moving = !RoadTestRules.IsStandstill(live.SpeedKmh);
        }
    }

    // The counter is parked at five while the car rolls: it does not run down
    // toward a green the player could not use, and the halt restarts it from
    // that digit. Pushed on entry only — a held counter repaints nothing, so a
    // long wait costs no per-tick record.
    private void HoldCountdown()
    {
        if (_countdownHeld)
        {
            return;
        }

        _countdownHeld = true;
        _hud?.SetRoadTest(new HudRoadTestReadout(
            HaltTaskKey,
            RoadTestRules.CountdownSteps.ToString(CultureInfo.CurrentCulture),
            TargetKmh: 0.0));
        _log.LogInfo("road test counter held: the car is moving");
    }

    // The halt landed: a fresh clock so the counter starts over at five with a
    // newly drawn hold, which is the only fair standing start — resuming a
    // half-spent count would charge the player for the wait.
    private void RestartCountdown(DateTime now)
    {
        _run = new RoadTestClock(now, DrawHold());
        _countdownHeld = false;
        PushReadout();
        _log.LogInfo("road test counter restarted: the car is halted");
    }

    private void PushReadout()
    {
        if (_hud is null || _run is not { } run)
        {
            return;
        }

        DateTime now = _clock.UtcNow;
        int countdown = run.Countdown(now);
        // WHY the per-tick format: a three-decimal stopwatch moves on every
        // poll by definition, so this is the one readout that cannot be
        // guarded by a change check. It costs one short string and one small
        // record per tick for the seconds the leg runs, never for the session
        // (the same bounded exception the Cento reputation counter takes).
        _hud.SetRoadTest(new HudRoadTestReadout(
            TaskKey,
            countdown > 0
                ? countdown.ToString(CultureInfo.CurrentCulture)
                : Strings.Format("RoadTestTimer", run.Elapsed(now).TotalSeconds),
            RoadTestRules.TargetSpeedKmh));
    }

    // The brake leg's block: the task names the 100 km/h start in the player's
    // unit and the clock line is the distance banked so far, so the player sees
    // the run build while the car is braking.
    private void PushBrakeReadout() =>
        _hud?.SetRoadTest(new HudRoadTestReadout(
            BrakeTaskKey,
            Strings.Format("RoadTestTimerMetres", _brakeMeters),
            RoadTestRules.BrakeStartSpeedKmh));

    private string BrakeResultLine(double meters, string carName)
    {
        MileageUnit unit = _hud?.Units ?? MileageUnit.Kilometres;
        return Strings.Format(
            "RoadTestBrakeResult",
            RoadTestRules.DisplaySpeed(RoadTestRules.BrakeStartSpeedKmh, unit),
            Strings.Get(unit == MileageUnit.Miles ? "RoadTestUnitMph" : "RoadTestUnitKmh"),
            meters,
            carName);
    }

    private string ResultLine(double seconds, string carName)
    {
        // WHY the companion's unit and not a stored copy: the HUD is the one
        // owner that follows the settings page, so the sentence names the same
        // unit the speedometer the player watched was reading.
        MileageUnit unit = _hud?.Units ?? MileageUnit.Kilometres;
        return Strings.Format(
            "RoadTestAccelResult",
            RoadTestRules.DisplayTarget(unit),
            Strings.Get(unit == MileageUnit.Miles ? "RoadTestUnitMph" : "RoadTestUnitKmh"),
            seconds,
            carName);
    }

    // "This week's test is for the X. Enter that car." — the page's line and
    // the companion's notice share it, so the two never disagree.
    private static string PickRequiredLine(CarSpec pick) =>
        Strings.Format("RoadTestPickRequired", CarTitles.Of(pick));

    // The way out before a run: no receipt, so the jobs page (the page the
    // player navigates from) comes back, unlike a completed visit's menu.
    private void Leave(string why)
    {
        Reset();
        _host.ShowControl();
        _log.LogInfo($"road test {why}");
    }

    // The visit's session state, dropped by both ways out: nothing here survives
    // the visit (Unity parity), so the next calendar tap draws a fresh one.
    private void Reset()
    {
        _phase = RoadTestPhase.Idle;
        _screen = null;
        _pick = null;
        _door = null;
        _run = null;
        _shotSample = null;
        ForgetBoardReads();
        _localRows = [];
        _localCarRows = [];
        _hud?.SetRoadTest(null);
    }

    private void PinDoor(PointOfInterest door)
    {
        _hud?.SetDestination(
            door,
            new HudDestinationCopy(
                "HudRoadTestDrive", "HudRoadTestArrived", "HudEnter"));
        _hud?.ShowTaskText();
    }

    private bool TryDoor(out PointOfInterest door)
    {
        door = null!;
        // The door is the tour's, banked with the onboarding progress: La Strada
        // has no interior of its own, so this flow is its one way in.
        if (_onboarding.Load().LaStradaAddress is not { } address)
        {
            _log.LogWarning("road test not begun: the save has no La Strada door");
            return false;
        }

        door = new PointOfInterest(
            StoryLaStrada.Definition.Id, StoryLaStrada.Definition.Name,
            address, PoiTypes.Magazine);
        return true;
    }

    private bool EnsureDoor()
    {
        if (_door is not null)
        {
            return true;
        }
        if (!TryDoor(out PointOfInterest door))
        {
            return false;
        }
        _door = door;
        return true;
    }

    private static bool IsDoor(PointOfInterest poi) =>
        string.Equals(poi.Id, StoryLaStrada.Definition.Id, StringComparison.Ordinal);

    // Unity's reaction hold, drawn on the flow's own seeded stream so a test
    // pins the whole countdown.
    private double DrawHold() =>
        RoadTestRules.HoldMinSeconds
        + _rng.NextDouble() * (RoadTestRules.HoldMaxSeconds - RoadTestRules.HoldMinSeconds);

    private string PlayerName() =>
        _profiles.Load()?.DisplayName is { Length: > 0 } name
            ? name
            : Strings.Get("RoadTestYou");

    private string DescribePick() =>
        _roads.Current is { } car ? CarTitles.Of(car) : "no car";

    private static int EventDay()
    {
        foreach (CalendarDay day in ControlCalendar.Week)
        {
            if (day.EventId == ControlCalendar.RoadTest)
            {
                return day.DayOfWeek;
            }
        }
        return -1;
    }
}
