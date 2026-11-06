// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 16-19
// (Delivery Job page, port of Unity DeliveryJobScreen + DeliveryCartAccounting).
namespace AceDrive.Core;

/// <summary>
/// Confirmed restock orders waiting for hub pickup (Unity
/// player.pickupCart). One line per store, carrying the units the player
/// could afford and the trunk litres they take; a confirmed store re-answers
/// to its row and drops the order. The committed cash and volume are the
/// page's budget guards: a second order may only use what the cart has not
/// already earmarked.
/// </summary>
public sealed class DeliveryCart
{
    private readonly List<DeliveryOrder> _orders = new();

    public IReadOnlyList<DeliveryOrder> Orders => _orders;

    public bool IsEmpty => _orders.Count == 0;

    public DeliveryPhase Phase => IsEmpty ? DeliveryPhase.OpenCareer : DeliveryPhase.Deliver;

    /// <summary>Wholesale CR already earmarked by the cart's orders.</summary>
    public double TotalCost
    {
        get
        {
            double total = 0.0;
            foreach (DeliveryOrder order in _orders)
            {
                total += order.WholesaleCost;
            }
            return Math.Round(total, 2, MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>Trunk litres already earmarked by the cart's orders.</summary>
    public double TotalVolume
    {
        get
        {
            double total = 0.0;
            foreach (DeliveryOrder order in _orders)
            {
                total += order.Volume;
            }
            return Math.Round(total, 3, MidpointRounding.AwayFromZero);
        }
    }

    public bool Contains(string key) => Find(key) is not null;

    /// <summary>The cart's line for a store, or null when it holds no order.</summary>
    public DeliveryOrder? Find(string key)
    {
        foreach (DeliveryOrder order in _orders)
        {
            if (order.StoreKey == key) return order;
        }
        return null;
    }

    /// <summary>Confirms an order; already-confirmed stores are left as-is.</summary>
    public void Confirm(DeliveryOrder order)
    {
        if (!Contains(order.StoreKey)) _orders.Add(order);
    }

    public void Remove(string key) => _orders.RemoveAll(order => order.StoreKey == key);

    public void Clear() => _orders.Clear();

    /// <summary>
    /// The whole cart as one run, in cart order. Empty carts run nothing; the
    /// run's money is derived from its lines so the page's budget guard and
    /// the hand-over charge read the same numbers.
    /// </summary>
    public DeliveryRun? Run() =>
        _orders.Count == 0 ? null : new DeliveryRun([.. _orders]);
}
