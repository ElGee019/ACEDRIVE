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
    private readonly string _file;
    private readonly ILog _log;

    public JsonHungerRules(ILog log)
        : this(DataPaths.GameData("hunger_rules.json"), log)
    { }

    public JsonHungerRules(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public HungerRules Load()
    {
        if (!JsonTable.TryLoad(
                _log, _file, "hunger_rules.json", "using built-in defaults",
                IsValid, out HungerRules? rules))
        {
            return HungerRules.Default;
        }

        _log.LogInfo($"hunger rules loaded: +{rules.PerHour} per game hour, " +
            $"cap {rules.MaxHunger}, pancake threshold {rules.PancakeThreshold}");
        return rules;
    }

    // The increment must be real or the meter never moves; the cap must be
    // reachable; and the threshold must sit strictly under the cap — Unity's
    // UpdateHunger returns at full hunger, so an event AT the cap can never
    // fire. The amber band is display data and never loaded.
    private static bool IsValid(HungerRules rules) =>
        rules.PerHour > 0 && rules.MaxHunger > 0
        && rules.PancakeThreshold > 0 && rules.PancakeThreshold < rules.MaxHunger;
}
