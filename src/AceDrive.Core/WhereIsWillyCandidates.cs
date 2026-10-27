// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: ToWhereIsWilly.md §2.2/§2.4/§3.1 (Unity JobSelectionScreen's
// coffee-shop + elder draw and WhereIsWillyHandler's shop/elder lookups).
namespace AceDrive.Core;

/// <summary>
/// The Wednesday run's draws, kept apart from the flow so each is a pure,
/// deterministic function of its input: the town's coffee shops, the residents
/// a missing elder may be drawn from (never a venue owner), the pick itself and
/// the free-house fallback that keeps an empty resident table from dead-ending
/// the event.
/// </summary>
public static class WhereIsWillyCandidates
{
    /// <summary>
    /// Every discovered coffee-shop door, in directory order. A row without a
    /// real house number is skipped: the quest cannot pin a door that does not
    /// resolve on the map.
    /// </summary>
    public static IReadOnlyList<PointOfInterest> CoffeeShops(
        IEnumerable<PointOfInterest>? doors)
    {
        List<PointOfInterest> shops = [];
        if (doors is null)
        {
            return shops;
        }

        foreach (PointOfInterest poi in doors)
        {
            if (poi is not null
                && string.Equals(poi.Type, PoiTypes.CoffeeShop, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(poi.Address.StreetName)
                && poi.Address.HouseNumber > 0)
            {
                shops.Add(poi);
            }
        }

        return shops;
    }

    /// <summary>
    /// Unity's candidate filter: every resident whose base name is not a venue
    /// owner's, because a shop or hub owner can never be the missing elder.
    /// Order is the book's, so the caller's seeded pick stays deterministic.
    /// </summary>
    public static IReadOnlyList<Resident> EligibleResidents(
        IEnumerable<Resident>? residents, IEnumerable<string>? venueOwners)
    {
        List<Resident> candidates = [];
        if (residents is null)
        {
            return candidates;
        }

        var owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (venueOwners is not null)
        {
            foreach (string owner in venueOwners)
            {
                string name = (owner ?? "").Trim();
                if (name.Length > 0)
                {
                    owners.Add(name);
                }
            }
        }

        foreach (Resident resident in residents)
        {
            if (resident?.Name is null || resident.Name.BaseName.Length == 0)
            {
                continue;
            }
            if (!owners.Contains(resident.Name.BaseName))
            {
                candidates.Add(resident);
            }
        }

        return candidates;
    }

    /// <summary>
    /// One candidate, uniformly, on the caller's seeded stream. Null when there
    /// is nobody to draw (the flow then falls back to a free house).
    /// </summary>
    public static Resident? Pick(
        IReadOnlyList<Resident>? candidates, Func<int, int> nextInt)
    {
        if (candidates is not { Count: > 0 } || nextInt is null)
        {
            return null;
        }

        int index = Math.Clamp(nextInt(candidates.Count), 0, candidates.Count - 1);
        return candidates[index];
    }

    /// <summary>
    /// The empty-book fallback (adaptation A3): one house no known POI or
    /// resident already lives at, drawn on the caller's seeded stream over the
    /// map's streets with <see cref="PoiGenerator.DrawHouse"/>. The elder can
    /// never share a door with a discovered place, and a map with no free house
    /// answers null so the event degrades to its silent return.
    /// </summary>
    public static AddressLocation? PickFreeHouse(
        AddressMap map, PoiGenerator generator, IEnumerable<AddressLocation> taken)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(generator);

        List<AddressLocation> used = [.. taken];
        foreach (AddressStreet street in map.Streets)
        {
            List<int> excluded = [];
            foreach (AddressLocation address in used)
            {
                if (string.Equals(
                    address.StreetName.Trim(), street.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    excluded.Add(address.HouseNumber);
                }
            }

            if (generator.DrawHouse(street, [.. excluded]) is { } free)
            {
                return free;
            }
        }

        return null;
    }
}
