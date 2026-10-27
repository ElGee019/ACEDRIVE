// The driven car's trunk (PORTPLAN items 26/51, port of Unity
// PlayerData.trunkItems): goods the player bought, merged by name, measured
// in litres against the current car's trunk. Mutable state holder like
// DriverWallet; every mutation is a cold path (a purchase or a load).
namespace AceDrive.Core;

/// <summary>Everything the driver carries, plus the trunk maths the shop page needs.</summary>
public sealed class Trunk
{
    private readonly List<TrunkItem> _items = [];

    public Trunk(IReadOnlyList<TrunkItem>? saved = null)
    {
        if (saved is null)
        {
            return;
        }

        foreach (TrunkItem? item in saved)
        {
            // Hand-edited/corrupt rows are dropped rather than poisoning the
            // volume maths; the next purchase writes a clean stack. A zero
            // unit volume is a keepsake the certificate path banked, never a
            // corrupt row, so it must survive the load.
            if (item is null
                || string.IsNullOrWhiteSpace(item.Name)
                || item.Quantity <= 0
                || !double.IsFinite(item.UnitVolume)
                || item.UnitVolume < 0.0)
            {
                continue;
            }
            Merge(item.Name, item.UnitVolume, item.Quantity);
        }
    }

    /// <summary>Live stacks; callers must not mutate them (use Snapshot for saving).</summary>
    public IReadOnlyList<TrunkItem> Items => _items;

    /// <summary>Litres currently occupied.</summary>
    public double UsedLitres
    {
        get
        {
            double total = 0.0;
            foreach (TrunkItem item in _items)
            {
                total += item.UnitVolume * item.Quantity;
            }
            return total;
        }
    }

    /// <summary>Free litres against the current car; negative means overloaded.</summary>
    public double Remaining(double capacity) =>
        double.IsFinite(capacity) ? capacity - UsedLitres : 0.0;

    /// <summary>Merges a bought item into the trunk.</summary>
    public void Add(StoreItem item, int quantity) =>
        Add(item.Name, quantity, item.Volume);

    /// <summary>
    /// True when a stack with this name is carried. Story errands use it to
    /// see whether their guaranteed item was bought, regardless of stack size.
    /// </summary>
    public bool Contains(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        foreach (TrunkItem item in _items)
        {
            if (string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return item.Quantity > 0;
            }
        }

        return false;
    }

    /// <summary>
    /// Merges a stack; invalid/zero quantities are ignored.
    /// </summary>
    public void Add(string name, int quantity, double unitVolume)
    {
        if (string.IsNullOrWhiteSpace(name) || quantity <= 0
            || !double.IsFinite(unitVolume) || unitVolume <= 0.0)
        {
            return;
        }

        Merge(name, unitVolume, quantity);
    }

    /// <summary>
    /// Adds a keepsake: a certificate or award the game hands over as a card
    /// (Unity's 0-volume glove-compartment item), so it never consumes trunk
    /// space and a full trunk still takes it. The 0 L stack survives the
    /// snapshot/load round trip because the loader accepts a zero unit volume.
    /// </summary>
    public void AddKeepsake(string name, int quantity)
    {
        if (string.IsNullOrWhiteSpace(name) || quantity <= 0)
        {
            return;
        }

        Merge(name, 0.0, quantity);
    }

    private void Merge(string name, double unitVolume, int quantity)
    {
        foreach (TrunkItem item in _items)
        {
            if (string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                item.Quantity += quantity;
                return;
            }
        }
        _items.Add(new TrunkItem(name, unitVolume) { Quantity = quantity });
    }

    /// <summary>
    /// Removes one unit of a stack and drops it when the last unit goes
    /// (Unity YouScreen.OnTrunkItemClicked decrements, then removes at zero).
    /// False when the stack is absent, so the caller never eats from nowhere.
    /// </summary>
    public bool RemoveOne(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        for (int i = 0; i < _items.Count; i++)
        {
            TrunkItem item = _items[i];
            if (!string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (item.Quantity > 1)
            {
                item.Quantity--;
            }
            else
            {
                _items.RemoveAt(i);
            }
            return true;
        }

        return false;
    }

    /// <summary>A detached copy for driver.json.</summary>
    public IReadOnlyList<TrunkItem> Snapshot()
    {
        var copy = new List<TrunkItem>(_items.Count);
        foreach (TrunkItem item in _items)
        {
            copy.Add(item with { });
        }
        return copy;
    }
}
