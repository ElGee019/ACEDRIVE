using System.IO;
using System.Text.Json;
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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonCityRules(ILog log)
        : this(Path.Combine(AppContext.BaseDirectory, "Data", "city_rules.json"), log)
    { }

    public JsonCityRules(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public CityRules Load()
    {
        try
        {
            if (!File.Exists(_file))
            {
                _log.LogWarning($"city_rules.json missing at {_file}; using built-in defaults");
                return CityRules.Default;
            }
            CityRules? rules = JsonSerializer.Deserialize<CityRules>(
                File.ReadAllText(_file), JsonOptions);
            if (rules is null || !IsValid(rules))
            {
                _log.LogWarning($"city_rules.json invalid at {_file}; using built-in defaults");
                return CityRules.Default;
            }
            _log.LogInfo($"city rules loaded: {rules.CitizenTickMinutes} min citizens, " +
                $"{rules.DepletionTickMinutes} min demand");
            return rules;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Read/deserialize boundary only: a corrupt or locked data file
            // must not take the overlay down (AGENTS.md: Try* at every
            // external boundary). Anything else stays a crash.
            _log.LogWarning($"city_rules.json unreadable at {_file} ({ex.GetType().Name}); using built-in defaults");
            return CityRules.Default;
        }
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
