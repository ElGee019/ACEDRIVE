// One car the driver owns (PORTPLAN item 30, port of Unity Models/PlayerData.cs
// OwnedCar, cut down to what this port uses). Pure data: display text, specs and
// money come from the catalogue row the key resolves, never from a copy taken at
// purchase time — a game update that re-prices a car re-prices the garage too.
// Mutable like the other live state holders (DriverWallet, OperatingZone).
namespace AceDrive.Core;

/// <summary>
/// An owned car: the catalogue key it was bought as (id, or id#preset for a
/// showroom variant) plus the per-car state gameplay accumulates. PurchaseWeek is
/// the calendar week the car joined the garage — Unity stores the ceremony year
/// there; with no year system the week is the only stamp that exists.
/// </summary>
public sealed class OwnedCar
{
    /// <summary>
    /// The ranking a car starts on before the stretch leaderboards can measure
    /// it: the top percentile, so the placeholder never penalises a car. Once
    /// the boards exist the computed ranking (<see cref="ValuationRules.LeaderboardRanking"/>)
    /// replaces it — this constant is only the starting value.
    /// </summary>
    public const double DefaultLeaderboardRanking = 1.0;

    // WHY the rules ride in: the filth cap (filth_rules.json), the part
    // intervals/prices and the engine bands (maintenance_rules.json) are data,
    // so each reading needs the rule record its garage was built with. The
    // defaults keep a hand-made car (tests, tools) working — FareTable.Default
    // precedent.
    public OwnedCar(
        string key, int purchaseWeek,
        FilthRules? filthRules = null, MaintenanceRules? maintenanceRules = null,
        int seed = 0, OwnedCarState? saved = null)
    {
        Key = key;
        PurchaseWeek = purchaseWeek;
        double odometer = saved?.Odometer ?? 0.0;
        Odometer = double.IsFinite(odometer) && odometer > 0.0 ? odometer : 0.0;
        HasInsurance = saved?.HasInsurance ?? false;
        Filth = new CarFilth(filthRules ?? FilthRules.Default, saved?.Filth ?? 0);
        MaintenanceRules maintenance = maintenanceRules ?? MaintenanceRules.Default;
        Condition = new CarCondition(maintenance, saved?.Parts, Odometer);
        Engine = new EngineHealth(maintenance, saved?.Engine, Odometer, seed);
        _askedCategories = CleanAsked(saved?.AskedCategories);
        JobsCompleted = Math.Max(0, saved?.JobsCompleted ?? 0);
        LeaderboardRanking = CleanRanking(saved?.LeaderboardRanking ?? DefaultLeaderboardRanking);
    }

    /// <summary>Catalogue key of the bought car (<see cref="CarSpec.Key"/>).</summary>
    public string Key { get; }

    /// <summary>Calendar week the car joined the garage (the display week, PORTPLAN 50).</summary>
    public int PurchaseWeek { get; }

    /// <summary>Kilometres driven in this car. Feeds the wear/engine math (PORTPLAN 31/32).</summary>
    public double Odometer { get; private set; }

    /// <summary>
    /// Settled taxi/delivery jobs completed with this car (Unity
    /// CarJobsService.GetJobs). The weekly valuation score adds one point per
    /// job, so the count rides the garage save.
    /// </summary>
    public int JobsCompleted { get; private set; }

    /// <summary>
    /// The car's stretch leaderboard ranking, 0..1 where 1 is the top
    /// percentile of its model's board. The weekly valuation scores it as
    /// ranking² × 150 (<see cref="ValuationRules.LeaderboardPoints"/>); until
    /// the stretch leaderboards land it stays on
    /// <see cref="DefaultLeaderboardRanking"/>, and the future board source
    /// writes the computed value through <see cref="SetLeaderboardRanking"/>.
    /// Per owned car, so two copies of one model each carry their own value.
    /// </summary>
    public double LeaderboardRanking { get; private set; }

    /// <summary>Whether the daily premium covers this car (Unity OwnedCar.hasInsurance).</summary>
    public bool HasInsurance { get; private set; }

    /// <summary>The car's dirt (PORTPLAN item 33, Unity PlayerData.carFilth):
    /// +1 per completed job, cleared by a paid wash.</summary>
    public CarFilth Filth { get; }

    /// <summary>The per-part wear deck (PORTPLAN item 31, Unity maintenanceParts).</summary>
    public CarCondition Condition { get; }

    /// <summary>The engine health (PORTPLAN item 32, Unity EngineHealthService).</summary>
    public EngineHealth Engine { get; }

    // The quiz history: category keys already asked on this car. Reset once
    // every category has been asked (CarStatQuiz.MarkAsked owns that rule).
    private IReadOnlyList<string> _askedCategories;

    /// <summary>Car-stat categories already asked on this car (oldest first).</summary>
    public IReadOnlyList<string> AskedCategories => _askedCategories;

    /// <summary>
    /// Records one asked quiz category and applies the at-count reset. The
    /// caller saves the garage once after this call.
    /// </summary>
    public void MarkAskedCategory(string key, int categoryCount) =>
        _askedCategories = CarStatQuiz.MarkAsked(_askedCategories, key, categoryCount);

    /// <summary>Clears the quiz history (a new car, or a hand-edited reset).</summary>
    public void ResetAskedCategories() => _askedCategories = [];

    /// <summary>Banks kilometres driven (Unity OwnedCar.odometer). Never negative,
    /// never NaN — a torn telemetry read must not poison the ledger.</summary>
    public void Drive(double distanceKm)
    {
        if (distanceKm <= 0.0 || !double.IsFinite(distanceKm))
        {
            return;
        }
        Odometer += distanceKm;
    }

    /// <summary>Unity OwnedCar.hasInsurance flip (broker / insurance screen, item 34).</summary>
    public void SetInsurance(bool insured) => HasInsurance = insured;

    /// <summary>
    /// One settled job with this car (Unity CarJobsService's counter). The
    /// caller saving the garage right after the settle banks it.
    /// </summary>
    public void RegisterJob() => JobsCompleted++;

    /// <summary>
    /// Records the ranking the stretch leaderboards computed for this car's
    /// model (0..1). A non-finite value keeps the placeholder instead of
    /// poisoning the valuation score with NaN.
    /// </summary>
    public void SetLeaderboardRanking(double ranking) =>
        LeaderboardRanking = CleanRanking(ranking);

    /// <summary>Unity ReplaceEngine's odometer reset ("a new engine resets car
    /// km to 0"): the caller resets the part baselines too (<see cref="CarCondition.ResetAll"/>).</summary>
    public void ResetOdometer() => Odometer = 0.0;

    public OwnedCarState Snapshot() => new(
        Key, PurchaseWeek, Odometer, HasInsurance, Filth.Value,
        Condition.Snapshot(), Engine.Snapshot(), _askedCategories, JobsCompleted,
        LeaderboardRanking);

    // A hand-edited or NaN ranking would make the valuation score unusable, so
    // the reading clamps into the 0..1 band the curve expects; a non-finite one
    // falls back to the placeholder the car starts on.
    private static double CleanRanking(double ranking) =>
        double.IsFinite(ranking)
            ? Math.Clamp(ranking, 0.0, 1.0)
            : DefaultLeaderboardRanking;

    private static List<string> CleanAsked(IReadOnlyList<string>? saved)
    {
        if (saved is not { Count: > 0 })
        {
            return [];
        }

        List<string> cleaned = [];
        foreach (string value in saved)
        {
            string key = (value ?? "").Trim();
            if (key.Length > 0 && !cleaned.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                cleaned.Add(key);
            }
        }
        return cleaned;
    }
}
