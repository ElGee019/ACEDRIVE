// Draws the two addresses of a taxi proposal (PORTPLAN item 11, Unity
// TaxiJobGenerator steps 1-2): one house on the customer's street, one on a
// second street, with the optional radius around the player's home applied
// before any name is resolved. Pure and seeded.
namespace AceDrive.Core;

/// <summary>
/// Unity's address draw: filter the map's houses by the radius and the player's
/// own door, group the survivors by street, then pick the customer's home and a
/// second residence. The two legs sit on different streets whenever the map
/// offers a second one, so the
/// phrase can say "home" at one door and "the {surname} residence" at the
/// other. A zone that has discovered a single street falls back to a second
/// house on that same street — the copy already reads naturally for a
/// neighbour ("the {surname} residence" two doors down) and a one-street zone
/// must still propose jobs (TOZONES §4.7). A door a POI already owns stays in
/// the pool — the house is a business, not a vacancy — and
/// <see cref="TaxiJobProposer"/> names its owner as the occupant, so a POI
/// owner can be a passenger too. A banked resident's door is NOT excluded
/// either: the resident book resolves it to the same person every time, and
/// a returning customer is the point of that table.
/// </summary>
public static class TaxiAddressPlan
{
    /// <summary>
    /// False when the map has no street with a usable house inside the radius
    /// that is still free, or when a single-street map cannot offer a second
    /// free house on it. The caller then keeps the page's fallback copy. Never
    /// throws for an invalid map or geometry.
    /// </summary>
    /// <param name="home">The player's own door: never a customer address,
    /// because the player already lives there.</param>
    public static bool TryDraw(
        AddressMap map, Random rng, AddressLocation? home, int radiusKm,
        out AddressLocation customerHome, out AddressLocation secondResidence)
    {
        customerHome = default;
        secondResidence = default;

        int radius = TaxiJobRadius.Effective(radiusKm);
        AddressLocation origin = home.GetValueOrDefault();
        bool limited = radius > 0 && home.HasValue;

        var streets = new List<AddressStreet>();
        var houses = new List<List<int>>();
        foreach (AddressStreet street in map.Streets)
        {
            int count = HouseNumberPlan.Count(street, map);
            if (count <= 0)
            {
                continue;
            }

            var valid = new List<int>(count);
            for (int number = 1; number <= count; number++)
            {
                var address = new AddressLocation(
                    street.Name, number, HouseNumberPlan.SideOf(number));
                if (home is { } player && address.Matches(player))
                {
                    // The player already lives there: never a customer door.
                    continue;
                }

                if (limited && !AddressBook.WithinRadius(map, origin, address, radius))
                {
                    continue;
                }

                valid.Add(number);
            }

            if (valid.Count > 0)
            {
                streets.Add(street);
                houses.Add(valid);
            }
        }

        if (streets.Count == 0)
        {
            return false;
        }

        int firstStreet = rng.Next(streets.Count);
        List<int> firstHouses = houses[firstStreet];
        int firstIndex = rng.Next(firstHouses.Count);
        int firstNumber = firstHouses[firstIndex];

        int secondStreet;
        int secondNumber;
        if (streets.Count >= 2)
        {
            // Pick uniformly among the OTHER streets; the shift avoids a retry
            // loop and keeps the same distribution as Unity's do/while.
            secondStreet = rng.Next(streets.Count - 1);
            if (secondStreet >= firstStreet)
            {
                secondStreet++;
            }
            List<int> secondHouses = houses[secondStreet];
            secondNumber = secondHouses[rng.Next(secondHouses.Count)];
        }
        else
        {
            // One discovered street: the second door must be another house, or
            // the proposal would send the passenger to the address they are
            // standing at. A street with a single valid door cannot supply it.
            if (firstHouses.Count < 2)
            {
                return false;
            }
            int slot = rng.Next(firstHouses.Count - 1);
            secondStreet = firstStreet;
            secondNumber = firstHouses[slot >= firstIndex ? slot + 1 : slot];
        }

        customerHome = new AddressLocation(
            streets[firstStreet].Name, firstNumber, HouseNumberPlan.SideOf(firstNumber));
        secondResidence = new AddressLocation(
            streets[secondStreet].Name, secondNumber, HouseNumberPlan.SideOf(secondNumber));
        return true;
    }
}
