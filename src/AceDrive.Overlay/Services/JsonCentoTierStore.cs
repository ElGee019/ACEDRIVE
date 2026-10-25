using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the Cento Miglia tiers from Data/cento_miglia.json next to the exe.
/// The list is content (club names, descriptions, club levels), not balance:
/// a missing or invalid file leaves the page with its header and BACK and one
/// warning in the log, because the overlay must never fail to start over a
/// data file. Rows that cannot be shown are dropped instead of failing the
/// whole table, the same degrade rule the car loader uses.
/// </summary>
public sealed class JsonCentoTierStore
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonCentoTierStore(ILog log)
        : this(DataPaths.GameData("cento_miglia.json"), log)
    { }

    public JsonCentoTierStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public IReadOnlyList<CentoTier> Load()
    {
        JsonReadStatus status =
            JsonFile.TryRead(_file, out List<CentoTier>? tiers, out string? error);
        if (status == JsonReadStatus.Missing)
        {
            _log.LogWarning($"cento_miglia.json missing at {_file}; no event tiers");
            return [];
        }

        // A corrupt or locked data file must not take the overlay down
        // (AGENTS.md: Try* at every external boundary).
        if (tiers is null)
        {
            _log.LogWarning(
                $"cento_miglia.json {JsonFile.Describe(error)} at {_file}; no event tiers");
            return [];
        }

        var valid = new List<CentoTier>(tiers.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (CentoTier tier in tiers)
        {
            if (IsUsable(tier) && seen.Add(tier.Id))
            {
                valid.Add(tier);
            }
        }

        _log.LogInfo($"cento miglia tiers loaded: {valid.Count} of {tiers.Count}");
        return valid;
    }

    // A tier needs an id the gates can key on, copy to print and a level that
    // is not negative; a negative level would lock a club forever.
    private static bool IsUsable(CentoTier tier) =>
        !string.IsNullOrWhiteSpace(tier.Id)
        && !string.IsNullOrWhiteSpace(tier.Label)
        && !string.IsNullOrWhiteSpace(tier.Description)
        && tier.ReputationRequired >= 0;
}
