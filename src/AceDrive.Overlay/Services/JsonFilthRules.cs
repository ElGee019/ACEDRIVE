using System.IO;
using System.Text.Json;
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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonFilthRules(ILog log)
        : this(Path.Combine(AppContext.BaseDirectory, "Data", "filth_rules.json"), log)
    { }

    public JsonFilthRules(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public FilthRules Load()
    {
        try
        {
            if (!File.Exists(_file))
            {
                _log.LogWarning($"filth_rules.json missing at {_file}; using built-in defaults");
                return FilthRules.Default;
            }
            FilthRules? rules = JsonSerializer.Deserialize<FilthRules>(
                File.ReadAllText(_file), JsonOptions);
            if (rules is null || !IsValid(rules))
            {
                _log.LogWarning($"filth_rules.json invalid at {_file}; using built-in defaults");
                return FilthRules.Default;
            }
            _log.LogInfo($"filth rules loaded: cap {rules.MaxFilth}, wash CR{rules.WashPrice}");
            return rules;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Read/deserialize boundary only: a corrupt or locked data file
            // must not take the overlay down (AGENTS.md: Try* at every
            // external boundary). Anything else stays a crash.
            _log.LogWarning($"filth_rules.json unreadable at {_file} ({ex.GetType().Name}); using built-in defaults");
            return FilthRules.Default;
        }
    }

    // The cap must be reachable and the wash must cost something. The amber
    // band is display data (MapScreen) and never gates a legal file: a cap
    // below the band simply never wears the amber colour.
    private static bool IsValid(FilthRules rules) =>
        rules.MaxFilth > 0 && rules.WashPrice > 0;
}
