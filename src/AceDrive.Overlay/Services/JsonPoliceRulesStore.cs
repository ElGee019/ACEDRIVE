using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the speed-challenge police rules from Data/police.json next to the
/// exe. Missing or invalid files degrade to the built-in defaults with a
/// warning — the overlay must never fail to start over a data file.
/// </summary>
public sealed class JsonPoliceRulesStore
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonPoliceRulesStore(ILog log)
        : this(DataPaths.GameData("police.json"), log)
    { }

    public JsonPoliceRulesStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public PoliceRules Load()
    {
        if (!JsonTable.TryLoad(
                _log, _file, "police.json", "using built-in defaults",
                IsValid, out PoliceRules? rules))
        {
            return PoliceRules.Default;
        }

        _log.LogInfo(
            $"police rules loaded: patrols={rules.PatrolsPerJob} " +
            $"radius={rules.PatrolRadiusMeters:0} m limit={rules.SpeedLimitKmh} km/h " +
            $"stop clear={rules.ExcludedStopRadiusMeters:0} m " +
            $"warning={rules.WarningRadiusMeters:0} m");
        return rules;
    }

    private static bool IsValid(PoliceRules rules) =>
        rules.PatrolsPerJob >= 0
        && double.IsFinite(rules.PatrolRadiusMeters) && rules.PatrolRadiusMeters > 0.0
        && rules.SpeedLimitKmh > 0
        && double.IsFinite(rules.ExcludedStopRadiusMeters) && rules.ExcludedStopRadiusMeters >= 0.0
        && double.IsFinite(rules.WarningRadiusMeters) && rules.WarningRadiusMeters >= 0.0;
}
