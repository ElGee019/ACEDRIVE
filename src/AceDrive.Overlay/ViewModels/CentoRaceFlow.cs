using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One Cento Miglia entry from the built event to the receipt (PORTPLAN item
/// 49, port of Unity MilleMigliaManager + MilleMigliaHandler): drive to the
/// start door, type the registration number the club issued, race to the finish
/// with the club reputation growing on the companion, and settle the receipt.
///
/// The race is solo on purpose — Unity's three NPC competitors are the next
/// slice — so the player's place is first and the receipt pays the first-place
/// purse. The reputation ticker is Unity's factor charged once per 0.01 km
/// driven: the level at the start multiplied by the car/garage factor for every
/// 10 m the car covers, painted live and written back when the finish is
/// reached. A parked car grows nothing, so the entry cannot farm a club's
/// standing by standing still. The host owns the panel, the HUD and the saves;
/// this class owns when the entry moves between its phases.
/// </summary>
public sealed class CentoRaceFlow
{
    private readonly DriverWallet _wallet;
    private readonly Garage _garage;
    private readonly VoiceBeat _beat;
    private readonly ILog _log;

    // WHY late: the host is the coordinator, which cannot exist before this
    // flow does. It attaches once at construction; every command here is only
    // reachable from the UI after that.
    private ICentoRaceHost _host = null!;

    private CentoRacePhase _phase = CentoRacePhase.None;
    private CentoMigliaEvent? _race;
    private CentoRegistrationCheck? _check;
    // The race's reputation: the ticker banks the distance driven, the level
    // the race started from is what the receipt reports beside it (Unity's
    // milleMigliaStartReputation / tempClubReputation pair).
    private CentoReputationTicker? _ticker;
    private double _startReputation;
    private double _reputation;

    public CentoRaceFlow(
        DriverWallet wallet, Garage garage, VoiceBeat beat, ILog log)
    {
        _wallet = wallet;
        _garage = garage;
        _beat = beat;
        _log = log;
    }

    /// <summary>Attaches the shell that owns the panel, the HUD and the settle.</summary>
    public void Attach(ICentoRaceHost host) => _host = host;

    /// <summary>Where the entry is; None when nothing is running.</summary>
    public CentoRacePhase Phase => _phase;

    /// <summary>True from the built event until the receipt takes over.</summary>
    public bool InProgress => _phase != CentoRacePhase.None;

    /// <summary>The live entry, or null when nothing is running.</summary>
    public CentoMigliaEvent? Race => _race;

    /// <summary>
    /// The event page built the entry: start the drive to its first door. The
    /// panel goes away so the companion can guide the player, exactly like the
    /// taxi and delivery legs.
    /// </summary>
    public void Start(CentoMigliaEvent race)
    {
        ArgumentNullException.ThrowIfNull(race);
        if (InProgress)
        {
            _log.LogWarning(
                $"cento miglia start ignored: tier {race.TierNumber} arrived while " +
                $"an entry was {_phase}");
            return;
        }

        _race = race;
        _check = null;
        _phase = CentoRacePhase.ToStart;
        _host.PinStart(race);
        _host.HidePanel();
        _log.LogInfo(
            $"cento miglia entry: tier {race.TierNumber}, registration #{race.Registration}, " +
            $"{AddressFormatter.Format(race.Route.Start)} -> " +
            $"{AddressFormatter.Format(race.Route.Finish)}, {race.Route.DistanceKm:0.000} km");
    }

    /// <summary>
    /// One poll while an entry is live. Parked at the start opens the
    /// registration page; parked at the finish ends the race. The reputation
    /// counter advances on the race leg only, and only by the distance a live
    /// telemetry sample proves the car covered: a parked car, a lost sample
    /// and a paused game all leave it where it is (Unity's ticker paused with
    /// the game, and a still car must not buy the club any standing).
    /// </summary>
    public void Advance(TelemetrySample? sample, double seconds)
    {
        if (_phase == CentoRacePhase.ToStart)
        {
            if (sample is not null && _host.ParkedAtTarget)
            {
                OpenRegistration();
            }
            return;
        }

        if (_phase != CentoRacePhase.Racing || sample is null || _ticker is null)
        {
            return;
        }

        _ticker.Add(sample.SpeedKmh, seconds);
        _reputation = _ticker.Value;
        _host.SetReputation(_reputation);
        if (_host.ParkedAtTarget)
        {
            Finish();
        }
    }

    /// <summary>
    /// CONTINUE on the registration page: the number was accepted, so the race
    /// leg starts. Unity's StartRace reads the same way (the ticker starts, the
    /// phase moves to Driving and the finish becomes the destination).
    /// </summary>
    public void StartRace()
    {
        if (_phase != CentoRacePhase.Verification
            || _race is not { } race
            || _check is not { IsSolved: true })
        {
            return;
        }

        _phase = CentoRacePhase.Racing;
        // Unity floors a first-ever driver's raw 0 at 1 when the ticker starts;
        // the floor is what the race grows from and what the receipt reports.
        _startReputation = CentoMiglia.DisplayReputation(_wallet.ClubReputation);
        _ticker = new CentoReputationTicker(
            _startReputation, CarClass(), _garage.Cars.Count);
        _reputation = _startReputation;
        // Pin first: the counter is painted beside the drive-to sentence the
        // leg owns.
        _host.PinFinish(race);
        _host.SetReputation(_reputation);
        _host.HidePanel();
        // The registration page has done its job: hand the hidden panel back to
        // the control page so a mid-race reopen cannot replay its keypad (or
        // abandon a race the player is already driving).
        _host.ShowPage(_host.ControlPage());
        _log.LogInfo(
            $"cento miglia race started: rep {_startReputation:0.00000}, " +
            $"{race.Route.DistanceKm:0.000} km to the finish");
    }

    /// <summary>
    /// An abandoned entry (BACK on the registration page): no race, no receipt,
    /// no penalty. The companion line goes away and the player lands on the
    /// control page.
    /// </summary>
    public void Abandon()
    {
        if (!InProgress)
        {
            return;
        }

        bool wasRacing = _phase == CentoRacePhase.Racing;
        Reset();
        _host.SetReputation(null);
        _host.ClearGuidance();
        _host.HidePanel();
        _host.ShowPage(_host.ControlPage());
        _log.LogInfo($"cento miglia abandoned: {(wasRacing ? "race" : "entry")} dropped");
    }

    // The finish: stop the ticker at the value the companion last showed and
    // hand the race to the settle, which prices the receipt and banks it.
    private void Finish()
    {
        if (_phase != CentoRacePhase.Racing || _race is not { } race)
        {
            return;
        }

        double start = _startReputation;
        double end = _reputation;
        double distanceKm = _ticker?.DistanceKm ?? 0.0;
        Reset();
        _host.SetReputation(null);
        _host.ClearGuidance();
        _log.LogInfo(
            $"cento miglia finished: rep {start:0.00000} -> {end:0.00000} " +
            $"over {distanceKm:0.000} km");
        _host.SettleRace(race, start, end);
    }

    // Unity's arrival at the start opens the register screen: the player types
    // the number the club issued before the race begins.
    private void OpenRegistration()
    {
        if (_race is not { } race)
        {
            return;
        }

        _phase = CentoRacePhase.Verification;
        _check = new CentoRegistrationCheck(race.Registration);
        _host.RevealPage(new CentoRegisterScreenViewModel(
            _check, StartRace, Abandon, _beat));
        _log.LogInfo(
            $"cento miglia start reached: registration #{race.Registration} " +
            $"({_check.DigitCount} digit(s))");
    }

    private void Reset()
    {
        _phase = CentoRacePhase.None;
        _race = null;
        _check = null;
        _ticker = null;
        _startReputation = 0.0;
        _reputation = 0.0;
    }

    private int CarClass() =>
        _garage.Current is { } car && _garage.SpecOf(car) is { } spec ? spec.Class : 0;
}
