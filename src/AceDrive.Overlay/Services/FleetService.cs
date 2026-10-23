using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// The fleet's condition: how the car driven now wears (odometer, engine
/// inspections, filth), what the driven car can carry, and the nightly
/// insurance charge. One home for every path that mutates an
/// <see cref="OwnedCar"/>, so the poll, the settles and the garage doors all
/// go through the same rules instead of duplicating them.
/// </summary>
public sealed class FleetService
{
    /// <summary>WHY a clamp: EVO reports a per-tick speed, and a teleport/hiccup
    /// can report an absurd value for one frame; capping the sample keeps a torn
    /// or glitched read from banking a bogus jump into the wear ledger.</summary>
    private const double MaxTelemetrySpeedKmh = 500.0;

    private readonly Garage _garage;
    private readonly IReadOnlyList<CarSpec> _cars;
    private readonly GameClock _game;
    private readonly DriverWallet _wallet;
    private readonly InsuranceRules _insurance;
    private readonly ILog _log;
    private readonly IFleetHost _host;

    public FleetService(
        Garage garage, IReadOnlyList<CarSpec> cars, GameClock game,
        DriverWallet wallet, InsuranceRules insurance, ILog log,
        IFleetHost host)
    {
        _garage = garage;
        _cars = cars;
        _game = game;
        _wallet = wallet;
        _insurance = insurance;
        _log = log;
        _host = host;
    }

    /// <summary>
    /// Passenger seats of the driven car (Unity: passengerCapacity scales the
    /// distance charge). Unknown car (EVO quiet, pool gap), ambiguous variant
    /// (multi-variant folder, no preset announced) or 0-seat race entry rides
    /// solo; a 0-seat entry still takes one customer. The receipt builder
    /// clamps too.
    /// </summary>
    public int PassengerCapacity() =>
        _host.CarIdentity?.FindIn(_cars) is { } car ? Math.Max(1, car.Passengers) : 1;

    /// <summary>
    /// The car that earns class/rarity XP and the global km increment: the
    /// live car when it matches an owned garage row, the garage's current car
    /// when the watcher has not announced one yet, and null when the player is
    /// driving a car they do not own. Null means no car class/rarity and no
    /// Firebase car write — a borrowed free-roam car must not distort the
    /// ownership economy or the global rarity board.
    /// </summary>
    public CarSpec? XpCar()
    {
        if (_host.CarIdentity is { } live)
        {
            return FindOwned(live) is { } owned ? _garage.SpecOf(owned) : null;
        }

        return _garage.Current is { } current ? _garage.SpecOf(current) : null;
    }

    /// <summary>
    /// True when the watcher has announced a live car that matches no owned
    /// garage row. An unknown identity is not unowned (the watcher may not
    /// have caught up yet), so the garage's current car stands in, the same
    /// rule as the wrong-car flag. A job driven in an unowned car still pays
    /// its fare, but contributes no career progress: no XP/standings, no
    /// country km, no zone job and no global car km.
    /// </summary>
    public bool IsDrivingUnownedCar() =>
        _host.CarIdentity is { } live && FindOwned(live) is null;

    /// <summary>
    /// Display brand for the receipt's sponsorship lines: the watcher's spoken
    /// brand ("mazda") canonicalized to the onboarding spelling ("Mazda") so
    /// the receipt reads like the brand page. Unknown brands ride through
    /// untouched; the mileage store matches case-insensitively.
    /// </summary>
    public string SponsorBrand()
    {
        string spoken = _host.CarBrand.Trim();
        foreach (string known in _host.Brands)
        {
            if (string.Equals(known, spoken, StringComparison.OrdinalIgnoreCase))
            {
                return known;
            }
        }
        return spoken;
    }

    /// <summary>
    /// The career follows the garage's current car; a live car that is not that
    /// row is a free-roam detour, and the HUD says so. An unknown identity (the
    /// log watcher has not caught up yet) is not a mismatch: warning on a guess
    /// would flag every fresh session.
    /// </summary>
    public bool IsDrivingWrongCar() =>
        _host.CarIdentity is { } identity
        && _garage.Current is { } current
        && !identity.Matches(current.Key);

    /// <summary>
    /// The odometer feed (PORTPLAN items 31/32, Unity DrivingManager's speed
    /// integration): bank this tick's distance into the car driven now, then run
    /// the engine inspections. The same dt guard as the fare meter keeps a
    /// stalled, torn or teleported poll out of the ledger; the speed clamp keeps
    /// a one-frame glitch out of it too.
    /// </summary>
    public void IntegrateOdometer(TelemetrySample sample, double dt)
    {
        if (_garage.Current is not { } car)
        {
            return;
        }
        double speed = double.IsFinite(sample.SpeedKmh)
            ? Math.Clamp(sample.SpeedKmh, 0.0, MaxTelemetrySpeedKmh)
            : 0.0;
        if (speed <= 0.0)
        {
            return;
        }
        // Only a sane tick banks distance; a stalled or lagged poll skips the
        // add (the guard the fare meter uses) but still lets the inspections
        // run, so a long frame never delays a due engine check.
        if (dt is > 0 and < 1)
        {
            car.Drive(speed / 3600.0 * dt);
        }
        // Unity runs the inspections only while driving and only past the intro
        // (firstSceneComplete); the whole engine system is dormant before it.
        if (_host.OnboardingDone)
        {
            RunEngineChecks(car);
        }
    }

    /// <summary>
    /// One completed job's dirt (PORTPLAN 33, Unity IncrementFilth): +1 on the
    /// car driven now, clamped at the rules' cap. A settled job on an already
    /// filthy car is no transition, so nothing logs. The caller banks the garage.
    /// </summary>
    public void AddDirt()
    {
        if (_garage.Current is not { } car)
        {
            return;
        }
        int before = car.Filth.Value;
        car.Filth.Add(1);
        if (car.Filth.Value != before)
        {
            _log.LogInfo($"[Filth] {car.Key} {car.Filth.Value}/{car.Filth.Max} after the job");
        }
    }

    /// <summary>
    /// The nightly per-car insurance premium (PORTPLAN 34, Unity
    /// GameClockManager.UpdateInsurance): once per game date, inside the rules'
    /// charge hour, for every insured car with a price. The charge takes what
    /// the cash covers (a premium is a commitment, not a purchase), and the
    /// stamp rides the driver save so a restart inside the same hour never
    /// double-charges. True when a charge was banked, so the caller can run the
    /// shared driver save. The discount reads the wallet's LIVE overall rating
    /// (PORTPLAN 06): a skill gained earlier today lowers tonight's premium.
    /// </summary>
    public bool ChargeDailyInsurance()
    {
        DateTime now = _game.GameNow;
        if (now.Hour != _insurance.ChargeHour)
        {
            return false;
        }
        string date = now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        if (string.Equals(_wallet.LastInsuranceChargeDate, date, StringComparison.Ordinal))
        {
            return false;
        }
        _wallet.MarkInsuranceCharged(date);
        double total = 0.0;
        int insured = 0;
        double driverRating = _wallet.Rating.Overall;
        foreach (OwnedCar car in _garage.Cars)
        {
            if (!car.HasInsurance || _garage.SpecOf(car) is not { Price: > 0.0 } spec)
            {
                continue;
            }
            double premium = InsurancePricing.DiscountedDailyCost(spec.Price, driverRating, _insurance);
            total += _wallet.ChargeUnavoidable(premium);
            insured++;
        }
        if (insured == 0)
        {
            return false;
        }

        _log.LogInfo($"[Insurance] daily premium {total:0.00} CR for {insured} car(s) " +
            $"(cash {_wallet.Cash:0.00})");
        return true;
    }

    // Unity DrivingManager.RunEngineInspections: the weekly check is
    // informational (the schedule rolls forward), the annual one can strand the
    // car. Both fire at most once per period because the schedule rolls on the
    // due tick (EngineHealth, Unity parity).
    private void RunEngineChecks(OwnedCar car)
    {
        DateTime now = _game.GameNow;
        car.Engine.EnsureInitialized(now);
        if (car.Engine.IsWeeklyDue(now))
        {
            car.Engine.CompleteWeekly(now);
            _log.LogInfo($"[Engine] {car.Key} weekly inspection " +
                $"({car.Engine.BrokenPercent(EngineCcOf(car), car.Odometer) * 100.0:0.0000}% broken)");
        }
        if (car.Engine.IsAnnualDue(now))
        {
            bool failed = car.Engine.RunAnnual(EngineCcOf(car), car.Odometer, now);
            if (failed)
            {
                _log.LogWarning($"[Engine] {car.Key} failed its annual inspection at " +
                    $"{car.Odometer:0} km — stranded until the mechanic");
            }
            else
            {
                _log.LogInfo($"[Engine] {car.Key} passed its annual inspection at {car.Odometer:0} km");
            }
        }
    }

    // The garage row behind the announced live car; null when the player
    // drives a car they do not own (or a variant no owned row matches).
    private OwnedCar? FindOwned(CarIdentity live)
    {
        foreach (OwnedCar owned in _garage.Cars)
        {
            if (live.Matches(owned.Key))
            {
                return owned;
            }
        }
        return null;
    }

    // The catalogue cc behind the driven car; an unknown key reads 0 and rides
    // the smallest red-km band, never throws.
    private int EngineCcOf(OwnedCar car) => _garage.SpecOf(car)?.EngineCc ?? 0;
}
