using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The taxi job from proposal to receipt: the customer request, the quiz, the
/// speed challenge with its patrols, the dirty-car refusal, and the pickup and
/// drop-off moments (Unity's JobSelectionScreen + TaxiJobLifecycleHandler).
///
/// Owns the job state and the pages the job puts up. The shell's half — the
/// panel, the saves, the breakdown and the settle — arrives through
/// <see cref="ITaxiJobHost"/>. The shift itself (start, timeout, end) stays with
/// the shell: it outlives one job.
/// </summary>
public sealed class TaxiJobFlow
{
    private readonly TaxiJobProposer _proposals;
    private readonly AddressMap _addressMap;
    private readonly ICarBrandSource _carBrands;
    private readonly IReadOnlyList<CarSpec> _cars;
    private readonly ICustomerRequestRoller? _requests;
    private readonly CustomerRequestTable _requestTable;
    private readonly PolicePatrolPlanner? _police;
    private readonly TaxiRules _taxiRules;
    private readonly FareTable _fares;
    private readonly Garage _garage;
    private readonly DriverWallet _wallet;
    private readonly IFriendshipStore _friends;
    private readonly FleetService _fleet;
    private readonly GameClock _game;
    private readonly IClock _clock;
    private readonly VoiceBeat _beat;
    private readonly TaxiDriveMeter _meter;
    private readonly GameStateSaver _saver;
    private readonly IRivalsService _rivals;
    private readonly ITelemetryHud? _hud;
    private readonly ILog _log;

    // WHY late: the host is the coordinator, which cannot exist before this
    // flow does. It attaches once at construction; every command here is only
    // reachable from the UI after that.
    private ITaxiJobHost _host = null!;

    private TaxiJobState _job = new();
    private TaxiJobState? _dirtyRefusedJob;
    private CustomerRequest? _pendingRequest;
    private DateTime _requestDueAt;
    private CustomerRequestScreenViewModel? _requestScreen;
    private TaxiJobProposalViewModel? _proposal;
    private ParameterizedCommand<TaxiJobState>? _acceptJobAction;
    private bool _speedChallengeArmed;
    private double? _trackLengthMeters;

    public TaxiJobFlow(
        TaxiJobProposer proposals, AddressMap addressMap, ICarBrandSource carBrands,
        IReadOnlyList<CarSpec> cars, ICustomerRequestRoller? requests,
        CustomerRequestTable requestTable, PolicePatrolPlanner? police,
        TaxiRules taxiRules, FareTable fares, Garage garage, DriverWallet wallet,
        IFriendshipStore friends, FleetService fleet, GameClock game, IClock clock,
        VoiceBeat beat, TaxiDriveMeter meter, GameStateSaver saver, IRivalsService rivals,
        ITelemetryHud? hud, ILog log)
    {
        _proposals = proposals;
        _addressMap = addressMap;
        _carBrands = carBrands;
        _cars = cars;
        _requests = requests;
        _requestTable = requestTable;
        _police = police;
        _taxiRules = taxiRules;
        _fares = fares;
        _garage = garage;
        _wallet = wallet;
        _friends = friends;
        _fleet = fleet;
        _game = game;
        _clock = clock;
        _beat = beat;
        _meter = meter;
        _saver = saver;
        _rivals = rivals;
        _hud = hud;
        _log = log;
        PickupCommand = new RelayCommand(ConfirmPickup, () => _job.Phase == JobPhase.ToPickup);
        DropoffCommand = new RelayCommand(ConfirmDropoff, () => _job.Phase == JobPhase.EnRoute);
    }

    /// <summary>Attaches the shell that owns the panel, the saves and the settle.</summary>
    public void Attach(ITaxiJobHost host) => _host = host;

    /// <summary>The job as it stands; the readouts read it (moves with them in S5).</summary>
    public TaxiJobState Job => _job;

    /// <summary>True from ACCEPT to the receipt: a call must not open the panel.</summary>
    public bool InProgress => _job.Phase is JobPhase.ToPickup or JobPhase.EnRoute;

    /// <summary>True while the paid leg runs: the meter and the patrols are live.</summary>
    public bool LegActive => _job.Phase == JobPhase.EnRoute;

    public ICommand PickupCommand { get; }

    public ICommand DropoffCommand { get; }

    /// <summary>Closed-lap length from the telemetry map, for the patrol geometry.</summary>
    public void SetTrackLength(double meters) => _trackLengthMeters = meters;

    /// <summary>
    /// The proposal page, built once and reused like the delivery board: the
    /// committed radius survives leaving the page, and every visit opens on a
    /// fresh proposal (Unity shows a newly generated job per visit).
    /// </summary>
    public object OpenProposal()
    {
        _proposal ??= new TaxiJobProposalViewModel(
            _proposals, _addressMap, () => _host.HomeAddress(),
            () => _fleet.PassengerCapacity(), _log, _beat,
            new RelayCommand(() => _host.ShowPage(_host.ControlPage())),
            _acceptJobAction ??= new ParameterizedCommand<TaxiJobState>(TaxiAccepted));
        _proposal.Open();
        return _proposal;
    }

    /// <summary>
    /// The request countdown and its read-in delay. Runs every tick whether or
    /// not EVO is attached, so a closed game cannot freeze an open ask.
    /// </summary>
    public void Tick(DateTime now)
    {
        _requestScreen?.Tick(now);

        // A rolled ask replaces the drop-off task text only once the read-in
        // delay has passed. One null check per tick; the clock comparison is
        // the only thing that appears while an ask is waiting.
        if (_pendingRequest is { } pending && now >= _requestDueAt)
        {
            _pendingRequest = null;
            ShowCustomerRequest(pending);
        }
    }

    /// <summary>
    /// The paid leg: bank the distance the meter measured, reprice the fare and
    /// run the speed-challenge patrols. The odometer already banked the wear per
    /// poll, so nothing here touches the car's condition.
    /// </summary>
    public void AdvanceLeg(TelemetrySample sample, double dt)
    {
        double before = _meter.DistanceKm;
        _meter.Add(sample.SpeedKmh, dt);
        if (_job.Phase == JobPhase.EnRoute && _meter.DistanceKm != before)
        {
            _job.DistanceKm = _meter.DistanceKm;
            _job.Fare = TaxiFare.ComputeBase(_job.DistanceKm, _fares);
            _host.RefreshJob();
        }

        if (_job.Phase == JobPhase.EnRoute && _job.SpeedChallengeAgreed)
        {
            CheckPolice(sample);
        }
    }

    // ACCEPT only commits the proposal: the customer is not aboard yet, so
    // the dirt gate and the breakdown roll wait at the pickup door (Unity's
    // TaxiJobLifecycleHandler). The overlay steps aside and the HUD pins the
    // pickup, counting the drive down to the arrival radius.
    private void TaxiAccepted(TaxiJobState? job)
    {
        if (job is null) return;
        _job = job;
        _job.Phase = JobPhase.ToPickup;
        _job.DistanceKm = 0.0;
        _job.Fare = 0.0;
        _job.Patrols = [];
        _job.FinedPatrolIds.Clear();
        _job.WarnedPatrolIds.Clear();
        _job.SpeedChallengeDeadline = null;
        _speedChallengeArmed = false;
        _pendingRequest = null;
        _meter.Reset();
        ShowPickupGuidance();
        _host.HidePanel();
        _log.LogInfo($"job accepted: {job.Customer.Full} {job.PickupName} -> {job.DropoffName}");
        _host.RefreshJob();
    }

    // Unity's pickup moment: the car is parked at the customer's door. The
    // dirt gate and the trip-start breakdown roll live here, not at ACCEPT,
    // because the customer only sees the car on arrival.
    private void ConfirmPickup()
    {
        if (_job.Phase != JobPhase.ToPickup) return;
        if (RefuseDirtyPickup(_job))
        {
            AbandonJob();
            if (_proposal is { } proposal)
            {
                _host.RevealPage(proposal);
            }
            return;
        }

        // Breakdown roll at the trip start (PORTPLAN 39, Unity
        // BeginTripSession): a triggered trip is cut before it pays — the job
        // never becomes active, the customer loses 1 familiarity, the
        // mechanic's tow fee is charged, and the breakdown page replaces the
        // proposal.
        if (_host.RollTripStart(BreakdownKind.Taxi) is { } breakdown)
        {
            _friends.AddFamiliarity(_job.Customer.BaseName, -1);
            _log.LogInfo($"taxi job cut by breakdown: {_job.Customer.Full} " +
                $"{_job.PickupName} -> {_job.DropoffName}");
            AbandonJob();
            _host.ShowBreakdown(breakdown);
            return;
        }

        _meter.Reset();
        _job.Phase = JobPhase.EnRoute;
        _job.DistanceKm = 0.0;
        _job.Fare = TaxiFare.ComputeBase(0.0, _fares);
        _log.LogInfo($"job pickup: {_job.PickupName}");
        // The drop-off task text goes up first: the player must read where to
        // drive before an ask may replace the HUD line. Unity rolls the special
        // request at the pickup, while the customer is still parked at the
        // door; the ask then rides the companion after the read-in delay
        // (ShowCustomerRequest on the poll). The panel is hidden for this leg.
        ShowDropoffGuidance();
        CustomerRequest? request = _requests?.Roll(
            _job.Customer, CurrentCarSpec(), _game.GameNow.Year,
            _garage.Current?.AskedCategories ?? []);
        if (request is not null)
        {
            _pendingRequest = request;
            _requestDueAt = _clock.UtcNow
                + TimeSpan.FromSeconds(_taxiRules.RequestDelaySeconds);
        }
        _host.RefreshJob();
    }

    // The customer asks once the drop-off task text has had its read-in time.
    // The job only carries the request from this moment, and the forced speed
    // challenge starts its clock here: the player is not timed while reading
    // where to drive. A forced challenge is not an ask: its page is a short
    // read-in with no answer countdown or button, so it closes itself and the
    // task text takes back over with the same clock still running.
    private void ShowCustomerRequest(CustomerRequest request)
    {
        _job.Request = request;
        if (request.IsSpeedChallenge)
        {
            BeginSpeedChallenge();
        }

        _requestScreen = new CustomerRequestScreenViewModel(
            request, _job.Customer.Full, _requestTable.TimerSeconds,
            _clock, _beat, OnRequestFinished,
            request.IsSpeedChallenge ? _job.SpeedChallengeDeadline : null,
            request.IsSpeedChallenge
                ? TimeSpan.FromSeconds(_taxiRules.NoticeSeconds)
                : null);
        _host.ShowPage(_requestScreen);
        _hud?.ShowRequest(_requestScreen);
        _log.LogInfo($"customer request: {request.Kind} for {_job.Customer.Full}");
        _host.RefreshJob();
    }

    // Unity's dirty-car refusal at the pickup door: the customer refuses the
    // ride, familiarity drops by one (the store banks itself), and the
    // proposal page shows the reason. A repeated ACCEPT on the same proposal
    // shows it again but never charges the customer twice
    // (Unity's _tooDirtyArrived).
    private bool RefuseDirtyPickup(TaxiJobState job)
    {
        if (_garage.Current is not { } car || !car.Filth.RefusesPickup)
        {
            _dirtyRefusedJob = null;
            return false;
        }

        string customer = job.Customer.BaseName;
        if (!ReferenceEquals(job, _dirtyRefusedJob))
        {
            _dirtyRefusedJob = job;
            _friends.AddFamiliarity(customer, -1);
            // Unity HandleTooDirtyArrival: a ride cancelled for a filthy car
            // costs one concentration point, once per cancelled pickup.
            _wallet.AddConcentration(-1.0);
            _saver.SaveDriver(_rivals.LastResult);
            _log.LogWarning($"[Filth] pickup refused at {car.Filth.Value}/{car.Filth.Max}: " +
                $"{customer} friendship -1, concentration {_wallet.Rating.Concentration:0.00}");
        }
        _proposal?.ShowRefusal(Strings.Format("TaxiTooDirty", customer));
        return true;
    }

    // The customer is at the drop-off door: settle, show the receipt and drop
    // the HUD guidance.
    private void ConfirmDropoff()
    {
        if (_job.Phase != JobPhase.EnRoute) return;
        _job.Phase = JobPhase.Complete;
        _pendingRequest = null;
        _hud?.SetJobGuidance(null);
        _host.SettleRide(_job);
        _job.Patrols = [];
        _job.FinedPatrolIds.Clear();
        _job.WarnedPatrolIds.Clear();
    }

    // A refused or broken-down pickup leaves no active job: the HUD line goes
    // away and the command guards close until the next ACCEPT.
    private void AbandonJob()
    {
        _job.Phase = JobPhase.None;
        _job.Patrols = [];
        _job.FinedPatrolIds.Clear();
        _job.WarnedPatrolIds.Clear();
        _job.SpeedChallengeDeadline = null;
        _speedChallengeArmed = false;
        _pendingRequest = null;
        _requestScreen = null;
        _hud?.ClearRequest();
        _hud?.SetJobGuidance(null);
        _host.RefreshJob();
    }

    // The request's one exit lands here exactly once: BACK on an answered ask,
    // its timeout, or the forced challenge's read-in closing itself. Copy the
    // outcome onto the job, bank the quiz history for this car, clear the
    // companion overlay and resume the drop-off guidance.
    private void OnRequestFinished(CustomerRequestOutcome outcome)
    {
        _requestScreen = null;
        _hud?.ClearRequest();
        _job.TipEligible = outcome.TipEligible;
        _job.QuizAnsweredCorrectly = outcome.QuizAnsweredCorrectly;
        _job.SpeedChallengeAgreed = outcome.SpeedChallengeAgreed;
        ApplyQuizRating(outcome);

        if (outcome.SpeedChallengeAgreed)
        {
            // The forced challenge already armed when its page opened; this
            // keeps the single exit idempotent without drawing a second plan.
            ArmSpeedChallenge();
        }

        if (_job.Request?.Quiz is { } quiz && _garage.Current is { } car)
        {
            // Unity marks the category on answer AND on timeout (HandleAnswer
            // covers both), then saves; the at-count reset lives in Core.
            car.MarkAskedCategory(quiz.Category.Key, _requestTable.QuestionCategories.Count);
            _saver.SaveGarage();
        }

        ShowDropoffGuidance();
        _host.HidePanel();
        _log.LogInfo($"customer request done: {_job.Request?.Kind} " +
            $"answered={outcome.Answered} tip={outcome.TipEligible} " +
            $"quiz={outcome.QuizAnsweredCorrectly} speed={outcome.SpeedChallengeAgreed}");
        _host.RefreshJob();
    }

    // The quiz asks about the car in the garage, like Unity's playerCar: the
    // catalogue row behind the current key, never a copy taken at purchase.
    private CarSpec? CurrentCarSpec() =>
        _garage.Current is { } car ? _garage.SpecOf(car) : null;

    // Unity CustomerRequestScreen.HandleAnswer: a correct quiz answer lifts
    // off-the-line by 1, a wrong answer or a timeout drops it by 1 (both reach
    // here with QuizAnsweredCorrectly false). The skill belongs to the driver,
    // not the car, so an unowned free-roam car pays the same.
    private void ApplyQuizRating(CustomerRequestOutcome outcome)
    {
        if (_job.Request?.Quiz is null)
        {
            return;
        }

        bool correct = outcome.Answered && outcome.QuizAnsweredCorrectly;
        _wallet.AddOffTheLine(correct ? 1.0 : -1.0);
        _saver.SaveDriver(_rivals.LastResult);
        _log.LogInfo($"[Rating] quiz {(correct ? "correct" : "missed")}: " +
            $"off-the-line {_wallet.Rating.OffTheLine:0.00}");
    }

    private double TrackLengthMeters() =>
        _trackLengthMeters ?? _addressMap.TrackLengthMeters;

    // The forced speed challenge starts with its page, after the drop-off task
    // text: the deadline and patrols are live even while the player is reading
    // the ask. The route is the same closed-lap distance the guidance and
    // patrol planner already use, so no routing service is needed.
    private void BeginSpeedChallenge()
    {
        double routeMeters = SpeedChallengeRouteMeters();
        CarSpec? timedCar = _fleet.XpCar()
            ?? _carBrands.CurrentIdentity?.FindIn(_cars)
            ?? CurrentCarSpec();
        TimeSpan? allowance = SpeedChallengeTimer.Estimate(
            routeMeters, timedCar?.TopSpeedKmh ?? 0, _taxiRules.SpeedChallenge);
        _job.SpeedChallengeDeadline = allowance is { } span
            ? _clock.UtcNow + span
            : null;
        _job.TipEligible = true;
        _job.SpeedChallengeAgreed = true;
        ArmSpeedChallenge();
        _log.LogInfo(allowance is { } estimated
            ? $"speed challenge deadline: {estimated.TotalSeconds:0}s " +
              $"({routeMeters / 1000.0:0.00} km, " +
              $"{timedCar?.TopSpeedKmh ?? 0} km/h top)"
            : "speed challenge has no deadline: route unavailable");
    }

    private double SpeedChallengeRouteMeters() =>
        _job.PickupAddress is { } pickup && _job.DropoffAddress is { } dropoff
            ? AddressBook.DistanceMeters(
                _addressMap, pickup, dropoff, TrackLengthMeters())
            : double.NaN;

    private void ArmSpeedChallenge()
    {
        if (_speedChallengeArmed || _police is not { } planner)
        {
            return;
        }

        // Unity spawns at the pickup for every job; this loop only needs the
        // plan when the gamble is on, and once per accepted job.
        _job.Patrols = planner.Plan(
            _job.PickupAddress, _job.DropoffAddress,
            TrackLengthMeters(), planner.Rules.PatrolsPerJob);
        _job.FinedPatrolIds.Clear();
        _job.WarnedPatrolIds.Clear();
        _speedChallengeArmed = true;
        _log.LogInfo(
            $"speed challenge armed: {_job.Patrols.Count} patrol(s), " +
            $"limit {planner.Rules.SpeedLimitKmh} km/h");
    }

    // Speed-challenge police check, once per live poll while the passenger is
    // aboard. A warning shows once per patrol on approach; a fine lands once
    // per patrol, clears the ordinary tip and adds the receipt deduction.
    private void CheckPolice(TelemetrySample sample)
    {
        if (_police is not { } planner)
        {
            return;
        }

        double length = TrackLengthMeters();
        double offset = sample.NormalizedPosition is { } position
            ? LapGeometry.OffsetMeters(position, length)
            : double.NaN;
        if (double.IsFinite(offset))
        {
            foreach (PolicePatrol patrol in _job.Patrols)
            {
                if (_job.FinedPatrolIds.Contains(patrol.Id)
                    || _job.WarnedPatrolIds.Contains(patrol.Id))
                {
                    continue;
                }

                double distance = LapGeometry.DistanceMeters(
                    offset, patrol.LapOffsetMeters, length);
                if (distance <= planner.Rules.WarningRadiusMeters)
                {
                    _job.WarnedPatrolIds.Add(patrol.Id);
                    _hud?.ShowNotice(
                        Strings.Format("HudPoliceWarning", patrol.SpeedLimitKmh),
                        TimeSpan.FromSeconds(_taxiRules.NoticeSeconds));
                }
            }
        }

        PolicePatrol? caught = SpeedChallengeFines.Check(
            _job.Patrols, sample.NormalizedPosition ?? double.NaN,
            length, sample.SpeedKmh, _job.FinedPatrolIds,
            _job.SpeedChallengeAgreed);
        if (caught is not { } fine)
        {
            return;
        }

        _job.FinedPatrolIds.Add(fine.Id);
        _job.SpeedChallengeFines++;
        // Unity's OR tip rule: the fine kills the ordinary ask tip but a
        // correct quiz tip rides its own flag through the settle.
        _job.TipEligible = false;
        _hud?.ShowNotice(
            Strings.Format("HudPoliceFine", _fares.FineAmount),
            TimeSpan.FromSeconds(_taxiRules.NoticeSeconds));
        _log.LogWarning(
            $"speed challenge fine: €{_fares.FineAmount:0} " +
            $"(patrol {fine.Id}, {sample.SpeedKmh:0} km/h in {fine.SpeedLimitKmh} zone)");
    }

    // The HUD line names the customer, not the leg: the templates carry the
    // door's street, number and town from the structured address, so passing
    // an address descriptor here would print it twice (Unity names the
    // passenger on the PICK UP / DROP OFF prompt too). The pickup names the
    // whole party (companion suffix included); the drop-off names the
    // customer alone, like Unity's base-name identity.
    private void ShowPickupGuidance() =>
        _hud?.SetJobGuidance(Guidance(
            _job.Customer.Full, _job.PickupAddress,
            "HudJobPickupDrive", "HudJobPickupArrived", "HudJobPickupAction"));

    private void ShowDropoffGuidance() =>
        _hud?.SetJobGuidance(Guidance(
            _job.Customer.BaseName, _job.DropoffAddress,
            "HudJobDropoffDrive", "HudJobDropoffArrived", "HudJobDropoffAction",
            _job.SpeedChallengeDeadline));

    // A proposal generated on a map that cannot supply two doors has no
    // structured address; the HUD then has nothing to route to and stays
    // unpinned rather than showing a wrong street.
    private HudJobGuidance? Guidance(
        string name, AddressLocation? target, string driveKey,
        string arrivedKey, string actionKey, DateTime? speedDeadline = null) =>
        target is { } door
            ? new HudJobGuidance(
                name, door, driveKey, arrivedKey, actionKey,
                _taxiRules.ArrivalRadiusMeters, speedDeadline)
            : null;
}
