using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the car-condition rules from Data/maintenance_rules.json next to the
/// exe. Missing or invalid content degrades to <see cref="MaintenanceRules.Default"/>
/// (Unity's shipped tables) with a warning — the overlay must never fail to
/// start over a data file, and a car with no wear model is worse than defaults.
/// </summary>
public sealed class JsonMaintenanceRules
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonMaintenanceRules(ILog log)
        : this(Path.Combine(AppContext.BaseDirectory, "Data", "maintenance_rules.json"), log)
    { }

    public JsonMaintenanceRules(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public MaintenanceRules Load()
    {
        try
        {
            if (!File.Exists(_file))
            {
                _log.LogWarning($"maintenance_rules.json missing at {_file}; using built-in defaults");
                return MaintenanceRules.Default;
            }
            MaintenanceRules? rules = JsonSerializer.Deserialize<MaintenanceRules>(
                File.ReadAllText(_file), JsonOptions);
            if (rules is null || !IsValid(rules))
            {
                _log.LogWarning($"maintenance_rules.json invalid at {_file}; using built-in defaults");
                return MaintenanceRules.Default;
            }
            _log.LogInfo($"maintenance rules loaded: {rules.Parts.Count} parts, " +
                $"breakdown cap {rules.MaxBreakdownChance:0.00}, pickup CR{rules.PickupFee:0}");
            return rules;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Read/deserialize boundary only: a corrupt or locked data file
            // must not take the overlay down (AGENTS.md: Try* at every
            // external boundary). Anything else stays a crash.
            _log.LogWarning($"maintenance_rules.json unreadable at {_file} ({ex.GetType().Name}); using built-in defaults");
            return MaintenanceRules.Default;
        }
    }

    // Every guard rejects a table that would misbehave at use: a part that can
    // never wear or is free, a class step that leaves a class unpriced, an
    // engine band order that makes RedKmFor pick the wrong threshold, or a
    // schedule that never fires. A malformed table is a data bug, so the whole
    // file falls back to the shipped defaults instead of loading half.
    private static bool IsValid(MaintenanceRules rules)
    {
        if (rules.Parts.Count == 0 || rules.ClassBands.Count == 0 || rules.EngineBands.Count == 0)
        {
            return false;
        }
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (MaintenancePart part in rules.Parts)
        {
            if (part.Key is not { Length: > 0 } || part.IntervalKm <= 0.0
                || part.FullPrice <= 0.0 || !keys.Add(part.Key))
            {
                return false;
            }
        }
        for (int i = 0; i < rules.ClassBands.Count; i++)
        {
            ClassPriceBand band = rules.ClassBands[i];
            if (band.Multiplier is <= 0.0 or > 1.0)
            {
                return false;
            }
            if (i > 0 && rules.ClassBands[i - 1].MinClass <= band.MinClass)
            {
                return false; // not strictly descending: MultiplierFor would skip a band
            }
        }
        if (rules.ClassBands[^1].MinClass > 0)
        {
            return false; // the lowest band must cover class 0
        }
        for (int i = 0; i < rules.EngineBands.Count; i++)
        {
            if (rules.EngineBands[i].RedKm <= 0.0)
            {
                return false;
            }
            if (i > 0 && rules.EngineBands[i - 1].MaxCc >= rules.EngineBands[i].MaxCc)
            {
                return false; // not strictly ascending: RedKmFor could pick the wrong band
            }
        }
        return rules.CurveExponent > 0.0
            && rules.MaxBreakdownChance is > 0.0 and <= 1.0
            && rules.BreakdownFractionMin >= 0.0
            && rules.BreakdownFractionMax <= 1.0
            && rules.BreakdownFractionMin <= rules.BreakdownFractionMax
            && rules.PickupFee >= 0.0
            && rules.EngineRepairFraction is > 0.0 and <= 1.0
            && rules.NewEngineFraction is > 0.0 and <= 1.0
            && rules.WeeklyIntervalDays > 0
            && rules.YearIntervalDays > 0
            && rules.YearJitterDays >= 0;
    }
}
