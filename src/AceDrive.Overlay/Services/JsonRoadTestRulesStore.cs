using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the road-test receipt balance from Data/road_test.json next to the exe
/// (PORTPLAN item 49): the flat forfeit, the magazine price divisor and the
/// commission the issue pays. Balance is content, not code, so the overlay
/// never ships these numbers in a class (AGENTS: no magic balance values). A
/// missing or invalid file answers Unity's own values instead of failing the
/// start: the test must still settle after a bad edit, and the log names the
/// file.
/// </summary>
public sealed class JsonRoadTestRulesStore
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonRoadTestRulesStore(ILog log)
        : this(DataPaths.GameData("road_test.json"), log)
    { }

    public JsonRoadTestRulesStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public RoadTestAudienceRules Load()
    {
        JsonReadStatus status =
            JsonFile.TryRead(_file, out RoadTestAudienceRules? rules, out string? error);
        if (status == JsonReadStatus.Missing)
        {
            _log.LogWarning(
                $"road_test.json missing at {_file}; using the built-in issue balance");
            return RoadTestAudienceRules.Default;
        }

        // A corrupt or locked data file must not take the overlay down
        // (AGENTS.md: Try* at every external boundary).
        if (rules is null)
        {
            _log.LogWarning(
                $"road_test.json {JsonFile.Describe(error)} at {_file}; " +
                "using the built-in issue balance");
            return RoadTestAudienceRules.Default;
        }

        if (!IsUsable(rules))
        {
            _log.LogWarning(
                $"road_test.json at {_file} has an unusable balance; " +
                "using the built-in issue balance");
            return RoadTestAudienceRules.Default;
        }

        _log.LogInfo(
            $"road test balance loaded: forfeit {rules.BaseForfeit} CR, " +
            $"divisor {rules.MagazinePriceDivisor:0.##}, " +
            $"commission {rules.CommissionPercent:0.##}%");
        return rules;
    }

    // One broken field would skew the earnings the page prints, so the whole
    // table falls back together: a half-applied balance is worse than Unity's
    // own numbers.
    private static bool IsUsable(RoadTestAudienceRules rules) =>
        rules.BaseForfeit >= 0
        && double.IsFinite(rules.MagazinePriceDivisor) && rules.MagazinePriceDivisor > 0.0
        && double.IsFinite(rules.CommissionPercent) && rules.CommissionPercent >= 0.0;
}
