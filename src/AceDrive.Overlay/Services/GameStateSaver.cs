using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// One place that banks the live session to disk. Every state owner
/// (wallet + trunk, streak, zone, garage, city, hunger, road test, weekly
/// market valuation, missing-person stamp) contributes its slice of a save, so
/// a save from any path can never clobber another owner with stale numbers —
/// the caller never assembles a snapshot itself.
/// All stores swallow their own I/O, so none of these methods throw.
/// </summary>
public sealed class GameStateSaver
{
    private readonly IClock _clock;
    private readonly IGameClockStore _clocks;
    private readonly IDriverStore _driver;
    private readonly IGarageStore _garageStore;
    private readonly ICityStore _cityStore;
    private readonly IHungerStateStore _hungerStore;
    private readonly IRoadTestStore _roadTestStore;
    private readonly IShiftStore _shiftStore;
    private readonly GameClock _game;
    private readonly DriverWallet _wallet;
    private readonly DailyStreak _streak;
    private readonly OperatingZone _zone;
    private readonly JobLedger _jobs;
    private readonly DestinationPins _pins;
    private readonly VisitLedger _visits;
    private readonly ValuationLedger _valuation;
    private readonly WhereIsWillyLedger _willy;
    private readonly Trunk _trunk;
    private readonly Garage _garage;
    private readonly CityEconomy _city;
    private readonly Hunger _hunger;
    private readonly RoadTestLedger _roads;
    private readonly TaxiShift _shift;
    private readonly ShiftRecord _shifts;

    public GameStateSaver(
        IClock clock, GameClock game,
        IGameClockStore clockStore, IDriverStore driverStore,
        IGarageStore garageStore, ICityStore cityStore,
        IHungerStateStore hungerStore, IRoadTestStore roadTestStore,
        IShiftStore shiftStore,
        DriverWallet wallet, DailyStreak streak, OperatingZone zone, Trunk trunk,
        Garage garage, CityEconomy city, Hunger hunger, RoadTestLedger roadTests,
        TaxiShift shift, ShiftRecord shifts, JobLedger jobs,
        DestinationPins pins, VisitLedger visits, ValuationLedger valuation,
        WhereIsWillyLedger willy)
    {
        _clock = clock;
        _game = game;
        _clocks = clockStore;
        _driver = driverStore;
        _garageStore = garageStore;
        _cityStore = cityStore;
        _hungerStore = hungerStore;
        _roadTestStore = roadTestStore;
        _shiftStore = shiftStore;
        _wallet = wallet;
        _streak = streak;
        _zone = zone;
        _jobs = jobs;
        _pins = pins;
        _visits = visits;
        _valuation = valuation;
        _willy = willy;
        _trunk = trunk;
        _garage = garage;
        _city = city;
        _hunger = hunger;
        _roads = roadTests;
        _shift = shift;
        _shifts = shifts;
        // WHY not DateTime.MinValue: the first throttled write must land a full
        // interval into the session, so a startup read that failed for a
        // transient reason (locked file) can never be immediately clobbered.
        LastClockSave = _clock.UtcNow;
        LastGarageSave = _clock.UtcNow;
    }

    /// <summary>Wall-clock stamp of the last driver-moment bank, so the poll's
    /// throttle can ask whether an interval has passed.</summary>
    public DateTime LastClockSave { get; private set; }

    /// <summary>Wall-clock stamp of the last garage bank (odometer feed).</summary>
    public DateTime LastGarageSave { get; private set; }

    /// <summary>
    /// Persists the game moment + display week and restamps the poll throttle,
    /// so every bank path (day flip, checkpoint, interval, window close) keeps
    /// the interval honest.
    /// </summary>
    public void SaveClock(int displayWeek)
    {
        LastClockSave = _clock.UtcNow;
        _clocks.Save(new GameClockState(_game.GameNow, displayWeek));
    }

    /// <summary>The garage's own file: a purchase, a switch, the odometer feed,
    /// the starter seed. Restamps so the throttle stays honest.</summary>
    public void SaveGarage()
    {
        LastGarageSave = _clock.UtcNow;
        _garageStore.Save(_garage.Snapshot());
    }

    /// <summary>
    /// Banks the town for the next session (PORTPLAN 25): the city size plus
    /// the shelf state that drives it. Exit-only, like Unity's storeStates,
    /// which are never written mid-session — the drift is world flavour, so a
    /// killed process costs an hour of growth and no player progress.
    /// </summary>
    public void SaveCity() => _cityStore.Save(_city.Snapshot());

    /// <summary>
    /// Banks the hunger reading for the next session (Unity PlayerData.hunger
    /// survives restarts): the meter plus the one-shot event state resume
    /// instead of re-seeding at 0. The hourly tick banks with the change, so a
    /// killed process costs at most the partial hour since the last write.
    /// </summary>
    public void SaveHunger() => _hungerStore.Save(_hunger.Snapshot());

    /// <summary>
    /// Banks the road-test pick. Two moments need it: a session opening (a
    /// rebuild/Stop-Debugging kills the process before any test is driven, and
    /// the next session must offer that very same car) and a completion (the
    /// only event allowed to move the car).
    /// </summary>
    public void SaveRoadTest() => _roadTestStore.Save(_roads.Snapshot());

    /// <summary>
    /// Banks the taxi shift with its wall-clock stamps. Three moments change
    /// it — START, a completed dropoff and the idle timeout — so the 30-minute
    /// rule keeps running across a closed overlay instead of restarting at
    /// the next launch.
    /// </summary>
    public void SaveShift() => _shiftStore.Save(_shift.Snapshot());

    /// <summary>
    /// The driver file (PORTPLAN 51 interim): every scalar owner contributes,
    /// plus the standings the caller just refreshed. A null snapshot keeps the
    /// last known board (OfflineRivals seeds it from the driver save at boot).
    /// </summary>
    public void SaveDriver(RivalsResult? standings) =>
        _driver.Save(_wallet.Snapshot() with
        {
            RivalsRank = standings?.CurrentRank ?? 0,
            RivalsTotal = standings?.TotalCompetitors ?? 0,
            DaysInARow = _streak.DaysInARow,
            LastLoginDate = _streak.LastLoginDate,
            LastDailyBonusDate = _streak.LastDailyBonusDate,
            ExpansionJobs = _zone.CountedJobs,
            ExpansionCount = _zone.ExpansionCount,
            StreetIds = _zone.StreetIds,
            ZoneKm = _zone.Km,
            JobsCompleted = _jobs.Completed,
            TaxiJobsCompleted = _jobs.TaxiCompleted,
            DeliveryJobsCompleted = _jobs.DeliveryCompleted,
            ManuallySetDestinations = _pins.Count,
            VisitedCarWash = _visits.CarWash,
            VisitedCarDealer = _visits.CarDealer,
            VisitedInsurance = _visits.InsuranceBroker,
            Bank = _wallet.BankBalance,
            HasEnteredBank = _wallet.HasEnteredBank,
            Trunk = _trunk.Snapshot(),
            BestShiftEarnings = _shifts.BestEarnings,
            BestShiftFirstEarnedTicks = _shifts.BestFirstEarnedTicks,
            ShiftRank = _shifts.Rank,
            ShiftTotal = _shifts.Total,
            ShiftsCompleted = _shifts.ShiftsCompleted,
            LongestShiftJobs = _shifts.LongestJobs,
            // The weekly market valuation (PORTPLAN 49): the venue draw, the
            // in-flight leg, the week's entry and the settled uid set all
            // resume from here, so a restart reopens the same showcase week.
            // The seen set is copied: the ledger keeps appending to its own
            // list, and the snapshot must not move under the write.
            ValuationVenueStreetId = _valuation.VenueStreetId,
            ValuationVenueHouseNumber = _valuation.VenueHouseNumber,
            ValuationDriving = _valuation.Driving,
            ValuationEventsCompleted = _valuation.EventsCompleted,
            ValuationEntryWeek = _valuation.EntryWeek,
            ValuationEntryCarKey = _valuation.EntryCarKey,
            ValuationOwnScore = _valuation.OwnScore,
            ValuationSeenUids = [.. _valuation.SeenUids],
            LastValuationResetWeek = _valuation.LastResetWeek,
            // The weekly missing-person run keeps only its completion stamp
            // (ToWhereIsWilly.md §2.9): the search itself is session state, so
            // a restart inside the run reopens the row with nothing paid.
            LastWillyDate = _willy.LastCompletedDate,
        });
}
