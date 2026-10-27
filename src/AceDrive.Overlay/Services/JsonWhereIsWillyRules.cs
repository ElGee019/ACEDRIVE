using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the missing-person run's balance from Data/willy_rules.json next to
/// the exe. Missing or invalid files degrade to
/// <see cref="WhereIsWillyRules.Default"/> with a warning — the overlay must
/// never fail to start over a data file (ToWhereIsWilly.md §3.2).
/// </summary>
public sealed class JsonWhereIsWillyRules
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonWhereIsWillyRules(ILog log)
        : this(DataPaths.GameData("willy_rules.json"), log)
    { }

    public JsonWhereIsWillyRules(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public WhereIsWillyRules Load()
    {
        JsonReadStatus status = JsonFile.TryRead(_file, out WhereIsWillyRules? rules, out string? error);
        if (status == JsonReadStatus.Missing)
        {
            _log.LogWarning($"willy_rules.json missing at {_file}; using built-in defaults");
            return WhereIsWillyRules.Default;
        }

        if (rules is null)
        {
            _log.LogWarning(
                $"willy_rules.json {JsonFile.Describe(error)} at {_file}; using built-in defaults");
            return WhereIsWillyRules.Default;
        }

        if (!rules.IsValid)
        {
            _log.LogWarning($"willy_rules.json invalid at {_file}; using built-in defaults");
            return WhereIsWillyRules.Default;
        }

        _log.LogInfo(
            $"willy rules loaded: fee {rules.FinderFeeRate:P0} of the average car, " +
            $"floor {rules.MinReward:0} CR, attempts {rules.AttemptBase:0.#}-" +
            $"{rules.AttemptFloor:0.#}, {rules.ShopBands.Count} shop band(s)");
        return rules;
    }
}
