using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the filth rules from Data/filth_rules.json next to the exe. Missing
/// or invalid content degrades to <see cref="FilthRules.Default"/> (Unity's
/// shipped CarWashService) with a warning — the overlay must never fail to
/// start over a data file.
/// </summary>
public sealed class JsonFilthRules
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonFilthRules(ILog log)
        : this(DataPaths.GameData("filth_rules.json"), log)
    { }

    public JsonFilthRules(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public FilthRules Load()
    {
        if (!JsonTable.TryLoad(
                _log, _file, "filth_rules.json", "using built-in defaults",
                IsValid, out FilthRules? rules))
        {
            return FilthRules.Default;
        }

        _log.LogInfo($"filth rules loaded: cap {rules.MaxFilth}, wash CR{rules.WashPrice}");
        return rules;
    }

    // The cap must be reachable and the wash must cost something. The amber
    // band is display data (MapScreen) and never gates a legal file: a cap
    // below the band simply never wears the amber colour.
    private static bool IsValid(FilthRules rules) =>
        rules.MaxFilth > 0 && rules.WashPrice > 0;
}
