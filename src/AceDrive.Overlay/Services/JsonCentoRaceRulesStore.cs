using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the Cento Miglia race balance from Data/cento_race.json next to the
/// exe (PORTPLAN item 49): the placement ladder, the Star of the Show curve and
/// the generated item names. Balance is content, not code, so the overlay never
/// ships these numbers in a class (AGENTS: no magic balance values). A missing
/// or invalid file answers Unity's own values instead of failing the start: the
/// race must still be playable after a bad edit, and the log names the file.
/// </summary>
public sealed class JsonCentoRaceRulesStore
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonCentoRaceRulesStore(ILog log)
        : this(DataPaths.GameData("cento_race.json"), log)
    { }

    public JsonCentoRaceRulesStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public CentoRaceRules Load()
    {
        JsonReadStatus status =
            JsonFile.TryRead(_file, out CentoRaceRules? rules, out string? error);
        if (status == JsonReadStatus.Missing)
        {
            _log.LogWarning(
                $"cento_race.json missing at {_file}; using the built-in race balance");
            return CentoRaceRules.Default;
        }

        // A corrupt or locked data file must not take the overlay down
        // (AGENTS.md: Try* at every external boundary).
        if (rules is null)
        {
            _log.LogWarning(
                $"cento_race.json {JsonFile.Describe(error)} at {_file}; " +
                "using the built-in race balance");
            return CentoRaceRules.Default;
        }

        if (!IsUsable(rules))
        {
            _log.LogWarning(
                $"cento_race.json at {_file} has an unusable balance; " +
                "using the built-in race balance");
            return CentoRaceRules.Default;
        }

        _log.LogInfo($"cento race balance loaded: {rules.PlacementEarnings.Count} places, " +
            $"star prize {rules.StarPrizePerWin:0}");
        return rules;
    }

    // One broken field would break either the purse or the award, so the whole
    // table falls back together: a half-applied balance is worse than Unity's
    // own numbers.
    private static bool IsUsable(CentoRaceRules rules) =>
        rules.PlacementEarnings is { Count: > 0 }
        && rules.StarPlacementMultipliers is { Count: > 0 }
        && IsChance(rules.StarBaseChance)
        && double.IsFinite(rules.StarSteepness) && rules.StarSteepness >= 0.0
        && double.IsFinite(rules.StarPrizePerWin) && rules.StarPrizePerWin >= 0.0
        && !string.IsNullOrWhiteSpace(rules.AwardItemName)
        && !string.IsNullOrWhiteSpace(rules.CertificateItemName);

    private static bool IsChance(double chance) =>
        double.IsFinite(chance) && chance >= 0.0 && chance <= 1.0;
}
