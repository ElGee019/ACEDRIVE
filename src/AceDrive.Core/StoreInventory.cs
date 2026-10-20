// One shop's shelves this session (PORTPLAN items 25/26, port of Unity
// StoreState.slots). The aggregate delivery-city cell stays StoreState;
// this is the per-item shop inventory the store page reads.
namespace AceDrive.Core;

/// <summary>
/// The live shelf list of one shop. The selected item set is fixed when the
/// shop is first entered; purchases only move the quantities.
/// </summary>
public sealed class StoreInventory
{
    private readonly List<StoreShelf> _shelves;

    public StoreInventory(string poiKey, IReadOnlyList<StoreItem> items, int defaultStock)
    {
        PoiKey = poiKey;
        int stock = Math.Max(0, defaultStock);
        _shelves = new List<StoreShelf>(items.Count);
        foreach (StoreItem item in items)
        {
            _shelves.Add(new StoreShelf(item, stock));
        }
    }

    /// <summary>The POI this shop belongs to.</summary>
    public string PoiKey { get; }

    /// <summary>Shelves in the order the store layout chose them.</summary>
    public IReadOnlyList<StoreShelf> Shelves => _shelves;

    /// <summary>Units left of an item; 0 when the shop does not carry it.</summary>
    public int StockOf(string itemName)
    {
        foreach (StoreShelf shelf in _shelves)
        {
            if (string.Equals(shelf.Item.Name, itemName, StringComparison.OrdinalIgnoreCase))
            {
                return shelf.Quantity;
            }
        }
        return 0;
    }

    /// <summary>Takes stock; false (and no move) when the shelf is short.</summary>
    public bool TryTake(string itemName, int quantity)
    {
        foreach (StoreShelf shelf in _shelves)
        {
            if (string.Equals(shelf.Item.Name, itemName, StringComparison.OrdinalIgnoreCase))
            {
                return shelf.TryTake(quantity);
            }
        }
        return false;
    }
}
