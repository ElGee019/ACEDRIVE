// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 16-19
// (Delivery Job page, port of Unity DeliveryJobScreen + DeliveryCartAccounting).
namespace AceDrive.Core;

/// <summary>
/// One store that accepts wholesale restock orders. Plain data; money in
/// EUR. Stocked/capacity mirror the Unity slots model collapsed to unit
/// counts until the per-item order screen (PORTPLAN 17) lands; StockedUnits
/// is the fill the town starts with — the live shelf count is CityEconomy's.
/// Category is the wholesale career that supplies the store (item 18):
/// its stores stay off the board until a hub of that category is bought.
/// No category means open content that never needed a career.
/// </summary>
public sealed record DeliveryStore(
    string Key,
    string Name,
    string Owner,
    int StockedUnits,
    int UnitCapacity,
    double PricePerUnit,
    string Category = "")
{
    /// <summary>Unity StoreListView: rows under 25 % flag as URGENT.</summary>
    public const double UrgentPercent = 25.0;

    /// <summary>Unity color-band edge between amber and green.</summary>
    public const double LowPercent = 60.0;

    public int UnitsNeeded => Math.Max(0, UnitCapacity - StockedUnits);

    /// <summary>Shelf fill of any stock count — the one percent formula both
    /// the row bands and the town's growth band read (CityEconomy).</summary>
    public double PercentOf(int stockedUnits) =>
        UnitCapacity <= 0
            ? 0.0
            : 100.0 * Math.Clamp(stockedUnits, 0, UnitCapacity) / UnitCapacity;

    public double StockPercent => PercentOf(StockedUnits);

    public bool IsUrgent => StockPercent < UrgentPercent;

    public StockLevel Level => StockPercent switch
    {
        < UrgentPercent => StockLevel.Red,
        < LowPercent => StockLevel.Amber,
        _ => StockLevel.Green,
    };
}
