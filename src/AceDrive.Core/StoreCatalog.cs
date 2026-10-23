// Content table for shop shelves (PORTPLAN items 24/26, port of Unity's
// ItemDatabaseSO + StorePlacementHelper): every item plus the per-store
// category layout that decides which items a shop stocks. Data/store_items.json
// is the source of truth; Default is the test-shell fallback.
namespace AceDrive.Core;

/// <summary>
/// The item database and the per-store layout. <see cref="Layouts"/> maps a
/// store category ("groceries") to source-category counts (8 groceries +
/// 2 coffee beans), mirroring Unity's StorePlacementHelper.StoreItemLayout.
/// </summary>
public sealed record StoreCatalog(
    IReadOnlyList<StoreItem> Items,
    int DefaultStock,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>? Layouts = null)
{
    /// <summary>
    /// Small fallback used when Data/store_items.json is missing or invalid.
    /// Maple Syrup stays in the table because the birthday errand needs it.
    /// </summary>
    public static readonly StoreCatalog Default = new(
        [
            new StoreItem("MAPLE SYRUP", 45.99, 0.4, 3, "groceries"),
            new StoreItem("BANANA", 2.0, 0.3, 4, "groceries"),
            new StoreItem("GLAZED DONUT", 4.0, 0.36, 6, "groceries"),
        ],
        DefaultStock: 20,
        Layouts: new Dictionary<string, IReadOnlyDictionary<string, int>>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["groceries"] = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["groceries"] = 8,
                ["coffee_beans"] = 2,
            },
        });

    /// <summary>The item with this name, or null when the catalogue lost it.</summary>
    public StoreItem? Find(string name)
    {
        foreach (StoreItem item in Items)
        {
            if (string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }
        return null;
    }

    /// <summary>
    /// Every item in a source category, in catalogue order. A missing
    /// category answers an empty list; the caller decides how to degrade.
    /// </summary>
    public IReadOnlyList<StoreItem> ByCategory(string category)
    {
        var found = new List<StoreItem>();
        foreach (StoreItem item in Items)
        {
            if (string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase))
            {
                found.Add(item);
            }
        }
        return found;
    }

    /// <summary>
    /// The average retail price of the items one store of this category can
    /// draw, weighted by the layout's source counts. Used when a placed store
    /// joins the delivery board, which keeps one collapsed price per unit
    /// until the per-item order screen lands. Zero when the category is not
    /// in the catalogue.
    /// </summary>
    public double AverageUnitPrice(string category)
    {
        if (Layouts is not null && Layouts.TryGetValue(category, out var layout))
        {
            double weighted = 0.0;
            int units = 0;
            foreach ((string source, int count) in layout)
            {
                if (count <= 0)
                {
                    continue;
                }

                (double average, int priced) = AveragePriceOf(source);
                if (priced == 0)
                {
                    continue;
                }

                weighted += average * count;
                units += count;
            }

            if (units > 0 && weighted > 0.0)
            {
                return weighted / units;
            }
        }

        return AveragePriceOf(category).Average;
    }

    // Sum and count in one pass: an empty source category carries no weight.
    private (double Average, int Count) AveragePriceOf(string category)
    {
        double total = 0.0;
        int count = 0;
        foreach (StoreItem item in Items)
        {
            if (string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase)
                && item.Price > 0.0)
            {
                total += item.Price;
                count++;
            }
        }
        return count == 0 ? (0.0, 0) : (total / count, count);
    }
}
