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

    // A nanolitre of slack for the exact-fit division (0.4 × 25 against
    // 10.0): below any volume the page can show, above binary residue.
    private const double VolumeTolerance = 1e-9;

    public DeliveryStore? Find(string key)
    {
        foreach (DeliveryStore store in Stores)
        {
            if (store.Key == key) return store;
        }
        return null;
    }

    /// <summary>
    /// Wholesale cost of an order for <paramref name="units"/> (Unity
    /// contract: the hub charges WholesaleRate x retail per unit).
    /// </summary>
    public double OrderCost(DeliveryStore store, int units) =>
        Math.Round(store.PricePerUnit * WholesaleRate * Math.Max(0, units), 2);

    /// <summary>
    /// Retail payout of a delivery of <paramref name="units"/> (Unity
    /// contract: the store pays PayoutRate x retail per unit —
    /// ApplyStoreDeliveryEffects' 0.9x price take).
    /// </summary>
    public double PayoutCost(DeliveryStore store, int units) =>
        Math.Round(store.PricePerUnit * PayoutRate * Math.Max(0, units), 2);

    /// <summary>
    /// Most units of the store's current need a budget buys, rounded down so
    /// the priced order can never overshoot it. The page passes cash minus
    /// what the cart already committed; the remainder stays on the shelf for
    /// a later run.
    /// </summary>
    public int AffordableUnits(DeliveryStore store, double budget)
    {
        if (store.UnitsNeeded <= 0 || !double.IsFinite(budget) || budget <= 0.0)
        {
            return 0;
        }

        double unitCost = store.PricePerUnit * WholesaleRate;
        if (unitCost <= 0.0)
        {
            return store.UnitsNeeded;
        }

        int units = Math.Min((int)Math.Floor(budget / unitCost), store.UnitsNeeded);
        // Binary residue can round a full order a cent over the budget; the
        // quote is what the wallet charges, so step back until it fits.
        while (units > 0 && OrderCost(store, units) > budget)
        {
            units--;
        }
        return units;
    }

    /// <summary>
    /// Most units of the store's current need the car's free litres carry,
    /// rounded down so the priced line can never overshoot them. A store with
    /// no unit volume (legacy/hand-authored content) is bounded by cash only;
    /// a full/absent trunk answers 0.
    /// </summary>
    public static int AffordableUnitsByVolume(DeliveryStore store, double freeLitres)
    {
        if (store.UnitsNeeded <= 0)
        {
            return 0;
        }
        if (store.LitresPerUnit <= 0.0)
        {
            return store.UnitsNeeded;
        }
        if (double.IsNaN(freeLitres) || freeLitres <= 0.0)
        {
            return 0;
        }
        if (double.IsPositiveInfinity(freeLitres))
        {
            return store.UnitsNeeded;
        }

        // Exact fits (25 × 0.4 L against 10.0 L) must not lose a unit to the
        // binary residue of the division; an order is rounded to millilitres.
        int units = (int)Math.Floor((freeLitres + VolumeTolerance) / store.LitresPerUnit);
        return Math.Clamp(units, 0, store.UnitsNeeded);
    }

    /// <summary>One priced order line for <paramref name="units"/> of the store's need.</summary>
    public DeliveryOrder Order(DeliveryStore store, int units)
    {
        // A line never carries more than the shelf is missing: the page caps
        // by budget and trunk space, this caps by need, and either overrun
        // would overfill.
        int capped = Math.Clamp(units, 0, store.UnitsNeeded);
        return new DeliveryOrder(
            store.Key,
            capped,
            OrderCost(store, capped),
            PayoutCost(store, capped),
            VolumeOf(store, capped));
    }

    /// <summary>Trunk litres an order of <paramref name="units"/> takes.</summary>
    private static double VolumeOf(DeliveryStore store, int units) =>
        store.LitresPerUnit <= 0.0
            ? 0.0
            : Math.Round(Math.Max(0, units) * store.LitresPerUnit, 3);

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
