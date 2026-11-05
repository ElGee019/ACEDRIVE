namespace AceDrive.Core;

/// <summary>
/// Banks the distance one finished job drove, without a constant tracker: the
/// job's start samples the driven car's odometer and the settle compares it
/// again, so the 10 Hz odometer feed stays the only place distance is
/// integrated. Taxi, delivery and car haul own one job at a time, so one
/// pending start reading is enough.
///
/// The wallet owns the record and the RNG the five-kilometre awards roll on;
/// this collaborator only turns an odometer pair into a bank. A car switch or
/// an engine reset mid-job invalidates the pair, and a torn reading never moves
/// the record.
/// </summary>
public sealed class JobMileageMeter
{
    private readonly Func<OwnedCar?> _currentCar;
    private readonly DriverWallet _wallet;
    private readonly Random _rng;
    private double? _startKm;
    private string? _startCarKey;

    public JobMileageMeter(Func<OwnedCar?> currentCar, DriverWallet wallet, Random rng)
    {
        ArgumentNullException.ThrowIfNull(currentCar);
        ArgumentNullException.ThrowIfNull(wallet);
        ArgumentNullException.ThrowIfNull(rng);
        _currentCar = currentCar;
        _wallet = wallet;
        _rng = rng;
    }

    /// <summary>One job starts: remember where its odometer stood.</summary>
    public void StartJob()
    {
        OwnedCar? car = _currentCar();
        _startKm = car?.Odometer;
        _startCarKey = car?.Key;
    }

    /// <summary>
    /// The job settled: bank the odometer delta and clear the pending start.
    /// Answers the banked kilometres, 0 when no owned car ran the job.
    /// </summary>
    public double FinishJob()
    {
        double km = BankSinceStart();
        Clear();
        return km;
    }

    /// <summary>The job ended without a settle (a crash or a cancellation).</summary>
    public void CancelJob() => Clear();

    /// <summary>
    /// A billed crash: bank the clean distance driven since the start before
    /// the record resets, then count the crash. A job that survives the hit
    /// restarts its measurement at the current odometer, so the post-crash
    /// remainder cannot be credited to the old run.
    /// </summary>
    public void RegisterAccident()
    {
        BankSinceStart();
        OwnedCar? car = _currentCar();
        if (_startKm is not null)
        {
            if (car is not null && string.Equals(car.Key, _startCarKey, StringComparison.Ordinal))
            {
                _startKm = car.Odometer;
            }
            else
            {
                Clear();
            }
        }

        _wallet.RegisterAccident();
    }

    // The shared read: a missing car, a switched car or a reset odometer answers
    // 0 instead of moving the record backwards.
    private double BankSinceStart()
    {
        if (_startKm is not { } start || _currentCar() is not { } car
            || !string.Equals(car.Key, _startCarKey, StringComparison.Ordinal))
        {
            return 0.0;
        }

        double km = car.Odometer - start;
        if (!double.IsFinite(km) || km <= 0.0)
        {
            return 0.0;
        }

        // WHY round: the odometer accumulates 10 Hz floating-point steps, so a
        // "5.0 km" job can land at 4.999999999 and miss the five-kilometre
        // award. A metre is far finer than the record reads, so rounding there
        // keeps the threshold honest without moving the displayed figures.
        km = Math.Round(km, 3, MidpointRounding.AwayFromZero);
        if (km <= 0.0)
        {
            return 0.0;
        }

        _wallet.BankJobDistance(km, _rng);
        return km;
    }

    private void Clear()
    {
        _startKm = null;
        _startCarKey = null;
    }
}
