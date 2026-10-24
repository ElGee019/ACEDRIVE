// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 16-19
// (Delivery Job page, port of Unity DeliveryJobScreen + DeliveryCartAccounting).
namespace AceDrive.Core;

/// <summary>
/// Balance/content table for the delivery page. The rates and the CredP
/// Max's first store visit hands over load from Data/delivery_catalog.json at
/// startup; <see cref="Stores"/> carries the real shop rows the host derives
/// from POIs (normally empty in the data file). This Default is the test-shell
/// fallback (empty board), not the source of truth.
/// </summary>
public sealed record DeliveryCatalog(
    IReadOnlyList<DeliveryStore> Stores,
    double WholesaleRate,
    int StartCredP,
    double PayoutRate = 0.9)
{
    public static readonly DeliveryCatalog Default = new([], 0.7, 100);

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
    /// charges WholesaleRate x retail per unit).
    /// </summary>
    public double OrderCost(DeliveryStore store) =>
        Math.Round(store.PricePerUnit * WholesaleRate * store.UnitsNeeded, 2);

    /// <summary>
    /// Retail payout of one full-restock delivery (Unity contract: the store
    /// pays PayoutRate x retail per unit — ApplyStoreDeliveryEffects' 0.9x
    /// price take).
    /// </summary>
    public double PayoutCost(DeliveryStore store) =>
        Math.Round(store.PricePerUnit * PayoutRate * store.UnitsNeeded, 2);

    /// <summary>
    /// The board with one placed store appended (Unity's backfill). An
    /// existing key wins, so a repeated placement or a boot rebuild never
    /// duplicates a row. Cold path: a hub purchase or the next expansion.
    /// </summary>
    public DeliveryCatalog WithStore(DeliveryStore store)
    {
        if (store is null || Find(store.Key) is not null)
        {
            return this;
        }

        var stores = new List<DeliveryStore>(Stores) { store };
        return this with { Stores = stores };
    }

    /// <summary>The board with every not-yet-known row appended.</summary>
    public DeliveryCatalog WithStores(IEnumerable<DeliveryStore> stores)
    {
        DeliveryCatalog board = this;
        foreach (DeliveryStore store in stores)
        {
            board = board.WithStore(store);
        }
        return board;
    }
}
