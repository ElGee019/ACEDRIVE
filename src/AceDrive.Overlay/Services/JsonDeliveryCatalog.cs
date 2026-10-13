using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads delivery board content from Data/delivery_catalog.json next to
/// the exe. Missing or invalid files degrade to
/// <see cref="DeliveryCatalog.Default"/> (an empty board) with a warning —
/// the overlay must never fail to start over a data file, and the page
/// shows its empty state instead.
/// </summary>
public sealed class JsonDeliveryCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonDeliveryCatalog(ILog log)
        : this(Path.Combine(AppContext.BaseDirectory, "Data", "delivery_catalog.json"), log)
    { }

    public JsonDeliveryCatalog(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public DeliveryCatalog Load()
    {
        try
        {
            if (!File.Exists(_file))
            {
                _log.LogWarning($"delivery_catalog.json missing at {_file}; using built-in defaults");
                return DeliveryCatalog.Default;
            }
            var catalog = JsonSerializer.Deserialize<DeliveryCatalog>(
                File.ReadAllText(_file), JsonOptions);
            if (catalog is null || !IsValid(catalog))
            {
                _log.LogWarning($"delivery_catalog.json invalid at {_file}; using built-in defaults");
                return DeliveryCatalog.Default;
            }
            _log.LogInfo($"delivery catalog loaded: {catalog.Stores.Count} stores");
            return catalog;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Read/deserialize boundary only: a corrupt or locked data file
            // must not take the overlay down (AGENTS.md: Try* at every
            // external boundary). Anything else stays a crash.
            _log.LogWarning($"delivery_catalog.json unreadable at {_file} ({ex.GetType().Name}); using built-in defaults");
            return DeliveryCatalog.Default;
        }
    }

    // Wholesale rate must be a real discount cut (Unity: 0.7 buys, 0.9 sells);
    // every store needs a unique key, a name to show and a positive capacity
    // with stock inside its bounds, or the row math (need, %, cost) lies.
    private static bool IsValid(DeliveryCatalog catalog)
    {
        if (catalog.WholesaleRate is <= 0 or > 1 || catalog.StartCredP < 0)
        {
            return false;
        }
        var keys = new HashSet<string>();
        foreach (DeliveryStore store in catalog.Stores)
        {
            if (store.Key.Length == 0 || store.Name.Length == 0
                || store.Owner.Length == 0
                || store.UnitCapacity <= 0
                || store.StockedUnits < 0 || store.StockedUnits > store.UnitCapacity
                || store.PricePerUnit < 0
                || !keys.Add(store.Key))
            {
                return false;
            }
        }
        return true;
    }
}
