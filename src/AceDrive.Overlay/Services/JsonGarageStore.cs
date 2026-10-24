using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads/saves the garage under %AppData%\AceDrive. Never throws: a missing or
/// corrupt file means an empty garage, and a failed write must never break
/// gameplay or shutdown. Unusable car rows are dropped with a warning (an empty
/// key, a duplicate key, a negative odometer) so a hand-edited file degrades to a
/// smaller garage; the store then never hands the Garage state it would reject.
/// </summary>
public sealed class JsonGarageStore : IGarageStore
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonGarageStore(ILog log)
        : this(DataPaths.UserSave("garage.json"), log)
    { }

    public JsonGarageStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public GarageState? Load()
    {
        JsonReadStatus status = JsonFile.TryRead(_file, out GarageState? state, out string? error);
        if (status == JsonReadStatus.Missing)
        {
            return null;
        }

        // A corrupt save must degrade to an empty garage, never take the
        // overlay down (AGENTS.md: Try* at every external boundary).
        if (state is null)
        {
            _log.LogWarning(
                $"garage.json {JsonFile.Describe(error)} at {_file}; starting with an empty garage");
            return null;
        }

        GarageState cleaned = Sanitize(state);
        _log.LogInfo($"garage loaded: {cleaned.Cars?.Count ?? 0} cars, current {cleaned.CurrentKey ?? "none"}");
        return cleaned;
    }

    public void Save(GarageState state)
    {
        // A failed bank must never break gameplay or shutdown; the next
        // purchase or switch retries with fresher numbers.
        if (!JsonFile.TryWrite(_file, state, out string? error))
        {
            _log.LogWarning(
                $"garage.json unwritable at {_file} ({error}); progress kept in memory");
        }
    }

    // One pass over the rows: trim keys, drop unusable ones, keep the first of a
    // duplicate key (a car owned twice would double every per-car number later),
    // and clear a current key no car backs.
    private GarageState Sanitize(GarageState state)
    {
        List<OwnedCarState> cars = [];
        var keys = new HashSet<string>(StringComparer.Ordinal);
        int dropped = 0;
        foreach (OwnedCarState row in state.Cars ?? [])
        {
            string key = (row.Key ?? "").Trim();
            if (key.Length == 0
                || row.Odometer < 0.0 || double.IsNaN(row.Odometer) || double.IsInfinity(row.Odometer))
            {
                dropped++;
                continue;
            }
            // Register the key only for a row that survives every check: a
            // half-valid row must not reserve its key and so drop the good row
            // behind it as a duplicate.
            if (!keys.Add(key))
            {
                dropped++;
                continue;
            }
            cars.Add(row with
            {
                Key = key,
                PurchaseWeek = Math.Max(0, row.PurchaseWeek),
                Parts = CleanParts(row.Parts),
                Engine = CleanEngine(row.Engine),
            });
        }
        if (dropped > 0)
        {
            _log.LogWarning($"garage.json dropped {dropped} unusable car rows at {_file}");
        }
        string? current = state.CurrentKey is { Length: > 0 } key2 && keys.Contains(key2) ? key2 : null;
        return new GarageState(current, cars);
    }

    // The nested condition state (PORTPLAN items 31/32) follows the row rule:
    // drop a part row with no key, a duplicate key or a non-finite/negative
    // baseline, so a hand-edited file degrades to a smaller deck instead of a
    // car with an unreadable one. A missing list stays missing (the CarCondition
    // backfills it at the current odometer).
    private static List<MaintenancePartState>? CleanParts(IReadOnlyList<MaintenancePartState>? parts)
    {
        if (parts is null)
        {
            return null;
        }
        List<MaintenancePartState> rows = [];
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (MaintenancePartState row in parts)
        {
            string key = (row.Key ?? "").Trim();
            if (key.Length == 0 || row.LastServiceKm < 0.0
                || double.IsNaN(row.LastServiceKm) || double.IsInfinity(row.LastServiceKm)
                || !keys.Add(key))
            {
                continue;
            }
            rows.Add(row with { Key = key });
        }
        return rows;
    }

    // A non-finite or negative engine base would poison BrokenPercent, so it
    // clamps; the broken flag and the inspection moments pass through untouched.
    private static EngineConditionState? CleanEngine(EngineConditionState? engine) =>
        engine is null || (double.IsFinite(engine.BaseOdometer) && engine.BaseOdometer >= 0.0)
            ? engine
            : engine with { BaseOdometer = 0.0 };
}
