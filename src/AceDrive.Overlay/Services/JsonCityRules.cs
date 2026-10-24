using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the town's economy rules from Data/city_rules.json next to the exe.
/// Missing or invalid content degrades to <see cref="CityRules.Default"/>
/// (Unity's shipped config) with a warning — the overlay must never fail to
/// start over a data file, and a town that cannot tick is worse than defaults.
/// </summary>
public sealed class JsonCityRules
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonCityRules(ILog log)
        : this(DataPaths.GameData("city_rules.json"), log)
    { }

    public JsonCityRules(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public CityRules Load()
    {
        if (!JsonTable.TryLoad(
                _log, _file, "city_rules.json", "using built-in defaults",
                IsValid, out CityRules? rules))
        {
            return CityRules.Default;
        }

        _log.LogInfo($"city rules loaded: {rules.CitizenTickMinutes} min citizens, " +
            $"{rules.DepletionTickMinutes} min demand");
        return rules;
    }

    // Intervals must be real minutes or the tick never pays out; the band
    // edges must descend and stay inside a percent, or every store lands in
    // the same band forever; and the demand ladder needs positive rungs.
    private static bool IsValid(CityRules rules) =>
        rules.CitizenTickMinutes > 0 && rules.DepletionTickMinutes > 0
        && rules.MinCitizens >= 0 && rules.DefaultCitizens >= rules.MinCitizens
        && rules.HighPercent >= rules.MediumPercent && rules.HighPercent <= 100
        && rules.MediumPercent > rules.LowPercent
        && rules.LowPercent > rules.VeryLowPercent && rules.VeryLowPercent >= 0
        && rules.DepletionFirstCitizens > 0 && rules.DepletionStepCitizens > 0;
}
