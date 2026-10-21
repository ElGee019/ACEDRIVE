using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the driving-slice rules from Data/taxi_rules.json next to the exe.
/// Missing or invalid files degrade to the built-in defaults with a warning —
/// the overlay must never fail to start over a data file.
/// </summary>
public sealed class JsonTaxiRulesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonTaxiRulesStore(ILog log)
        : this(Path.Combine(AppContext.BaseDirectory, "Data", "taxi_rules.json"), log)
    { }

    public JsonTaxiRulesStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public TaxiRules Load()
    {
        try
        {
            if (!File.Exists(_file))
            {
                _log.LogWarning($"taxi_rules.json missing at {_file}; using built-in defaults");
                return TaxiRules.Default;
            }

            TaxiRules? rules = JsonSerializer.Deserialize<TaxiRules>(
                File.ReadAllText(_file), JsonOptions);
            if (rules is null || !IsValid(rules))
            {
                _log.LogWarning($"taxi_rules.json invalid at {_file}; using built-in defaults");
                return TaxiRules.Default;
            }

            _log.LogInfo($"taxi rules loaded: arrival={rules.ArrivalRadiusMeters:0} m, " +
                $"meter cap={rules.MaxMeterSpeedKmh:0} km/h, " +
                $"distance step={rules.DisplayDistanceStepMeters:0} m, " +
                $"notice={rules.NoticeSeconds:0.#} s");
            return rules;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Read/deserialize boundary only: a corrupt or locked data file
            // must not take the overlay down (AGENTS.md: Try* at every
            // external boundary). Anything else stays a crash.
            _log.LogWarning($"taxi_rules.json unreadable at {_file} ({ex.GetType().Name}); using built-in defaults");
            return TaxiRules.Default;
        }
    }

    private static bool IsValid(TaxiRules rules) =>
        double.IsFinite(rules.ArrivalRadiusMeters) && rules.ArrivalRadiusMeters > 0.0
        && double.IsFinite(rules.MaxMeterSpeedKmh) && rules.MaxMeterSpeedKmh > 0.0
        && double.IsFinite(rules.DisplayDistanceStepMeters) && rules.DisplayDistanceStepMeters > 0.0
        && double.IsFinite(rules.NoticeSeconds) && rules.NoticeSeconds > 0.0;
}
