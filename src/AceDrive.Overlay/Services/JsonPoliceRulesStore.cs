using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the speed-challenge police rules from Data/police.json next to the
/// exe. Missing or invalid files degrade to the built-in defaults with a
/// warning — the overlay must never fail to start over a data file.
/// </summary>
public sealed class JsonPoliceRulesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonPoliceRulesStore(ILog log)
        : this(Path.Combine(AppContext.BaseDirectory, "Data", "police.json"), log)
    { }

    public JsonPoliceRulesStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public PoliceRules Load()
    {
        try
        {
            if (!File.Exists(_file))
            {
                _log.LogWarning($"police.json missing at {_file}; using built-in defaults");
                return PoliceRules.Default;
            }

            PoliceRules? rules = JsonSerializer.Deserialize<PoliceRules>(
                File.ReadAllText(_file), JsonOptions);
            if (rules is null || !IsValid(rules))
            {
                _log.LogWarning($"police.json invalid at {_file}; using built-in defaults");
                return PoliceRules.Default;
            }

            _log.LogInfo(
                $"police rules loaded: patrols={rules.PatrolsPerJob} " +
                $"radius={rules.PatrolRadiusMeters:0} m limit={rules.SpeedLimitKmh} km/h " +
                $"stop clear={rules.ExcludedStopRadiusMeters:0} m " +
                $"warning={rules.WarningRadiusMeters:0} m");
            return rules;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Read/deserialize boundary only: a corrupt or locked data file
            // must not take the overlay down (AGENTS.md: Try* at every
            // external boundary). Anything else stays a crash.
            _log.LogWarning(
                $"police.json unreadable at {_file} ({ex.GetType().Name}); using built-in defaults");
            return PoliceRules.Default;
        }
    }

    private static bool IsValid(PoliceRules rules) =>
        rules.PatrolsPerJob >= 0
        && double.IsFinite(rules.PatrolRadiusMeters) && rules.PatrolRadiusMeters > 0.0
        && rules.SpeedLimitKmh > 0
        && double.IsFinite(rules.ExcludedStopRadiusMeters) && rules.ExcludedStopRadiusMeters >= 0.0
        && double.IsFinite(rules.WarningRadiusMeters) && rules.WarningRadiusMeters >= 0.0;
}
