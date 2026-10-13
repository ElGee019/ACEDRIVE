// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 16-19
// (Delivery Job page, port of Unity DeliveryJobScreen + DeliveryCartAccounting).
namespace AceDrive.Core;

/// <summary>
/// Phase of the delivery career page. Unity DeliveryJobScreen rule: the
/// main button reads OPEN NEW CAREER while nothing waits in the pickup
/// cart and flips to DELIVER once an order is confirmed.
/// </summary>
public enum DeliveryPhase
{
    OpenCareer,
    Deliver,
}

/// <summary>Stock color band for a store row (Unity thresholds, as data —
/// the view owns the actual paints).</summary>
public enum StockLevel
{
    Red,
    Amber,
    Green,
}

/// <summary>
/// One store that accepts wholesale restock orders. Plain data; money in
/// EUR. Stocked/capacity mirror the Unity slots model collapsed to unit
/// counts until the per-item order screen (PORTPLAN 17) lands.
/// </summary>
public sealed record DeliveryStore(
    string Key,
    string Name,
    string Owner,
    int StockedUnits,
    int UnitCapacity,
    double PricePerUnit,
    int Citizens,
    int GrowthPerHour)
{
    /// <summary>Unity StoreListView: rows under 25 % flag as URGENT.</summary>
    public const double UrgentPercent = 25.0;

    /// <summary>Unity color-band edge between amber and green.</summary>
    public const double LowPercent = 60.0;

    public int UnitsNeeded => Math.Max(0, UnitCapacity - StockedUnits);

    public double StockPercent => UnitCapacity <= 0
        ? 0.0
        : 100.0 * Math.Clamp(StockedUnits, 0, UnitCapacity) / UnitCapacity;

    public bool IsUrgent => StockPercent < UrgentPercent;

    public StockLevel Level => StockPercent switch
    {
        < UrgentPercent => StockLevel.Red,
        < LowPercent => StockLevel.Amber,
        _ => StockLevel.Green,
    };
}

/// <summary>
/// Balance/content table for the delivery page. Loaded from
/// Data/delivery_catalog.json at startup; this Default is the test-shell
/// fallback (empty board), not the source of truth.
/// </summary>
public sealed record DeliveryCatalog(
    IReadOnlyList<DeliveryStore> Stores,
    double WholesaleRate,
    int StartCredP)
{
    public static readonly DeliveryCatalog Default = new([], 0.7, 100);

    public int TotalCitizens
    {
        get
        {
            int total = 0;
            foreach (DeliveryStore store in Stores) total += store.Citizens;
            return total;
        }
    }

    public int CityGrowthPerHour
    {
        get
        {
            int total = 0;
            foreach (DeliveryStore store in Stores) total += store.GrowthPerHour;
            return total;
        }
    }

    public DeliveryStore? Find(string key)
    {
        foreach (DeliveryStore store in Stores)
        {
            if (store.Key == key) return store;
        }
        return null;
    }

    /// <summary>
    /// Wholesale cost of one full-restock order (Unity contract: the hub
    /// charges WholesaleRate x retail per unit; the 0.9x retail payout
    /// belongs to the run, not this page).
    /// </summary>
    public double OrderCost(DeliveryStore store) =>
        Math.Round(store.PricePerUnit * WholesaleRate * store.UnitsNeeded, 2);
}

/// <summary>
/// Confirmed restock orders waiting for hub pickup (Unity
/// player.pickupCart). One full-restock order per store until the per-item
/// order screen arrives; until then a confirmed store re-answers to its
/// row and drops the order.
/// </summary>
public sealed class DeliveryCart
{
    private readonly List<string> _storeKeys = new();

    public IReadOnlyList<string> ConfirmedKeys => _storeKeys;

    public bool IsEmpty => _storeKeys.Count == 0;

    public DeliveryPhase Phase => IsEmpty ? DeliveryPhase.OpenCareer : DeliveryPhase.Deliver;

    public bool Contains(string key) => _storeKeys.Contains(key);

    /// <summary>Confirms an order; already-confirmed stores are left as-is.</summary>
    public void Confirm(string key)
    {
        if (!Contains(key)) _storeKeys.Add(key);
    }

    public void Remove(string key) => _storeKeys.Remove(key);

    public void Clear() => _storeKeys.Clear();

    /// <summary>Total wholesale CR committed by the cart (escrow model,
    /// released on the save/economy page; PORTPLAN 19 + 51).</summary>
    public double TotalCost(DeliveryCatalog catalog)
    {
        double total = 0.0;
        foreach (string key in _storeKeys)
        {
            DeliveryStore? store = catalog.Find(key);
            if (store is not null) total += catalog.OrderCost(store);
        }
        return Math.Round(total, 2);
    }
}
