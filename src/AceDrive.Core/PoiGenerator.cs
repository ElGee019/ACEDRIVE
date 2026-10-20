// Places a POI at a random house on the placeholder address map (PORTPLAN
// 24/27). Randomness is a seeded instance — never the shared global (AGENTS)
// — so tests pin the address and a launch still surprises the player.
namespace AceDrive.Core;

/// <summary>
/// Draws one named POI and one house number from the map. The generator
/// advances its own RNG, so replaying the intro in one session yields a new
/// errand address; the composition root seeds it per launch.
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
                return new PointOfInterest(
                    definition.Id,
                    definition.Name,
                    new AddressLocation(
                        street.Name, number, HouseNumberPlan.SideOf(number)),
                    definition.Type,
                    definition.OwnerName,
                    definition.Category);
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
}
