// The delivery-board side of a real store door (PORTPLAN items 18/24/25, port
// of Unity StorePlacementHelper's grocery/coffee stores). Unity keeps one
// StoreState per store POI; the port collapses its item slots to one unit
// count, so a shop door becomes a DeliveryStore row derived from the shop
// category and the item catalogue.
namespace AceDrive.Core;

/// <summary>
/// Turns a real shop POI — Max's story grocery or a generated
/// <c>grocery_store</c> / <c>coffee_shop</c> — into the delivery board's row.
/// The POI is the persisted identity; the row's numeric columns are derived
/// from the same <see cref="StoreCatalog"/> the shop interior uses, so a
/// restart rebuilds the identical row.
/// </summary>
public static class PlacedStore
{
    /// <summary>
    /// The board row for this door, or null when the POI is not a shop or its
    /// category/layout is missing. Never throws: content edits must degrade to
    /// "shop only", not take the board down.
    /// </summary>
    public static DeliveryStore? Row(PointOfInterest? poi, StoreCatalog? catalog)
    {
        if (poi is null || catalog is null || !IsStoreType(poi.Type))
        {
            return null;
        }

        int capacity = Capacity(poi.Category, catalog);
        double price = catalog.AverageUnitPrice(poi.Category);
        if (capacity <= 0 || price <= 0.0)
        {
            return null;
        }

        return new DeliveryStore(
            poi.Id,
            poi.Name,
            poi.OwnerName,
            // Unity sets every slot to 20 when the store is placed: a new
            // store opens at full capacity.
            StockedUnits: capacity,
            UnitCapacity: capacity,
            PricePerUnit: Math.Round(price, 2),
            Category: poi.Category,
            // The POI is the persisted identity; its door is the destination
            // the delivery run drives to (placed stores need no second table).
            Address: poi.Address);
    }

    /// <summary>The rows for every real shop door in the directory.</summary>
    public static IReadOnlyList<DeliveryStore> Rows(
        IEnumerable<PointOfInterest> pois, StoreCatalog catalog)
    {
        var rows = new List<DeliveryStore>();
        foreach (PointOfInterest poi in pois)
        {
            if (Row(poi, catalog) is { } row)
            {
                rows.Add(row);
            }
        }
        return rows;
    }

    /// <summary>True for the shop families the board can restock. Max's story
    /// grocery is the real anchor of the grocery career; the generated grocery
    /// and coffee doors join it as each hub's backfill grows.</summary>
    public static bool IsStoreType(string type) =>
        string.Equals(type, PoiTypes.Grocery, StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, PoiTypes.MaxsGrocery, StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, PoiTypes.GroceryStore, StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, PoiTypes.CoffeeShop, StringComparison.OrdinalIgnoreCase);

    // Unity's placed shop has one 20-unit slot per generated item; the
    // collapsed port multiplies the category layout's slots by the
    // catalogue's default stock.
    private static int Capacity(string category, StoreCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return 0;
        }

        int slots = 0;
        if (catalog.Layouts is not null
            && catalog.Layouts.TryGetValue(category, out var layout))
        {
            foreach (int count in layout.Values)
            {
                slots += Math.Max(0, count);
            }
        }

        // A category with no explicit layout stocks every item of its own
        // category (StoreNetwork's fallback), so the row follows the same set.
        if (slots == 0)
        {
            slots = catalog.ByCategory(category).Count;
        }

        return slots * Math.Max(1, catalog.DefaultStock);
    }
}
