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
    private readonly string _file;
    private readonly ILog _log;

    public JsonTaxiRulesStore(ILog log)
        : this(DataPaths.GameData("taxi_rules.json"), log)
    { }

    public JsonTaxiRulesStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public TaxiRules Load()
    {
        JsonReadStatus status = JsonFile.TryRead(_file, out TaxiRules? rules, out string? error);
        if (status == JsonReadStatus.Missing)
        {
            _log.LogWarning($"taxi_rules.json missing at {_file}; using built-in defaults");
            return TaxiRules.Default;
        }

        if (rules is null)
        {
            _log.LogWarning(
                $"taxi_rules.json {JsonFile.Describe(error)} at {_file}; using built-in defaults");
            return TaxiRules.Default;
        }

        // A file written before the speed challenge existed has no block:
        // that is a sane default, not an invalid file.
        if (rules.SpeedChallenge is null)
        {
            rules = rules with { SpeedChallenge = SpeedChallengeRules.Default };
        }

        if (!IsValid(rules))
        {
            _log.LogWarning($"taxi_rules.json invalid at {_file}; using built-in defaults");
            return TaxiRules.Default;
        }

        _log.LogInfo($"taxi rules loaded: arrival={rules.ArrivalRadiusMeters:0} m, " +
            $"meter cap={rules.MaxMeterSpeedKmh:0} km/h, " +
            $"distance step={rules.DisplayDistanceStepMeters:0} m, " +
            $"notice={rules.NoticeSeconds:0.#} s, " +
            $"request delay={rules.RequestDelaySeconds:0.#} s, " +
            $"speed challenge avg={rules.SpeedChallenge.MinAverageKmh:0}-" +
            $"{rules.SpeedChallenge.MaxAverageKmh:0} km/h");
        return rules;
    }

    private static bool IsValid(TaxiRules rules) =>
        double.IsFinite(rules.ArrivalRadiusMeters) && rules.ArrivalRadiusMeters > 0.0
        && double.IsFinite(rules.MaxMeterSpeedKmh) && rules.MaxMeterSpeedKmh > 0.0
        && double.IsFinite(rules.DisplayDistanceStepMeters) && rules.DisplayDistanceStepMeters > 0.0
        && double.IsFinite(rules.NoticeSeconds) && rules.NoticeSeconds > 0.0
        && double.IsFinite(rules.RequestDelaySeconds) && rules.RequestDelaySeconds >= 0.0
        && rules.SpeedChallenge is { IsValid: true };
}
