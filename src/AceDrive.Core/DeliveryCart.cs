// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 16-19
// (Delivery Job page, port of Unity DeliveryJobScreen + DeliveryCartAccounting).
namespace AceDrive.Core;

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
