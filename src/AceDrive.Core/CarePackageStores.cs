// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// The store draw behind a care-package grant (PORTPLAN item 21, Unity
// CarePackageService.AvailablePoiKeys): a kit waits at a shop door the player
// can actually drive to, so a door that does not resolve on the address book
// can never hold one.
namespace AceDrive.Core;

/// <summary>
/// The shops a care kit may be dropped at: the store families the interior
/// handlers already know. Unity filters to the player's home town; every
/// discovered store door of that town qualifies, whether or not the taxi zone
/// has reached its street yet — the player has already driven there.
/// </summary>
public static class CarePackageStores
{
    /// <summary>
    /// Every discovered store door that resolves on <paramref name="addressable"/>,
    /// in directory order. The caller passes the whole address book, not
    /// <see cref="OperatingZone.ZoneMap"/>: the zone is the taxi job's scope,
    /// and filtering on it dropped real grants for shops the player had just
    /// walked into. Order is stable so a seeded draw reproduces the same store
    /// in tests and across a relaunch.
    /// </summary>
    public static IReadOnlyList<PointOfInterest> Eligible(
        IEnumerable<PointOfInterest>? doors, AddressMap addressable)
    {
        ArgumentNullException.ThrowIfNull(addressable);
        List<PointOfInterest> stores = [];
        if (doors is null)
        {
            return stores;
        }

        foreach (PointOfInterest poi in doors)
        {
            if (poi is not null
                && PlacedStore.IsStoreType(poi.Type)
                && poi.Address.HouseNumber > 0
                && !string.IsNullOrWhiteSpace(poi.Address.StreetName)
                && AddressBook.StreetByName(addressable, poi.Address.StreetName) is not null)
            {
                stores.Add(poi);
            }
        }

        return stores;
    }

    /// <summary>
    /// One store, uniformly, on the caller's seeded stream. Null when there is
    /// nobody to draw: the caller reports the lost grant instead of inventing a
    /// door.
    /// </summary>
    public static PointOfInterest? Pick(
        IReadOnlyList<PointOfInterest>? stores, Func<int, int> nextInt)
    {
        if (stores is not { Count: > 0 } || nextInt is null)
        {
            return null;
        }

        int index = Math.Clamp(nextInt(stores.Count), 0, stores.Count - 1);
        return stores[index];
    }
}
