using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads fare balance from Data/fares.json next to the exe. Missing or
/// invalid file degrades to <see cref="FareTable.Default"/> with a warning —
/// the overlay must never fail to start over a data file.
/// </summary>
public sealed class JsonFareStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonFareStore(ILog log)
        : this(Path.Combine(AppContext.BaseDirectory, "Data", "fares.json"), log)
    { }

    public JsonFareStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public FareTable Load()
    {
        try
        {
            if (!File.Exists(_file))
            {
                _log.LogWarning($"fares.json missing at {_file}; using built-in defaults");
                return FareTable.Default;
            }
            var table = JsonSerializer.Deserialize<FareTable>(
                File.ReadAllText(_file), JsonOptions);
            if (table is null || !IsValid(table))
            {
                _log.LogWarning($"fares.json invalid at {_file}; using built-in defaults");
                return FareTable.Default;
            }
            _log.LogInfo($"fares loaded: forfait={table.Forfait} tip={table.TipRate} fine={table.FineAmount} tiers={table.Tiers.Count}");
            return table;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Read/deserialize boundary only: a corrupt or locked data file
            // must not take the overlay down (AGENTS.md: Try* at every
            // external boundary). Anything else stays a crash.
            _log.LogWarning($"fares.json unreadable at {_file} ({ex.GetType().Name}); using built-in defaults");
            return FareTable.Default;
        }
    }

    // A table needs a non-negative forfait, a 0-100% tip rate, a
    // non-negative fine and at least one tier with a non-negative rate;
    // only the open top tier may carry a non-positive width.
    private static bool IsValid(FareTable table)
    {
        if (table.Forfait < 0 || table.TipRate is < 0 or > 1 || table.FineAmount < 0)
        {
            return false;
        }
        if (table.Tiers.Count == 0)
        {
            return false;
        }
        for (int i = 0; i < table.Tiers.Count; i++)
        {
            FareTier tier = table.Tiers[i];
            bool isLast = i == table.Tiers.Count - 1;
            if ((!isLast && tier.WidthKm <= 0.0) || tier.RatePerKm < 0.0)
            {
                return false;
            }
        }
        return true;
    }
}
