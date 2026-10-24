using System.IO;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads fare balance from Data/fares.json (taxi) and Data/delivery_fares.json
/// (delivery) next to the exe. Missing or invalid files degrade to the
/// built-in defaults with a warning — the overlay must never fail to start
/// over a data file.
/// </summary>
public sealed class JsonFareStore
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonFareStore(ILog log)
        : this(DataPaths.GameData("fares.json"), log)
    { }

    public JsonFareStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public FareTable Load()
    {
        JsonReadStatus status = JsonFile.TryRead(_file, out FareTable? table, out string? error);
        if (status == JsonReadStatus.Missing)
        {
            _log.LogWarning($"fares.json missing at {_file}; using built-in defaults");
            return FareTable.Default;
        }

        // A corrupt or locked data file must not take the overlay down
        // (AGENTS.md: Try* at every external boundary).
        if (table is null || !IsValid(table))
        {
            _log.LogWarning(
                $"fares.json {JsonFile.Describe(error)} at {_file}; using built-in defaults");
            return FareTable.Default;
        }

        _log.LogInfo($"fares loaded: forfait={table.Forfait} tip={table.TipRate} fine={table.FineAmount} tiers={table.Tiers.Count}");
        return table;
    }

    /// <summary>
    /// The delivery distance-charge table from its own file, same degrade
    /// rule (Unity's delivery rates live beside the taxi table but price an
    /// otherwise different fare: no forfait, tip or fine).
    /// </summary>
    public DeliveryFareTable LoadDelivery()
    {
        string file = Path.Combine(
            Path.GetDirectoryName(_file)!, "delivery_fares.json");
        JsonReadStatus status = JsonFile.TryRead(file, out DeliveryFareTable? table, out string? error);
        if (status == JsonReadStatus.Missing)
        {
            _log.LogWarning($"delivery_fares.json missing at {file}; using built-in defaults");
            return DeliveryFareTable.Default;
        }

        if (table is null || !FareTierRules.Valid(table.Tiers))
        {
            _log.LogWarning(
                $"delivery_fares.json {JsonFile.Describe(error)} at {file}; using built-in defaults");
            return DeliveryFareTable.Default;
        }

        _log.LogInfo($"delivery fares loaded: tiers={table.Tiers.Count}");
        return table;
    }

    // A table needs a non-negative forfait, a 0-100% tip rate, a
    // non-negative fine and a valid tier table; the tier shape is shared
    // with the delivery table (FareTierRules), so both loaders agree.
    private static bool IsValid(FareTable table)
    {
        if (table.Forfait < 0 || table.TipRate is < 0 or > 1 || table.FineAmount < 0)
        {
            return false;
        }
        return FareTierRules.Valid(table.Tiers);
    }
}
