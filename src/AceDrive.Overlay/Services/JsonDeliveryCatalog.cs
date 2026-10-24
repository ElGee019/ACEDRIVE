using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the delivery balance (rates, Max's intro CredP) from
/// Data/delivery_catalog.json next to the exe. The optional Stores array is
/// only hand-authored content; real board rows come from the shop POIs
/// (<see cref="PlacedStore"/>). Missing or invalid files degrade to
/// <see cref="DeliveryCatalog.Default"/> (an empty board) with a warning —
/// the overlay must never fail to start over a data file, and the page
/// shows its empty state instead.
/// </summary>
public sealed class JsonDeliveryCatalog
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonDeliveryCatalog(ILog log)
        : this(DataPaths.GameData("delivery_catalog.json"), log)
    { }

    public JsonDeliveryCatalog(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public DeliveryCatalog Load()
    {
        if (!JsonTable.TryLoad(
                _log, _file, "delivery_catalog.json", "using built-in defaults",
                IsValid, out DeliveryCatalog? catalog))
        {
            return DeliveryCatalog.Default;
        }

        _log.LogInfo($"delivery catalog loaded: {catalog.Stores.Count} stores");
        return catalog;
    }

    // Wholesale rate must be a real discount cut and the payout rate a real
    // retail take (Unity: 0.7 buys, 0.9 sells); every store needs a unique
    // key, a name to show and a positive capacity with stock inside its
    // bounds, or the row math (need, %, cost) lies.
    private static bool IsValid(DeliveryCatalog catalog)
    {
        if (catalog.WholesaleRate is <= 0 or > 1
            || catalog.PayoutRate is <= 0 or > 1
            || catalog.StartCredP < 0)
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
                || (store.Address is { } address
                    && (string.IsNullOrWhiteSpace(address.StreetName)
                        || address.HouseNumber < 1))
                || !keys.Add(store.Key))
            {
                return false;
            }
        }
        return true;
    }
}
