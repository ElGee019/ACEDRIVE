// Places a POI at a random house on the placeholder address map (PORTPLAN
// 24/27). Randomness is a seeded instance — never the shared global (AGENTS)
// — so tests pin the address and a launch still surprises the player.
namespace AceDrive.Core;

/// <summary>
/// Draws one named POI and one house number from the map. The generator
/// advances its own RNG; the onboarding flow banks the first draw so a later
/// launch reuses the same door, while a fresh save still gets a new address.
/// The composition root seeds it per launch.
/// </summary>
public sealed class PoiGenerator
{
    private readonly AddressMap _map;
    private readonly IReadOnlyList<PoiDefinition> _definitions;
    private readonly Random _rng;

    public PoiGenerator(
        AddressMap map, IReadOnlyList<PoiDefinition> definitions, int seed)
    {
        _map = map;
        _definitions = definitions;
        _rng = new Random(seed);
    }

    /// <summary>
    /// Null when there is no definition or the map has no house number.
    /// Never throws.
    /// </summary>
    public PointOfInterest? Spawn()
    {
        if (_definitions.Count == 0 || _map.Streets is not { Count: > 0 })
        {
            return null;
        }

        int total = TotalHouses();
        if (total <= 0)
        {
            return null;
        }

        // WHY the single-definition branch: with one business the draw would
        // only consume a random step, so the first address stays as simple as
        // "the first house this seed lands on".
        PoiDefinition definition = _definitions.Count == 1
            ? _definitions[0]
            : _definitions[_rng.Next(_definitions.Count)];

        return Place(definition, DrawAddress(total));
    }

    /// <summary>
    /// Places one named definition at a freshly drawn house. The story errand
    /// pins Max's shop by id, so it never depends on the random definition
    /// pick above. Null when the id is unknown or the map has no house. Never
    /// throws.
    /// </summary>
    public PointOfInterest? Spawn(string definitionId)
    {
        if (_map.Streets is not { Count: > 0 } || string.IsNullOrWhiteSpace(definitionId))
        {
            return null;
        }

        int total = TotalHouses();
        if (total <= 0)
        {
            return null;
        }

        foreach (PoiDefinition definition in _definitions)
        {
            if (string.Equals(definition.Id, definitionId, StringComparison.Ordinal))
            {
                return Place(definition, DrawAddress(total));
            }
        }

        return null;
    }

    /// <summary>
    /// Draws one house from the whole map. Story destinations that only need a
    /// door (the community centre) use this; the same random walk keeps every
    /// house equally likely. Null when the map has no house. Never throws.
    /// </summary>
    public AddressLocation? DrawAddress()
    {
        if (_map.Streets is not { Count: > 0 })
        {
            return null;
        }

        int total = TotalHouses();
        return total <= 0 ? null : DrawAddress(total);
    }

    /// <summary>
    /// Rebuilds a previously banked POI from its id and address without
    /// advancing the RNG. Null when the content table no longer carries the
    /// business, so an edited data file degrades to a fresh draw.
    /// </summary>
    public PointOfInterest? Place(string id, AddressLocation address)
    {
        foreach (PoiDefinition definition in _definitions)
        {
            if (string.Equals(definition.Id, id, StringComparison.Ordinal))
            {
                return Place(definition, address);
            }
        }

        return null;
    }

    private static PointOfInterest? Place(PoiDefinition definition, AddressLocation? address) =>
        address is { } door
            ? new PointOfInterest(
                definition.Id,
                definition.Name,
                door,
                definition.Type,
                definition.OwnerName,
                definition.Category,
                definition.GuaranteedItems)
            : null;

    private AddressLocation? DrawAddress(int total)
    {
        int pick = _rng.Next(total);
        foreach (AddressStreet? street in _map.Streets)
        {
            if (street is null)
            {
                continue;
            }

            int count = HouseNumberPlan.Count(street, _map);
            if (pick < count)
            {
                int number = pick + 1;
                return new AddressLocation(
                    street.Name, number, HouseNumberPlan.SideOf(number));
            }
            pick -= count;
        }

        // Unreachable with a positive total; a data edit between the two
        // passes must still not throw.
        return null;
    }

    private int TotalHouses()
    {
        int total = 0;
        foreach (AddressStreet? street in _map.Streets)
        {
            if (street is not null)
            {
                total += HouseNumberPlan.Count(street, _map);
            }
        }
        return total;
    }

    /// <summary>
    /// Draws one house on one street. The friends' places sit in the street
    /// where the player grew up, so the street is data (the profile's) instead
    /// of a random pick. Excluded numbers (the player's own house, a friend
    /// already placed) are skipped so two people never share a door; a
    /// one-house street still answers it (a neighbour is impossible there, a
    /// missing destination is worse). Null when the street has no free house.
    /// </summary>
    public AddressLocation? DrawHouse(AddressStreet street, params int[] excludeHouseNumbers)
    {
        int count = HouseNumberPlan.Count(street, _map);
        if (count <= 0)
        {
            return null;
        }

        if (DrawHouseNumber(count, excludeHouseNumbers) is not { } number)
        {
            return null;
        }

        return new AddressLocation(street.Name, number, HouseNumberPlan.SideOf(number));
    }

    // Walks the numbers that are still free and picks one slot uniformly, so
    // every remaining house stays equally likely (no retry loop).
    private int? DrawHouseNumber(int count, int[] exclude)
    {
        int free = 0;
        for (int number = 1; number <= count; number++)
        {
            if (!IsExcluded(number, exclude))
            {
                free++;
            }
        }

        if (free == 0)
        {
            // A street with a single house cannot host a second door: keeping
            // the only address beats having no destination at all.
            return count == 1 ? 1 : null;
        }

        int slot = _rng.Next(free);
        for (int number = 1; number <= count; number++)
        {
            if (IsExcluded(number, exclude))
            {
                continue;
            }

            if (slot == 0)
            {
                return number;
            }

            slot--;
        }

        return null;
    }

    private static bool IsExcluded(int number, int[] exclude)
    {
        foreach (int candidate in exclude)
        {
            if (candidate == number)
            {
                return true;
            }
        }

        return false;
    }
}
