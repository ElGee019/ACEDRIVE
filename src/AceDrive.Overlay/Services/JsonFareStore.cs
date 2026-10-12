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
            if (table is null || table.BaseFare < 0 || table.PerKmRate < 0
                || table.TipRate is < 0 or > 1)
            {
                _log.LogWarning($"fares.json invalid at {_file}; using built-in defaults");
                return FareTable.Default;
            }
            _log.LogInfo($"fares loaded: base={table.BaseFare} perKm={table.PerKmRate} tip={table.TipRate}");
            return table;
        }
        catch (Exception ex)
        {
            _log.LogWarning($"fares.json unreadable at {_file} ({ex.GetType().Name}); using built-in defaults");
            return FareTable.Default;
        }
    }
}
