// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 16-19
// (Delivery Job page, port of Unity DeliveryJobHandler).
namespace AceDrive.Core;

/// <summary>
/// Builds the legs of one run from the cart: one hub pickup per needed
/// wholesale category, then every ordered store in cart order. Unity asks the
/// player to click the hubs and stores on the map; the port's cart order is
/// the route, so the destination-setting wizard has no input here.
/// </summary>
public static class DeliveryJobPlanner
{
    /// <summary>
    /// The run's ordered stops for the given cart keys: each distinct owned
    /// category's newest hub first, then every store row the catalogue still
    /// knows. A missing store row is skipped, never fatal.
    /// </summary>
    public static IReadOnlyList<DeliveryStop> Plan(
        DeliveryCatalog catalog, DeliveryCareer career,
        IReadOnlyList<string> storeKeys)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(career);
        ArgumentNullException.ThrowIfNull(storeKeys);

        var stops = new List<DeliveryStop>();
        var picked = new HashSet<string>(StringComparer.Ordinal);

        // Hubs first, in the order the cart first needs each category, so the
        // pickup route mirrors Unity's ordered PickupHubStop list.
        foreach (string key in storeKeys)
        {
            if (catalog.Find(key) is not { Category.Length: > 0 } store
                || !picked.Add(store.Category)
                || career.OwnedFor(store.Category) is not { } hub)
            {
                continue;
            }

            stops.Add(new DeliveryStop(DeliveryStopKind.Hub, hub.Key, hub.Name));
        }

        // Then the store drop-offs, exactly the cart's confirmed order.
        foreach (string key in storeKeys)
        {
            if (catalog.Find(key) is { } store)
            {
                stops.Add(new DeliveryStop(DeliveryStopKind.Store, store.Key, store.Name));
            }
        }

        return stops;
    }
}
