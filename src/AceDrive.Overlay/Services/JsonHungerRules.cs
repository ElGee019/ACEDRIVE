using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the appetite rules from Data/hunger_rules.json next to the exe.
/// Missing or invalid content degrades to <see cref="HungerRules.Default"/>
/// (Unity's shipped config) with a warning — the overlay must never fail to
/// start over a data file, and a blind driver is worse than defaults.
/// </summary>
public sealed class JsonHungerRules
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonHungerRules(ILog log)
        : this(Path.Combine(AppContext.BaseDirectory, "Data", "hunger_rules.json"), log)
    { }

    public JsonHungerRules(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public HungerRules Load()
    {
        try
        {
            if (!File.Exists(_file))
            {
                _log.LogWarning($"hunger_rules.json missing at {_file}; using built-in defaults");
                return HungerRules.Default;
            }
            HungerRules? rules = JsonSerializer.Deserialize<HungerRules>(
                File.ReadAllText(_file), JsonOptions);
            if (rules is null || !IsValid(rules))
            {
                _log.LogWarning($"hunger_rules.json invalid at {_file}; using built-in defaults");
                return HungerRules.Default;
            }
            _log.LogInfo($"hunger rules loaded: +{rules.PerHour} per game hour, " +
                $"cap {rules.MaxHunger}, pancake threshold {rules.PancakeThreshold}");
            return rules;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Read/deserialize boundary only: a corrupt or locked data file
            // must not take the overlay down (AGENTS.md: Try* at every
            // external boundary). Anything else stays a crash.
            _log.LogWarning($"hunger_rules.json unreadable at {_file} ({ex.GetType().Name}); using built-in defaults");
            return HungerRules.Default;
        }
    }

    // The increment must be real or the meter never moves; the cap must be
    // reachable; and the threshold must sit strictly under the cap — Unity's
    // UpdateHunger returns at full hunger, so an event AT the cap can never
    // fire. The amber band is display data and never loaded.
    private static bool IsValid(HungerRules rules) =>
        rules.PerHour > 0 && rules.MaxHunger > 0
        && rules.PancakeThreshold > 0 && rules.PancakeThreshold < rules.MaxHunger;
}
